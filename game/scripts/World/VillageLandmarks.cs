using Godot;
using Telo.Sim.Planet;

namespace Telo.Game.World;

/// <summary>
/// The buildings that make each village its own place: Wrenfold's school and green, Kettle Cross's pub and boatyard,
/// Highmoor's chapel and weather station, and a parade of shops on Eastgate High Street. Looks only (the models in tools/models/village.py) plus a plain box of
/// collision round each building; nothing here decides anything and no lot moves.
/// They stand behind one row of houses, on the side of the street the road from town does not come in on.
/// </summary>
public static class VillageLandmarks
{
    /// <summary>One landmark: its prop, how far east of the village centre, and its solid parts (centre, size) in model space.</summary>
    private sealed record Mark(string Site, string Prop, float East, (Vector3 C, Vector3 S)[] Solids, float? South = null);

    private static readonly Mark[] Marks =
    {
        new("wrenfold", "village.school", -17f, new[] { (new Vector3(0, 1.9f, 0), new Vector3(11f, 3.8f, 6.5f)) }),
        new("wrenfold", "village.green", 18f, new[] { (new Vector3(4f, 1.5f, -0.5f), new Vector3(0.9f, 3f, 0.9f)) }),
        new("kettle-cross", "village.pub", -17f, new[] { (new Vector3(0, 2.8f, 0), new Vector3(10f, 5.6f, 7f)) }),
        new("kettle-cross", "village.boatyard", 15f, new[] { (new Vector3(0, 1.5f, 0), new Vector3(6f, 3f, 8f)), (new Vector3(6f, 1.2f, 1.5f), new Vector3(2.2f, 2.4f, 6f)) }),
        new("highmoor", "village.chapel", -20f, new[] { (new Vector3(0, 2.1f, 0), new Vector3(6f, 4.2f, 10f)) }),
        new("highmoor", "village.weather-station", 20f, new[] { (new Vector3(-2f, 1.2f, -2.6f), new Vector3(3f, 2.4f, 2.4f)), (new Vector3(2.4f, 4.5f, -1.5f), new Vector3(0.8f, 9f, 0.8f)) }),
        // Eastgate's shops stand on the High Street itself, where the north row of houses runs out, facing the street
        new("eastgate", "village.parade", 34f, new[] { (new Vector3(0, 3f, 0), new Vector3(18f, 6f, 8f)) }, South: -9f),
    };

    /// <summary>Behind the north row (yaw 0, facing the street) or the south row (turned round).</summary>
    private const float NorthSouth = -37f, SouthSouth = 50f;

    /// <summary>Where each landmark stands, by prop id (for screenshots).</summary>
    public static readonly Dictionary<string, Node3D> Placed = new();

    public static void Build(Node3D root, PlanetGen gen)
    {
        Placed.Clear();
        foreach (var m in Marks)
        {
            if (gen.SiteById(m.Site) is not { } site) continue;
            bool north = m.South is not null || !RoadComesFromNorth(gen, site);
            float south = m.South ?? (north ? NorthSouth : SouthSouth);
            var node = new Node3D { Name = m.Prop.Replace('.', '_'), Transform = TownBuilder.Place(gen, site, m.East, south, north ? 0 : 180) };
            node.AddChild(Props.Make(m.Prop));
            var body = new StaticBody3D();
            foreach (var (c, s) in m.Solids)
                body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = s }, Position = c });
            node.AddChild(body);
            root.AddChild(node);
            Placed[m.Prop] = node;
        }
    }

    /// <summary>Does the road from town reach this village on its north side (south offset below zero)?</summary>
    private static bool RoadComesFromNorth(PlanetGen gen, Site site)
    {
        var road = gen.Roads.FirstOrDefault(r => r.Id == $"road-town-{site.Id}");
        if (road is null) return false;
        var c = PlanetBuilder.ToGodot(site.Dir).Normalized();
        var e = Vector3.Up.Cross(c).Normalized();
        var n = c.Cross(e);
        float R = (float)gen.RadiusM, edge = (float)site.RadiusM * 0.75f;
        // the first point of the road that is outside the street area, seen from the village centre
        foreach (var p in road.Points.Reverse().Concat(road.Points))
        {
            var d = (PlanetBuilder.ToGodot(p).Normalized() - c) * R;
            float east = d.Dot(e), south = -d.Dot(n);
            float r = Mathf.Sqrt(east * east + south * south);
            if (r > edge && r < edge + 100f) return south < 0 && Mathf.Abs(south) > Mathf.Abs(east) * 0.5f;
        }
        return false;
    }
}
