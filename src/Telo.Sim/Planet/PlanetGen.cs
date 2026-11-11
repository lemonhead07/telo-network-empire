using Telo.Sim.Core;

namespace Telo.Sim.Planet;

public enum Biome { Seabed, Beach, Grassland, Forest, Desert, Tundra, Snow, Rock, Town }

/// <summary>A flattened patch of ground for buildings. Height follows the sphere (constant altitude).</summary>
public sealed record Site(string Id, V3 Dir, double RadiusM, double HeightM);

/// <summary>A graded route between two places. Terrain is cut and filled to the road's own smooth height profile.</summary>
public sealed record Road(string Id, IReadOnlyList<V3> Points, IReadOnlyList<double> Heights, double HalfWidthM);

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
    }

    public Site Town => _sites[0];
    public Site? SiteById(string id) => _sites.FirstOrDefault(s => s.Id == id);

    /// <summary>Metres above (or below) sea level, before any site flattening.</summary>
    public double RawHeight(V3 dir)
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

    private Road BuildRoad(string id, Site from, Site to)
    {
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
}
