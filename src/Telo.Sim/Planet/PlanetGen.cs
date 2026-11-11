using Telo.Sim.Core;

namespace Telo.Sim.Planet;

public enum Biome { Seabed, Beach, Grassland, Forest, Desert, Tundra, Snow, Rock, Town }

/// <summary>A flattened patch of ground for buildings. Height follows the sphere (constant altitude).</summary>
public sealed record Site(string Id, V3 Dir, double RadiusM, double HeightM);

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

    public PlanetGen(ulong seed, double radiusM = DefaultRadiusM)
    {
        Seed = seed; RadiusM = radiusM;
        _s = (uint)(seed ^ (seed >> 32));
        _sites.Add(FindTownSite());
    }

    public Site Town => _sites[0];

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
