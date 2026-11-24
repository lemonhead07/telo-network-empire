using Telo.Sim.Act;
using Telo.Sim.Net;
using Telo.Sim.Planet;
using Telo.Sim.Town;
using Telo.Sim.World;

namespace Telo.Sim.Tests;

/// <summary>EA3b: Farland is across the water, and the only way there is the ferry.</summary>
public class FarlandTests
{
    [Fact]
    public void Farland_is_land_of_its_own_with_sea_all_round_and_no_road_over()
    {
        var gen = new PlanetGen(1983);
        var farland = gen.SiteById("farland");
        Assert.NotNull(farland);
        var island = Assert.Single(gen.Islands);
        Assert.Equal("farland", gen.LandOf(farland!.Dir));
        Assert.Equal(PlanetGen.Mainland, gen.LandOf(gen.Town.Dir));
        Assert.Equal(PlanetGen.Mainland, gen.LandOf(gen.SiteById("eastgate")!.Dir));

        // water all the way round the edge of Farland's land: any dry way to the mainland would have to cross it
        var c = island.Dir;
        var e = new V3(0, 1, 0).Cross(c).Normalized();
        var n = c.Cross(e);
        foreach (double r in new[] { 1.25, 1.45 })
            for (int k = 0; k < 720; k++)
            {
                // points round the circle without trig: walk the square and push each point out to the radius
                double t = k / 180.0, x = t < 1 ? 1 : t < 2 ? 1 - (t - 1) * 2 : t < 3 ? -1 : -1 + (t - 3) * 2, z = t < 1 ? -1 + t * 2 : t < 2 ? 1 : t < 3 ? 1 - (t - 2) * 2 : -1;
                double len = Math.Sqrt(x * x + z * z);
                var p = (c + (e * (x / len) + n * (z / len)) * (r * island.RadiusM / gen.RadiusM)).Normalized();
                Assert.True(gen.Height(p) < 0, $"dry ground {r * island.RadiusM:0} m out from Farland's middle (height {gen.Height(p):0.0})");
            }

        // no road crosses the water, and the ferry is the way over: a few hundred metres of open sea, quay to quay
        foreach (var road in gen.Roads) Assert.Single(road.Points.Select(gen.LandOf).Distinct());
        var ferry = Assert.Single(gen.Crossings);
        Assert.Equal(PlanetGen.Mainland, ferry.From.Land);
        Assert.Equal("farland", ferry.To.Land);
        Assert.InRange(gen.SurfaceDistance(ferry.From.Dir, ferry.To.Dir), 400, 1500);
        Assert.True(gen.Height((ferry.From.Dir + ferry.To.Dir).Normalized()) < -5, "open water halfway across");
    }

    [Fact]
    public void A_trip_to_Farland_goes_by_the_ferry_from_Act_3_and_its_lines_skip_the_switchboard()
    {
        var sim = Simulation.NewGame(TestContent.Db, 1983);
        var me = sim.Actor();
        var gen = new PlanetGen(1983);
        var s = sim.State;
        const string door = "lot-fl-n1";

        // laid up until there's trade across the water
        var early = Actions.For(sim, me, gen).Single(a => a.Id == $"go:{door}");
        Assert.Contains("no sailings", early.Blocked);

        s.ActIndex = 3; s.Minute = 60;
        var go = Actions.For(sim, me, gen).Single(a => a.Id == $"go:{door}");
        Assert.True(go.Ready, go.Blocked);
        Assert.Contains("ferry", go.Label);
        var route = Places.RouteOf(gen, sim.Content, Places.Room, door);
        Assert.NotNull(route.By);
        Assert.Equal(Places.DriveMinutes(route.LandM) + Vehicles.All["ferry"].FixedMinutes, go.Minutes);
        int before = s.Minute;
        Assert.True(Actions.Perform(sim, me, gen, go.Id));
        Assert.Equal(go.Minutes, s.Minute - before);   // the crossing is lived time, like the drive either side of it
        Assert.Equal(door, me.Place);
        Assert.Equal(door, me.VanAt);                  // the van came over too

        // a Farland line is jumpered at the ocean cable's beach end as the install is signed off: live, and no port on your board
        var cust = TownRules.SignUp(s, sim.Content.Lots[door]);
        s.Modems = 1;
        Assert.True(TownRules.FitSocket(s, cust.Id, sim.Content));
        Assert.True(cust.Installed);
        Assert.Null(SwitchRules.Pos(s, cust.Id));
    }
}
