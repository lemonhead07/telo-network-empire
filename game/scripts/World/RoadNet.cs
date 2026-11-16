using Godot;
using Telo.Sim.Content;
using Telo.Sim.Planet;

namespace Telo.Game.World;

/// <summary>
/// The van's road map: points along Main Street, up every driveway, along the road out of town, and round the farmyard,
/// joined where you can actually drive. <see cref="Route"/> finds the shortest way between two places along them,
/// so a driver keeps to the street and turns in at the driveway instead of cutting across lawns and through houses.
/// Navigation only: it decides how to get somewhere, never what happens there.
/// </summary>
public sealed class RoadNet
{
    private readonly List<Vector3> _nodes = new();
    private readonly List<List<int>> _links = new();
    private readonly HashSet<int> _hubs = new();   // street and farmyard points a road out of town may join
    private readonly PhysicsDirectSpaceState3D _space;
    private readonly Godot.Collections.Array<Rid> _ignore;

    public IReadOnlyList<Vector3> Nodes => _nodes;

    public RoadNet(PlanetGen gen, Town town, PhysicsDirectSpaceState3D space, Godot.Collections.Array<Rid> ignore)
    {
        _space = space; _ignore = ignore;
        float R = (float)gen.RadiusM;

        // every street, a point every 6 m; a street that ends on another joins it there
        var street = new List<int>();
        var ends = new List<(int Node, TownBuilder.Street Of)>();
        var of = new Dictionary<int, TownBuilder.Street>();
        foreach (var st in TownBuilder.Streets)
        {
            if (gen.SiteById(st.Site) is not { } site) continue;
            var ids = new List<int>();
            for (float x = st.From; x <= st.To + 0.01f; x += 6)
            {
                var p = (st.NorthSouth ? TownBuilder.Place(gen, site, st.At, x, 0, 0.3f) : TownBuilder.Place(gen, site, x, st.At, 0, 0.3f)).Origin;
                int id = Add(p, hub: true); ids.Add(id); of[id] = st;
            }
            for (int i = 1; i < ids.Count; i++) Link(ids[i - 1], ids[i]);
            street.AddRange(ids);
            if (ids.Count > 0) { ends.Add((ids[0], st)); ends.Add((ids[^1], st)); }
        }
        foreach (var (end, st) in ends)
            if (street.Where(n => of[n] != st).OrderBy(n => _nodes[n].DistanceSquaredTo(_nodes[end])).FirstOrDefault(-1) is var j and >= 0
                && _nodes[j].DistanceTo(_nodes[end]) < 9) Link(end, j);

        // every driveway joins the street at the two street points either side of it
        void Driveway(Vector3 entry, Vector3 park)
        {
            int e = Add(entry), p = Add(park);
            Link(e, p);
            foreach (int s in street.OrderBy(s => _nodes[s].DistanceSquaredTo(entry)).Take(2)) Link(s, e);
        }
        Driveway(town.HqEntry, town.HqPark);
        foreach (var lot in town.Lots)
            if (lot is { DriveEntry: { } en, Park: { } pk }) Driveway(en, pk);

        // the farmyard: a ring of points round the farmhouse (clear of the barn), and the yard where the van stops
        foreach (var farm in town.Lots.Where(l => l.Lot.Kind == LotKind.Farm && l.Park is not null))
        {
            if (gen.SiteById(farm.Lot.Site) is not { } site) continue;
            var ring = new List<int>();
            for (int k = 0; k < 12; k++)
            {
                float a = k * Mathf.Tau / 12;
                ring.Add(Add(TownBuilder.Place(gen, site, (float)farm.Lot.East + 13 * Mathf.Cos(a), (float)farm.Lot.South + 13 * Mathf.Sin(a), 0, 0.3f).Origin, hub: true));
            }
            for (int k = 0; k < ring.Count; k++) LinkIfClear(ring[k], ring[(k + 1) % ring.Count]);
            int yard = Add(farm.Park!.Value, hub: true);
            foreach (int r in ring) LinkIfClear(yard, r);
        }

        // roads out of town: their points outside the settlements, joined end to end, and each end to the nearest clear street or yard point
        foreach (var road in gen.Roads)
        {
            var ids = new List<int>();
            foreach (var p in road.Points)
            {
                if (gen.Sites.Any(s => (p - s.Dir).Length * gen.RadiusM < s.RadiusM * 0.5)) continue;
                var d = PlanetBuilder.ToGodot(p).Normalized();
                ids.Add(Add(d * (R + (float)gen.Height(p) + 0.3f)));
            }
            for (int i = 1; i < ids.Count; i++) Link(ids[i - 1], ids[i]);
            if (ids.Count == 0) continue;
            foreach (int end in new[] { ids[0], ids[^1] })
            {
                var near = _hubs.OrderBy(h => _nodes[h].DistanceSquaredTo(_nodes[end])).Take(12).ToList();
                int best = near.FirstOrDefault(h => _nodes[h].DistanceTo(_nodes[end]) < 150 && Clear(_nodes[h], _nodes[end]), -1);
                Link(end, best >= 0 ? best : near[0]);
            }
        }
    }

    /// <summary>
    /// The way from here to there along the roads: the points to drive through, ending at <paramref name="to"/>.
    /// A short clear hop goes straight there.
    /// </summary>
    public List<Vector3> Route(Vector3 from, Vector3 to)
    {
        if (from.DistanceTo(to) < 30 && Clear(from, to)) return new() { to };
        int start = Nearest(from), goal = _nodes.FindIndex(n => n.DistanceTo(to) < 0.5f);
        bool goalIsNode = goal >= 0;
        if (!goalIsNode) goal = Nearest(to);

        // Dijkstra: the graph is a few hundred points
        var dist = Enumerable.Repeat(float.MaxValue, _nodes.Count).ToArray();
        var prev = Enumerable.Repeat(-1, _nodes.Count).ToArray();
        var open = new PriorityQueue<int, float>();
        dist[start] = 0; open.Enqueue(start, 0);
        while (open.TryDequeue(out int u, out float du))
        {
            if (u == goal) break;
            if (du > dist[u]) continue;
            foreach (int v in _links[u])
            {
                float alt = du + _nodes[u].DistanceTo(_nodes[v]);
                if (alt < dist[v]) { dist[v] = alt; prev[v] = u; open.Enqueue(v, alt); }
            }
        }
        var path = new List<Vector3>();
        if (dist[goal] == float.MaxValue) return new() { to }; // no way through the map: head straight there
        for (int n = goal; n >= 0; n = prev[n]) path.Add(_nodes[n]);
        path.Reverse();
        if (!goalIsNode) path.Add(to);
        return path;
    }

    /// <summary>The closest map point you can drive straight to from here (or simply the closest, if none is clear).</summary>
    private int Nearest(Vector3 p)
    {
        var near = Enumerable.Range(0, _nodes.Count).OrderBy(i => _nodes[i].DistanceSquaredTo(p)).Take(8).ToList();
        int clear = near.FirstOrDefault(i => _nodes[i].DistanceTo(p) < 80 && Clear(p, _nodes[i]), -1);
        return clear >= 0 ? clear : near[0];
    }

    /// <summary>Could the van drive straight from a to b? Three rays at bonnet height: the middle and either side.</summary>
    public bool Clear(Vector3 a, Vector3 b)
    {
        var up = a.Normalized();
        var along = b - a; along -= up * along.Dot(up);
        if (along.LengthSquared() < 0.01f) return true;
        var side = along.Normalized().Cross(up) * 1.1f;
        var lift = up * 1.0f;
        foreach (var off in new[] { Vector3.Zero, side, -side })
        {
            var q = PhysicsRayQueryParameters3D.Create(a + lift + off, b + b.Normalized() * 1.0f + off, 1, _ignore);
            if (_space.IntersectRay(q).Count > 0) return false;
        }
        return true;
    }

    private int Add(Vector3 p, bool hub = false)
    {
        _nodes.Add(p); _links.Add(new List<int>());
        if (hub) _hubs.Add(_nodes.Count - 1);
        return _nodes.Count - 1;
    }

    private void Link(int a, int b)
    {
        if (a == b || _links[a].Contains(b)) return;
        _links[a].Add(b); _links[b].Add(a);
    }

    private void LinkIfClear(int a, int b) { if (Clear(_nodes[a], _nodes[b])) Link(a, b); }
}
