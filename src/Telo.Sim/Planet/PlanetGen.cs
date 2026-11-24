using Telo.Sim.Core;

namespace Telo.Sim.Planet;

public enum Biome { Seabed, Beach, Grassland, Forest, Desert, Tundra, Snow, Rock, Town }

/// <summary>A flattened patch of ground for buildings. Height follows the sphere (constant altitude).</summary>
public sealed record Site(string Id, V3 Dir, double RadiusM, double HeightM);

/// <summary>A graded route between two places. Terrain is cut and filled to the road's own smooth height profile.</summary>
public sealed record Road(string Id, IReadOnlyList<V3> Points, IReadOnlyList<double> Heights, double HalfWidthM);

/// <summary>
/// Land of its own, raised from the sea after everything else is placed (EA3b): deep water all round it, so the only way
/// there is by boat. Its id is the land's name in <see cref="PlanetGen.LandOf"/>; everywhere else is <see cref="PlanetGen.Mainland"/>.
/// </summary>
public sealed record Island(string Id, string Name, V3 Dir, double RadiusM);

/// <summary>A slipway on the shore where a ferry lands. A road runs down to it; <see cref="HeightM"/> is the top of the slip.</summary>
public sealed record Quay(string Id, string Name, string Land, V3 Dir, double HeightM);

/// <summary>A sea crossing between two quays on different land, sailed by a vehicle in <c>Act/Vehicles</c> (a ferry).</summary>
public sealed record Crossing(string Id, string Vehicle, Quay From, Quay To)
{
    /// <summary>The quay on this land, and the one across the water. Null when this crossing doesn't touch that land.</summary>
    public (Quay Here, Quay There)? Ends(string land) => From.Land == land ? (From, To) : To.Land == land ? (To, From) : null;
}

/// <summary>
/// The planet as a pure function: direction on the unit sphere → terrain height and biome.
/// The presentation layer meshes this; the sim uses it for coverage, routes and line-of-sight.
/// Small on purpose: 2.5 km radius (~16 km lap): about 5 minutes to fly around, 12–20 to drive, and ~78 km² to build on.
/// </summary>
public sealed class PlanetGen
{
    public const double DefaultRadiusM = 2500;

    public ulong Seed { get; }
    public double RadiusM { get; }
    public IReadOnlyList<Site> Sites => _sites;
    private readonly List<Site> _sites = new();
    private readonly uint _s;

    public IReadOnlyList<Road> Roads => _roads;
    private readonly List<Road> _roads = new();
    public const double RoadSpacingM = 20, RoadShoulderM = 38;

    /// <summary>Land raised from the sea (EA3b). Empty while the settlements are found, so adding one moves nothing else.</summary>
    public IReadOnlyList<Island> Islands => _islands;
    private readonly List<Island> _islands = new();
    public IReadOnlyList<Quay> Quays => _quays;
    private readonly List<Quay> _quays = new();
    public IReadOnlyList<Crossing> Crossings => _crossings;
    private readonly List<Crossing> _crossings = new();
    /// <summary>The land everything stands on unless it is on an island.</summary>
    public const string Mainland = "mainland";

    public PlanetGen(ulong seed, double radiusM = DefaultRadiusM)
    {
        Seed = seed; RadiusM = radiusM;
        _s = (uint)(seed ^ (seed >> 32));
        _sites.Add(FindTownSite());
        if (FindFarmSite() is { } farm)
        {
            _sites.Add(farm);
            _roads.Add(BuildRoad("road-town-farm", Town, farm));
        }
        // Eastgate: the next village down the valley (after the farm road, so the farm and its road are unchanged)
        if (FindVillageSite(_sites.FirstOrDefault(s => s.Id == "farm-hollow")) is { } village)
        {
            _sites.Add(village);
            _roads.Add(BuildRoad("road-town-eastgate", Town, village));
        }
        // EA2f: two more villages for hand work, opened by projects in Acts 1 and 2 (added last so the others are unchanged)
        foreach (var (id, near, far, apart, steep) in new[] { ("wrenfold", 520, 820, 0.6, 9.0), ("kettle-cross", 450, 1200, 0.92, 20.0), ("highmoor", 550, 1400, 0.95, 22.0) })
            if (FindVillageSite(null, id, near, far, apart, steep) is { } more)
            {
                _sites.Add(more);
                _roads.Add(BuildRoad($"road-town-{id}", Town, more));
            }
        // DC1: Halden Flats, the plain between the valley, Eastgate and the shore, where the data centre goes (added last)
        if (FindFlatsSite() is { } flats)
        {
            _sites.Add(flats);
            _roads.Add(BuildRoad("road-town-halden", Town, flats));
        }
        // EA3a: Millbrook, the market town over the hill past the farm (Act 2), reached by driving on from the farm. Added
        // last, so every place and road before it is unchanged.
        if (SiteById("farm-hollow") is { } hollow && FindMillbrookSite(hollow) is { } millbrook)
        {
            _sites.Add(millbrook);
            _roads.Add(BuildRoad("road-farm-millbrook", hollow, millbrook));
        }
        // EA3b: Farland, across the water from Kestrel Point, and the car ferry over (added last, so nothing above moves)
        AddFarland();
    }

    // ---- Farland (EA3b) --------------------------------------------------------------------------------
    /// <summary>Farland's land: this far across (m, to its shoreline) and this much open water kept all round it.</summary>
    public const double FarlandRadiusM = 420, FarlandMoatM = 220;

    /// <summary>
    /// Farland, "the far continent's near coast" (AGES.md Act 3): land of its own across the sea from Kestrel Point, the
    /// headland below Eastgate. The planet's noise makes one big continent round inland seas, so Farland is raised out of the
    /// open water, with deep sea all round it (no dry way there), and a harbour town on its near shore. A slipway on each
    /// side and a car ferry between them; a short road down to each slipway. Deterministic for a seed; nothing if the sea
    /// off Eastgate has no room for it.
    /// </summary>
    private void AddFarland()
    {
        if (SiteById("eastgate") is not { } eastgate || FindHomeQuay(eastgate) is not { } home) return;
        if (FindFarlandIsland(eastgate, home) is not { } island) return;
        _islands.Add(island);   // from here on RawHeight includes it
        if (FindFarlandSite(island, home) is not { } site) { _islands.Clear(); return; }
        _sites.Add(site);
        if (Shoreline(site.Dir, home.Dir, site.RadiusM) is not { } far) { _sites.Remove(site); _islands.Clear(); return; }
        var farQuay = new Quay("quay-farland", "Farland harbour", island.Id, far.Dir, far.HeightM);
        var homeQuay = home with { HeightM = Math.Max(QuayHeightM, Height(home.Dir)) };
        _quays.Add(homeQuay); _quays.Add(farQuay);
        _crossings.Add(new Crossing("ferry-farland", "ferry", homeQuay, farQuay));
        _roads.Add(BuildRoad("road-eastgate-quay", eastgate, QuaySite(homeQuay), even: true));
        _roads.Add(BuildRoad("road-farland-quay", site, QuaySite(farQuay), even: true));
    }

    /// <summary>The top of a slipway stands this far above the sea.</summary>
    public const double QuayHeightM = 1.4;

    private static Site QuaySite(Quay q) => new(q.Id, q.Dir, 6, q.HeightM);

    /// <summary>
    /// Kestrel Point, the headland below Eastgate facing the open sea, where projects.json stands the cable landing station
    /// (62 m south of Eastgate's centre) and the home end of the Farland cable beside it. The slipway road keeps clear of both.
    /// </summary>
    private static readonly (double East, double South)[] KestrelPoint = { (0, 62), (-20, 60) };
    /// <summary>The back fences of the High Street's south side, metres south of Eastgate's centre (houses at 24, gardens 14 deep).</summary>
    private const double EastgateBackFences = 38;

    /// <summary>
    /// The nearest stretch of open shore below Eastgate: the waterline, with real water (not a creek) beyond it, clear of every
    /// other settlement and road. The road down to it leaves Eastgate southwards, behind the High Street's back gardens and
    /// clear of Kestrel Point's buildings.
    /// </summary>
    private Quay? FindHomeQuay(Site eastgate)
    {
        var rng = new DetRng(DetRng.Derive(Seed, "quay-site"));
        var (c, east, north) = Frame(eastgate.Dir);
        V3 At(double e, double s) => (c + (east * e - north * s) * (1 / RadiusM)).Normalized();
        var keepClear = KestrelPoint.Select(k => At(k.East, k.South)).ToArray();
        V3? best = null; double bestScore = double.MinValue;
        for (int i = 0; i < 400; i++)
        {
            double dx = rng.Next(2001) / 1000.0 - 1, dz = rng.Next(2001) / 1000.0 - 1;
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len is < 0.2 or > 1) continue;
            double he = dx / len, hs = -dz / len;   // heading east and south
            if (hs < 0.5) continue;                 // the sea is south of the High Street
            // walk out past the levelled ground to the waterline
            double dist = -1;
            for (double m = eastgate.RadiusM * 2; m < 600; m += 4)
                if (Height(At(he * m, hs * m)) < 0.6) { dist = m - 4; break; }
            if (dist < 0) continue;
            var q = At(he * dist, hs * dist);
            if (Height(q) is < 0.2 or > 2.5) continue;
            if (Height(At(he * (dist + 40), hs * (dist + 40))) > -0.8) continue;     // water beyond it...
            if (Height(At(he * (dist + 140), hs * (dist + 140))) > -2.5) continue;   // ...and open sea, not a creek
            if (Clashes(q, 0, eastgate)) continue;
            // the road down: drawn from where the street ends (0.75 of the site's radius), behind the gardens, clear of the headland,
            // and once off Eastgate's own ground, clear of every other road
            bool clash = false;
            for (double m = eastgate.RadiusM * 0.75; m <= dist && !clash; m += 6)
            {
                var p = At(he * m, hs * m);
                if (m < eastgate.RadiusM * 1.2 && hs * m - 9 * Math.Abs(he) < EastgateBackFences + 2) clash = true;
                foreach (var k in keepClear) if ((p - k).Length * RadiusM < 9 + 20) clash = true;
                if (m > eastgate.RadiusM * 2 && Clashes(p, 0, eastgate, roadsOnly: true)) clash = true;
            }
            if (clash) continue;
            double score = -dist;   // as close as the shore comes
            if (score > bestScore) { bestScore = score; best = q; }
        }
        return best is { } b ? new Quay("quay-kestrel", "Kestrel Point", Mainland, b, Height(b)) : null;
    }

    /// <summary>Within reach of another settlement's flattened ground or a road (beyond <paramref name="marginM"/>)?</summary>
    private bool Clashes(V3 p, double marginM, Site except, bool roadsOnly = false)
    {
        if (!roadsOnly)
            foreach (var s in _sites)
                if (s != except && (p - s.Dir).Length * RadiusM < s.RadiusM * 2 + marginM) return true;
        foreach (var road in _roads)
            if (NearestOnRoad(road, p).Dist < road.HalfWidthM + RoadShoulderM + 10 + marginM) return true;
        return false;
    }

    /// <summary>
    /// Open sea off the home slipway, a ferry ride out: Farland's middle, with water (before it rises) everywhere within its
    /// radius and a moat round it. Preferring a crossing of six or seven hundred metres, straight out.
    /// </summary>
    private Island? FindFarlandIsland(Site eastgate, Quay home)
    {
        var rng = new DetRng(DetRng.Derive(Seed, "farland-site"));
        var (_, east, north) = Frame(home.Dir);
        var (ec, _, _) = Frame(eastgate.Dir);
        var out0 = home.Dir - ec; out0 = (out0 - home.Dir * out0.Dot(home.Dir)).Normalized();   // seaward, from Eastgate
        V3? best = null; double bestScore = double.MinValue;
        for (int i = 0; i < 1500; i++)
        {
            double dx = rng.Next(2001) / 1000.0 - 1, dz = rng.Next(2001) / 1000.0 - 1;
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len is < 0.2 or > 1) continue;
            var heading = (east * dx + north * dz) * (1 / len);
            double ahead = heading.Dot(out0);
            if (ahead < 0.55) continue;                                   // out to sea, not along the coast
            double crossing = 450 + rng.Next(700);                        // quay to Farland's shore
            double d = crossing + FarlandRadiusM;
            var c = (home.Dir + heading * (d / RadiusM)).Normalized();
            if (RawHeight(c) > -12) continue;
            bool dry = false;
            var (cc, ce, cn) = Frame(c);
            for (double r = 60; r <= FarlandRadiusM + FarlandMoatM && !dry; r += 55)
                foreach (var (rx, rz) in Ring16)
                    if (RawHeight((cc + (ce * rx + cn * rz) * (r / RadiusM)).Normalized()) > -1.5) { dry = true; break; }
            if (dry) continue;
            double score = -Math.Abs(crossing - 650) / 50 + ahead * 4;
            if (score > bestScore) { bestScore = score; best = c; }
        }
        return best is { } b ? new Island("farland", "Farland", b, FarlandRadiusM) : null;
    }

    /// <summary>
    /// The harbour town: low, gentle ground on Farland's near shore, facing the home slipway, set back far enough that its
    /// levelled ground ends before the beach (so the harbour slipway sits at the water, not on a bank).
    /// </summary>
    private Site? FindFarlandSite(Island island, Quay home)
    {
        var (c, _, _) = Frame(island.Dir);
        var toHome = home.Dir - c; toHome = (toHome - c * toHome.Dot(c)).Normalized();
        var side = c.Cross(toHome);
        V3? best = null; double bestScore = double.MinValue;
        for (int turn = -6; turn <= 6; turn++)
            for (int step = 0; step <= 24; step++)
            {
                double f = 0.3 + step * 0.025;
                var heading = (toHome + side * (turn * 0.06)).Normalized();
                var p = (c + heading * (f * island.RadiusM / RadiusM)).Normalized();
                double h = RawHeight(p);
                if (h is < 3.5 or > 14) continue;
                // the beach towards home: past where the town's levelling fades (twice its radius), not much further
                var dir = (home.Dir - p); dir = (dir - p * dir.Dot(p)).Normalized();
                double shore = -1;
                for (double m = 60; m < 320; m += 5)
                    if (RawHeight((p + dir * (m / RadiusM)).Normalized()) < 0.5) { shore = m; break; }
                if (shore is < 150 or > 215) continue;
                var (pc, pe, pn) = Frame(p);
                double low = double.MaxValue, rough = 0;
                foreach (var (rx, rz) in Ring)
                    foreach (double m in new[] { 35.0, 70.0 })
                    {
                        double hk = RawHeight((pc + (pe * rx + pn * rz) * (m / RadiusM)).Normalized());
                        low = Math.Min(low, hk); rough = Math.Max(rough, Math.Abs(hk - h));
                    }
                if (low < 2.5) continue;   // the whole street on dry land before it's levelled
                double score = -rough * 2 - Math.Abs(h - 7) / 2 - Math.Abs(turn) * 0.3 - shore / 40;   // gentle, low, facing home, near the water
                if (score > bestScore) { bestScore = score; best = p; }
            }
        return best is { } b ? new Site(island.Id, b, 70, RawHeight(b)) : null;
    }

    /// <summary>Where the ground meets the sea going from <paramref name="from"/> towards <paramref name="towards"/>, past the levelled ground.</summary>
    private (V3 Dir, double HeightM)? Shoreline(V3 from, V3 towards, double startM)
    {
        var dir = towards - from; dir = (dir - from * dir.Dot(from)).Normalized();
        for (double m = startM; m < startM + 400; m += 4)
            if (Height((from + dir * (m / RadiusM)).Normalized()) < 0.6)
            {
                var q = (from + dir * ((m - 6) / RadiusM)).Normalized();   // a few metres back up the beach
                return (q, Math.Max(QuayHeightM, Height(q)));
            }
        return null;
    }

    /// <summary>The land a point stands on: an island's id, or <see cref="Mainland"/>.</summary>
    public string LandOf(V3 dir)
    {
        var p = dir.Normalized();
        foreach (var i in _islands) if ((p - i.Dir).Length * RadiusM < i.RadiusM * 1.45) return i.Id;
        return Mainland;
    }

    /// <summary>A land's name for people: "Farland", or "the mainland".</summary>
    public string LandName(string land) => _islands.FirstOrDefault(i => i.Id == land)?.Name ?? "the mainland";

    /// <summary>The crossing between two lands, if a ferry runs between them.</summary>
    public Crossing? CrossingBetween(string a, string b) =>
        _crossings.FirstOrDefault(x => (x.From.Land == a && x.To.Land == b) || (x.From.Land == b && x.To.Land == a));

    /// <summary>How high an island stands at a point (very low far from it).</summary>
    private double IslandHeight(Island isl, V3 p)
    {
        double d = (p - isl.Dir).Length * RadiusM;
        if (d > isl.RadiusM * 1.8) return double.MinValue;
        double wobble = 1 + 0.16 * Noise.Fbm(p * 9.0, _s + 707, 3);              // a ragged coast, coves and points
        double x = d / (isl.RadiusM * wobble);                                     // 0 in the middle, 1 at the waterline
        if (x >= 1) return -1 - (x - 1) * 70;                                      // the shelf drops away into the deep
        // a low coastal plain (a few metres up, for the harbour town), then hills inland
        double u = 1 - x;
        double hills = Noise.Fbm(p * 13.0, _s + 717, 4) * 16 * Noise.Smooth(0.45, 0.75, u);
        return -1 + 9 * Noise.Smooth(0, 0.45, u) + 26 * Noise.Smooth(0.45, 0.95, u) + hills;
    }

    private static (V3 C, V3 East, V3 North) Frame(V3 dir)
    {
        var c = dir.Normalized();
        var east = new V3(0, 1, 0).Cross(c).Normalized();
        return (c, east, c.Cross(east));
    }

    /// <summary>
    /// Millbrook: on past the farm, on the side away from town, over a hill but not across the sea. Gentle ground for a
    /// street, well clear of every village and the flats, and the road from the farm keeps off their ground and their roads.
    /// Deterministic for a seed.
    /// </summary>
    private Site? FindMillbrookSite(Site farm)
    {
        const double radius = 70;
        var rng = new DetRng(DetRng.Derive(Seed, "millbrook-site"));
        var f = farm.Dir;
        var east = new V3(0, 1, 0).Cross(f).Normalized();
        var north = f.Cross(east);
        var onward = (f - Town.Dir).Normalized();
        V3? best = null; double bestScore = double.MinValue;
        for (int i = 0; i < 1500; i++)
        {
            double dx = rng.Next(2001) / 1000.0 - 1, dz = rng.Next(2001) / 1000.0 - 1;
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len is < 0.2 or > 1) continue;
            double dist = 450 + rng.Next(350);
            var c = (f + (east * (dx / len) + north * (dz / len)) * (dist / RadiusM)).Normalized();
            if (onward.Dot((c - f).Normalized()) < 0.2) continue;                           // on past the farm, not back
            if ((c - Town.Dir).Length * RadiusM < 1100) continue;
            double h = RawHeight(c);
            if (h is < 4 or > 50) continue;
            if (!ClearOfOthers(c, f, farm, radius)) continue;
            double worst = 0, low = double.MaxValue, high = double.MinValue, prev = farm.HeightM, rough = 0;
            int steps = (int)(dist / 25);
            for (int k = 1; k <= steps; k++)
            {
                double hk = RawHeight((f + (c - f) * ((double)k / steps)).Normalized());
                low = Math.Min(low, hk); high = Math.Max(high, hk);
                worst = Math.Max(worst, Math.Abs(hk - prev));
                prev = hk;
            }
            foreach (var (rx, rz) in Ring) rough = Math.Max(rough, Math.Abs(RawHeight((c + east * (rx * 60 / RadiusM) + north * (rz * 60 / RadiusM)).Normalized()) - h));
            if (low < 3 || worst > 10) continue;
            // a hill between (that is the point of the relay), a flat town at the end
            double hill = Math.Min(20, high - Math.Max(farm.HeightM, h));
            double score = -worst * 3 - rough * 2 + hill / 4 - Math.Abs(h - 15) / 10;
            if (score > bestScore) { bestScore = score; best = c; }
        }
        return best is { } b ? new Site("millbrook", b, radius, RawHeight(b)) : null;
    }

    /// <summary>
    /// A far town at <paramref name="c"/>, with its road from <paramref name="from"/>: its flattened ground and the road's
    /// shoulders touch no other settlement's ground and no other road (bar the one into <paramref name="home"/>, near it).
    /// </summary>
    private bool ClearOfOthers(V3 c, V3 from, Site home, double radius)
    {
        foreach (var o in _sites)
            if (o != home && (c - o.Dir).Length * RadiusM < Math.Max(600, o.RadiusM * 2 + radius * 2 + 100)) return false;
        foreach (var road in _roads)
            if (NearestOnRoad(road, c).Dist < radius * 2 + 9 + RoadShoulderM + 30) return false;
        int n = Math.Max(2, (int)((c - from).Length * RadiusM / RoadSpacingM));
        for (int k = 1; k < n; k++)
        {
            var p = (from + (c - from) * ((double)k / n)).Normalized();
            if ((p - home.Dir).Length * RadiusM < home.RadiusM * 2 + 60) continue;      // leaving home, where the roads meet
            foreach (var o in _sites)
                if (o != home && (p - o.Dir).Length * RadiusM < o.RadiusM * 2 + 9 + RoadShoulderM) return false;
            foreach (var road in _roads)
                if (NearestOnRoad(road, p).Dist < 2 * (9 + RoadShoulderM)) return false;
        }
        return true;
    }

    /// <summary>
    /// Halden Flats (DATA_CENTRE_DESIGN.md §2): wide, flat and boring on purpose, a short drive from town on the Eastgate side,
    /// off every existing road and clear of every village, with an overland route home. Deterministic for a seed.
    /// </summary>
    private Site? FindFlatsSite()
    {
        if (SiteById("eastgate") is not { } eastgate) return null;
        var rng = new DetRng(DetRng.Derive(Seed, "halden-site"));
        var town = Town.Dir;
        var east = new V3(0, 1, 0).Cross(town).Normalized();
        var north = town.Cross(east);
        // aim halfway down the Eastgate road; the nearest clear, flat ground off it wins
        var aim = (town + eastgate.Dir).Normalized();
        V3? best = null; double bestScore = double.MinValue;
        for (int i = 0; i < 2000; i++)
        {
            double dx = rng.Next(2001) / 1000.0 - 1, dz = rng.Next(2001) / 1000.0 - 1;
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len > 1) continue;
            var c = (aim + (east * dx + north * dz) * (600 / RadiusM)).Normalized();
            double dist = (c - town).Length * RadiusM;
            if (dist is < 300 or > 900) continue;
            double h = RawHeight(c);
            if (h is < 4 or > 40) continue;
            bool clash = false;
            // its flat ground stays off every road and every settlement's flat ground (the slopes between may share)
            foreach (var o in _sites) if ((c - o.Dir).Length * RadiusM < o.RadiusM + 80 + 70) clash = true;
            foreach (var road in _roads)
            {
                // off the road, and where its slope reaches a road, about the road's height so the road doesn't dip
                var (rd, rh) = NearestOnRoad(road, c);
                if (rd < 115 || (rd < 160 && Math.Abs(rh - h) > 2.5)) clash = true;
            }
            if (clash) continue;
            double worst = 0, low = double.MaxValue, prev = Town.HeightM, rough = 0;
            int steps = (int)(dist / 25);
            for (int k = 1; k <= steps; k++)
            {
                double hk = RawHeight((town + (c - town) * ((double)k / steps)).Normalized());
                low = Math.Min(low, hk);
                worst = Math.Max(worst, Math.Abs(hk - prev));
                prev = hk;
            }
            if (low < 3 || worst > 12) continue;
            foreach (var (rx, rz) in Ring)
                foreach (double m in new[] { 50.0, 100.0 })
                    rough = Math.Max(rough, Math.Abs(RawHeight((c + east * (rx * m / RadiusM) + north * (rz * m / RadiusM)).Normalized()) - h));
            double score = -rough * 3 - worst - (c - aim).Length * RadiusM / 25 - Math.Abs(h - 15) / 4;
            if (score > bestScore) { bestScore = score; best = c; }
        }
        return best is { } b ? new Site("halden", b, 80, RawHeight(b)) : null;
    }

    /// <summary>
    /// A village a short drive from town, away from the farm road: gentle dry ground with an overland route back that
    /// crosses no sea and no cliff. Deterministic for a seed.
    /// </summary>
    private Site? FindVillageSite(Site? avoid, string id = "eastgate", int near = 480, int far = 780, double apart = 0.6, double steep = 9)
    {
        var rng = new DetRng(DetRng.Derive(Seed, $"{id}-site"));
        var others = _sites.Skip(1).ToList();   // later villages keep clear of every settlement and road out of town
        var town = Town.Dir;
        var east = new V3(0, 1, 0).Cross(town).Normalized();
        var north = town.Cross(east);
        V3? best = null; double bestScore = double.MinValue;
        for (int i = 0; i < 1500; i++)
        {
            double dx = rng.Next(2001) / 1000.0 - 1, dz = rng.Next(2001) / 1000.0 - 1;
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len is < 0.2 or > 1) continue;
            double dist = near + rng.Next(far - near);
            var heading = east * (dx / len) + north * (dz / len);
            var c = (town + heading * (dist / RadiusM)).Normalized();
            double h = RawHeight(c);
            if (h is < 4 or > 45) continue;
            if (avoid is not null)
            {
                // well away from the farm, and not out along the farm road
                if ((c - avoid.Dir).Length * RadiusM < 500) continue;
                if ((avoid.Dir - town).Normalized().Dot((c - town).Normalized()) > 0.5) continue;
            }
            else if (id != "eastgate")
            {
                bool clash = false;
                foreach (var o in others)
                    if ((c - o.Dir).Length * RadiusM < 420 || (o.Dir - town).Normalized().Dot((c - town).Normalized()) > apart) clash = true;
                if (clash) continue;
            }
            double worst = 0, low = double.MaxValue, prev = Town.HeightM, rough = 0;
            int steps = (int)(dist / 25);
            for (int k = 1; k <= steps; k++)
            {
                double hk = RawHeight((town + (c - town) * ((double)k / steps)).Normalized());
                low = Math.Min(low, hk);
                worst = Math.Max(worst, Math.Abs(hk - prev));
                prev = hk;
            }
            foreach (var (rx, rz) in Ring) rough = Math.Max(rough, Math.Abs(RawHeight((c + east * (rx * 60 / RadiusM) + north * (rz * 60 / RadiusM)).Normalized()) - h));
            if (low < 3 || worst > steep) continue;
            double score = -worst * 3 - rough * 2 - Math.Abs(h - 15) / 10;
            if (score > bestScore) { bestScore = score; best = c; }
        }
        return best is { } b ? new Site(id, b, 70, RawHeight(b)) : null;
    }

    public Site Town => _sites[0];
    public Site? SiteById(string id) => _sites.FirstOrDefault(s => s.Id == id);

    /// <summary>Metres above (or below) sea level, before any site flattening. Islands (EA3b) rise out of the sea.</summary>
    public double RawHeight(V3 dir)
    {
        double h = NoiseHeight(dir);
        if (_islands.Count > 0)
        {
            var p = dir.Normalized();
            foreach (var isl in _islands) h = Math.Max(h, IslandHeight(isl, p));
        }
        return h;
    }

    private double NoiseHeight(V3 dir)
    {
        var p = dir.Normalized();
        double continent = Noise.Fbm(p * 1.25, _s, 5) * 1.9 + 0.08;
        if (continent < 0) return Math.Max(-80, continent * 70 - 1);

        double shore = Noise.Smooth(0, 0.09, continent);
        double hills = Noise.Fbm(p * 13.0, _s + 101, 5) * 18 * shore;
        double range = Noise.Smooth(0.42, 0.72, Noise.Fbm(p * 2.3, _s + 202, 3) * 0.9 + 0.5) * Noise.Smooth(0.08, 0.35, continent);
        double mountains = Noise.Ridged(p * 5.0, _s + 303, 5);
        mountains = mountains * mountains * 260 * range;
        return 0.6 + continent * 42 * shore + hills + mountains;
    }

    /// <summary>Terrain height including flattened building sites.</summary>
    public double Height(V3 dir)
    {
        var p = dir.Normalized();
        double h = RawHeight(p);
        foreach (var road in _roads)
        {
            var (d, rh) = NearestOnRoad(road, p);
            if (d < road.HalfWidthM + RoadShoulderM) h = rh + (h - rh) * Noise.Smooth(road.HalfWidthM, road.HalfWidthM + RoadShoulderM, d);
        }
        foreach (var s in _sites)
        {
            double d = (p - s.Dir).Length * RadiusM;
            if (d < s.RadiusM * 2) h = s.HeightM + (h - s.HeightM) * Noise.Smooth(s.RadiusM, s.RadiusM * 2, d);
        }
        return h;
    }

    public Biome BiomeAt(V3 dir)
    {
        var p = dir.Normalized();
        foreach (var s in _sites)
            if ((p - s.Dir).Length * RadiusM < s.RadiusM) return Biome.Town;
        double h = Height(p);
        if (h < 0) return Biome.Seabed;
        if (h < 2.6) return Biome.Beach;
        double temp = 1 - Math.Abs(p.Y) * 1.15 - h / 330 + Noise.Perlin(p * 3.0, _s + 404) * 0.08;
        if (temp < 0.08) return Biome.Snow;
        if (h > 150) return Biome.Rock;
        if (temp < 0.24) return Biome.Tundra;
        double moisture = Noise.Fbm(p * 2.6, _s + 505, 3);
        if (moisture < -0.1 && temp > 0.55) return Biome.Desert;
        return moisture > 0.06 ? Biome.Forest : Biome.Grassland;
    }

    /// <summary>True on a road or inside a settlement: ground a van can use at full speed.</summary>
    public bool IsPaved(V3 dir)
    {
        var p = dir.Normalized();
        foreach (var s in _sites) if ((p - s.Dir).Length * RadiusM < s.RadiusM) return true;
        foreach (var road in _roads) if (NearestOnRoad(road, p).Dist < road.HalfWidthM) return true;
        return false;
    }

    /// <summary>Distance (m) from a point to the road's centreline, and the road height there.</summary>
    private (double Dist, double Height) NearestOnRoad(Road road, V3 p)
    {
        var pts = road.Points;
        // cheap reject using the road's midpoint
        double reach = (pts[0] - pts[^1]).Length * RadiusM / 2 + road.HalfWidthM + RoadShoulderM + 60;
        if ((p - pts[pts.Count / 2]).Length * RadiusM > reach) return (double.MaxValue, 0);
        int best = 0; double bestD = double.MaxValue;
        for (int i = 0; i < pts.Count; i++)
        {
            double d = (p - pts[i]).Length;
            if (d < bestD) { bestD = d; best = i; }
        }
        // refine along the better neighbouring segment so height changes smoothly between samples
        int other = best == 0 ? 1 : best == pts.Count - 1 ? best - 1
            : (p - pts[best - 1]).Length < (p - pts[best + 1]).Length ? best - 1 : best + 1;
        var a = pts[best]; var ab = pts[other] - a;
        double t = Math.Clamp((p - a).Dot(ab) / Math.Max(1e-12, ab.Dot(ab)), 0, 1);
        var q = a + ab * t;
        return ((p - q).Length * RadiusM, road.Heights[best] + (road.Heights[other] - road.Heights[best]) * t);
    }

    private double SiteAwareRaw(V3 p)
    {
        double h = RawHeight(p);
        foreach (var s in _sites)
        {
            double d = (p - s.Dir).Length * RadiusM;
            if (d < s.RadiusM * 2) h = s.HeightM + (h - s.HeightM) * Noise.Smooth(s.RadiusM, s.RadiusM * 2, d);
        }
        return h;
    }

    private Road BuildRoad(string id, Site from, Site to, bool even = false)
    {
        if (even)
        {
            // a short road down to the water: one steady grade from end to end, cut and filled into the slope
            int m = Math.Max(2, (int)((from.Dir - to.Dir).Length * RadiusM / RoadSpacingM));
            var p = new List<V3>(); var hs = new List<double>();
            for (int i = 0; i <= m; i++)
            {
                double t = (double)i / m;
                p.Add((from.Dir + (to.Dir - from.Dir) * t).Normalized());
                hs.Add(from.HeightM + (to.HeightM - from.HeightM) * t);
            }
            return new Road(id, p, hs, 9);
        }
        int n = Math.Max(2, (int)((from.Dir - to.Dir).Length * RadiusM / RoadSpacingM));
        var pts = new List<V3>(); var raw = new List<double>();
        for (int i = 0; i <= n; i++)
        {
            var p = (from.Dir + (to.Dir - from.Dir) * ((double)i / n)).Normalized();
            pts.Add(p); raw.Add(Math.Max(1.5, SiteAwareRaw(p)));
        }
        // grade it: a moving average takes the bumps out; the ends stay pinned to their sites
        var heights = new List<double>();
        const int w = 6;
        for (int i = 0; i <= n; i++)
        {
            double sum = 0; int cnt = 0;
            for (int k = Math.Max(0, i - w); k <= Math.Min(n, i + w); k++) { sum += raw[k]; cnt++; }
            heights.Add(sum / cnt);
        }
        heights[0] = from.HeightM; heights[n] = to.HeightM;
        return new Road(id, pts, heights, 9);
    }

    /// <summary>
    /// A farm a good drive from town: dry ground, with an overland line back to town that crosses no sea and no mountain.
    /// </summary>
    private Site? FindFarmSite()
    {
        var rng = new DetRng(DetRng.Derive(Seed, "farm-site"));
        var town = Town.Dir;
        var east = new V3(0, 1, 0).Cross(town).Normalized();
        var north = town.Cross(east);
        V3? best = null; double bestScore = double.MinValue;
        for (int i = 0; i < 1500; i++)
        {
            double dx = rng.Next(2001) / 1000.0 - 1, dz = rng.Next(2001) / 1000.0 - 1;
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len is < 0.2 or > 1) continue;
            double dist = 900 + rng.Next(700); // metres from town
            var c = (town + (east * (dx / len) + north * (dz / len)) * (dist / RadiusM)).Normalized();
            double h = RawHeight(c);
            if (h is < 5 or > 60) continue;

            double worst = 0, low = double.MaxValue, prev = Town.HeightM;
            int steps = (int)(dist / 25);
            for (int k = 1; k <= steps; k++)
            {
                double hk = RawHeight((town + (c - town) * ((double)k / steps)).Normalized());
                low = Math.Min(low, hk);
                worst = Math.Max(worst, Math.Abs(hk - prev));
                prev = hk;
            }
            if (low < 3 || worst > 9) continue;       // no sea crossings, no cliffs
            double score = -worst * 3 + dist / 200 - Math.Abs(h - 25) / 10;
            if (score > bestScore) { bestScore = score; best = c; }
        }
        return best is { } b ? new Site("farm-hollow", b, 55, RawHeight(b)) : null;
    }

    /// <summary>Great-circle distance in metres along sea level.</summary>
    public double SurfaceDistance(V3 a, V3 b)
    {
        double chord = (a.Normalized() - b.Normalized()).Length;
        // 2·asin(chord/2) without trig: fine approximation for gameplay distances (error < 5% up to a quarter lap).
        double half = chord / 2;
        return 2 * RadiusM * (half + half * half * half / 6 + 3 * half * half * half * half * half / 40);
    }

    /// <summary>
    /// Pick the starting town: temperate, gentle coastal grassland. Deterministic for a seed.
    /// </summary>
    private Site FindTownSite()
    {
        var rng = new DetRng(DetRng.Derive(Seed, "town-site"));
        V3 best = new(0.3, 0.4, 0.86); double bestScore = double.MinValue;
        for (int i = 0; i < 6000; i++)
        {
            var c = new V3(rng.Next(20001) / 10000.0 - 1, rng.Next(20001) / 10000.0 - 1, rng.Next(20001) / 10000.0 - 1);
            if (c.Length is > 1 or < 0.1) continue;
            c = c.Normalized();
            double lat = Math.Abs(c.Y);
            if (lat is < 0.15 or > 0.55) continue;
            double h = RawHeight(c);
            if (h is < 6 or > 22) continue;

            // local tangent frame
            var east = new V3(0, 1, 0).Cross(c).Normalized();
            var north = c.Cross(east);
            double near = 110 / RadiusM, far = 600 / RadiusM;
            double rough = 0; bool sea = false;
            for (int k = 0; k < 8; k++)
            {
                var (dx, dz) = Ring[k];
                rough = Math.Max(rough, Math.Abs(RawHeight((c + east * (dx * near) + north * (dz * near)).Normalized()) - h));
                if (RawHeight((c + east * (dx * far) + north * (dz * far)).Normalized()) < 0) sea = true;
            }
            double score = -rough * 2 + (sea ? 15 : 0) - Math.Abs(lat - 0.35) * 10;
            if (score > bestScore) { bestScore = score; best = c; }
        }
        return new Site("town-start", best, 130, Math.Max(4, RawHeight(best)));
    }

    private static readonly (double, double)[] Ring =
        { (1, 0), (-1, 0), (0, 1), (0, -1), (0.7, 0.7), (-0.7, 0.7), (0.7, -0.7), (-0.7, -0.7) };

    /// <summary>Sixteen points of the compass (unit vectors, without trig).</summary>
    private static readonly (double, double)[] Ring16 = Compass16();

    private static (double, double)[] Compass16()
    {
        const double a = 0.92387953251128674, b = 0.38268343236508978, h = 0.70710678118654752;   // cos and sin of 22.5 and 45 degrees
        var q = new[] { (1.0, 0.0), (a, b), (h, h), (b, a) };
        return q.Concat(q.Select(v => (-v.Item2, v.Item1))).Concat(q.Select(v => (-v.Item1, -v.Item2))).Concat(q.Select(v => (v.Item2, -v.Item1))).ToArray();
    }
}
