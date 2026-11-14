using Telo.Sim.Net;
using Telo.Sim.Save;
using Telo.Sim.Town;
using Telo.Sim.World;

namespace Telo.Sim.Tests;

public class SwitchboardTests
{
    private static (Simulation Sim, List<Customer> Custs) Seated(int n, ulong seed = 42)
    {
        var sim = TestContent.NewGame(seed);
        sim.State.Modems = n;
        var custs = new List<Customer>();
        for (int i = 0; i < n; i++) { var c = sim.TakeEnquiry()!; Assert.True(TownRules.FitSocket(sim.State, c.Id)); custs.Add(c); }
        return (sim, custs);
    }

    [Fact]
    public void SameSeedSameBoard_AndProperWiringPutsEveryoneOn()
    {
        var (a, _) = Seated(8); var (b, _) = Seated(8);
        Assert.Equal(a.State.Switch.Positions.Select(p => (p.AssignedPort, p.TagRead)), b.State.Switch.Positions.Select(p => (p.AssignedPort, p.TagRead)));
        Assert.Equal(a.State.Switch.FaultyPorts, b.State.Switch.FaultyPorts);
        Assert.True(a.State.Switch.Positions.Take(2).All(p => p.TagRead));       // the first two are plain
        TestContent.WireUp(a);
        Assert.Equal(8, a.State.LiveSubscribers);
        Assert.Empty(a.State.Defects);
    }

    [Fact]
    public void WiresArePeople_PullOneAndTheyGoDead_CrossOneAndSomeoneListensIn()
    {
        var (sim, custs) = Seated(4);
        var s = sim.State;
        TestContent.WireUp(sim);
        int before = custs[0].Satisfaction;
        Assert.Equal(custs[0].Id, SwitchRules.Pull(s, custs[0].Id));
        Assert.True(!custs[0].Installed && custs[0].Satisfaction < before);
        long cash = s.Cash;
        SwitchRules.Jumper(s, custs[0].Id, SwitchRules.Pos(s, custs[0].Id)!.AssignedPort);
        Assert.Equal(ThrowResult.Live, sim.ThrowSwitch().Single().Result);
        Assert.Equal(cash, s.Cash);                                              // back on, but no second connection fee

        s.Modems = 1;
        Customer? late = null;
        while (late is null) late = sim.TakeEnquiry();
        TownRules.FitSocket(s, late.Id);
        string? cut = SwitchRules.Jumper(s, late.Id, SwitchRules.Pos(s, custs[1].Id)!.AssignedPort);   // plugged over a neighbour
        Assert.Equal(custs[1].Id, cut);
        Assert.Equal(ThrowResult.Crossed, sim.ThrowSwitch().Single(r => r.CustomerId == late.Id).Result);
        Assert.False(late.Installed);
    }

    [Fact]
    public void TheBoardFillsUp_UntilAnotherPanelIsBoltedOn_AndStaffJumpersCanBeReseated()
    {
        var (sim, _) = Seated(8);
        var s = sim.State;
        TestContent.WireUp(sim);
        s.Modems = 1;
        Customer? ninth = null;
        while (ninth is null) ninth = sim.TakeEnquiry();
        TownRules.FitSocket(s, ninth.Id);
        Assert.Null(SwitchRules.Pos(s, ninth.Id));
        s.Orders.Clear();
        Assert.Contains("full", Guidance.Next(s, sim.Content).Text);
        s.Cash = 500;
        Assert.True(SwitchRules.AddPanel(s));
        Assert.NotNull(SwitchRules.Pos(s, ninth.Id));

        Assert.True(SwitchRules.AutoWire(s, sim.Content, ninth, sloppy: true));
        s.Defects.Add(new LatentDefect { Id = "d", SurfaceDay = s.Day + 20, Source = "technician", CustomerId = ninth.Id, Text = "x" });
        Assert.True(ninth.Installed && SwitchRules.Pos(s, ninth.Id)!.Loose);
        Assert.True(SwitchRules.Reseat(s, ninth.Id));
        Assert.Empty(s.Defects);

        var reloaded = SaveCodec.FromJson(SaveCodec.ToJson(s), sim.Content);
        Assert.Equal(s.Switch.Positions.Count, reloaded.Switch.Positions.Count);
        var node = System.Text.Json.Nodes.JsonNode.Parse(SaveCodec.ToJson(s))!.AsObject();
        node["version"] = 6; node.Remove("switch");
        var old = SaveCodec.FromJson(node.ToJsonString(), sim.Content);
        Assert.Contains(old.Switch.Positions, p => p.CustomerId == ninth.Id && p.Jumper >= 0);   // old saves get their wires
    }
}
