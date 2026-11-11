using Telo.Sim.Planet;

namespace Telo.Sim.Tests;

public class PlanetTests
{
    private static IEnumerable<V3> Samples(int n)
    {
        var rng = new Core.DetRng(1);
        while (n > 0)
        {
            var v = new V3(rng.Next(2001) / 1000.0 - 1, rng.Next(2001) / 1000.0 - 1, rng.Next(2001) / 1000.0 - 1);
            if (v.Length is > 1 or < 0.1) continue;
            n--; yield return v.Normalized();
        }
    }

    [Fact]
    public void SameSeedSamePlanet()
    {
        var a = new PlanetGen(7); var b = new PlanetGen(7);
        Assert.Equal(a.Town.Dir, b.Town.Dir);
        foreach (var p in Samples(200)) Assert.Equal(a.Height(p), b.Height(p));
    }

    [Theory] [InlineData(1UL)] [InlineData(42UL)] [InlineData(2026UL)]
    public void HasBothOceansAndLand_AndATownOnDryFlatGround(ulong seed)
    {
        var g = new PlanetGen(seed);
        double ocean = Samples(3000).Count(p => g.Height(p) < 0) / 3000.0;
        Assert.InRange(ocean, 0.25, 0.75);
        Assert.True(g.Town.HeightM >= 4);
        Assert.Equal(Biome.Town, g.BiomeAt(g.Town.Dir));
        // flattened: a point 40 m from the centre sits at town height
        var east = new V3(0, 1, 0).Cross(g.Town.Dir).Normalized();
        Assert.Equal(g.Town.HeightM, g.Height((g.Town.Dir + east * 0.04).Normalized()), 6);
    }

    [Fact]
    public void HasVariedBiomes()
    {
        var g = new PlanetGen(42);
        var seen = Samples(4000).Select(g.BiomeAt).Distinct().ToList();
        Assert.True(seen.Count >= 6, "biomes: " + string.Join(",", seen));
    }
}
