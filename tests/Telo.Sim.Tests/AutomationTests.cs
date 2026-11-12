using Telo.Sim.Minigames.Install;
using Telo.Sim.Save;
using Telo.Sim.Town;
using Telo.Sim.Work;
using Telo.Sim.World;

namespace Telo.Sim.Tests;

/// <summary>The first automation stage end to end: hire, the technician works alone, their mistakes become repair visits.</summary>
public class AutomationTests
{
    private static Simulation Hired()
    {
        var sim = TestContent.NewGame();
        for (int i = 0; i < 4; i++) sim.AddCustomer($"S{i}", "x", 5);
        sim.State.Cash = 2000;
        Assert.Equal(UpgradeResult.Ok, WorkRules.Upgrade(sim.State, sim.Content, "install"));
        return sim;
    }

    [Fact]
    public void TechnicianConnectsOneCustomerADay_AndNeedsModems()
    {
        var sim = Hired();
        sim.State.Modems = 1;
        var a = sim.TakeEnquiry()!; var b = sim.TakeEnquiry()!;
        sim.AdvanceDay();
        Assert.True(a.Installed && !b.Installed);
        sim.AdvanceDays(3);
        Assert.False(b.Installed); // no modem, no install
        sim.State.Modems = 1;
        sim.AdvanceDay();
        Assert.True(b.Installed);
        Assert.Equal(2, sim.State.TechInstalls);
    }

    [Fact]
    public void SurfacedDefectBecomesAFaultUntilRepairedWithTheToolbox()
    {
        var sim = TestContent.NewGame();
        var cust = sim.TakeEnquiry()!;
        var job = sim.NewInstallJob(cust.Id)!;
        job.Rush(InstallStep.DropWire);
        while (job.NextStep is { } step) job.Do(step);
        sim.ApplyInstallOutcome(job.Evaluate());
        for (int i = 0; i < 50 && sim.State.Faults.Count == 0; i++) sim.AdvanceDay();
        var fault = TownRules.FaultAt(sim.State, cust.LotId!);
        Assert.NotNull(fault);
        int before = cust.Satisfaction;
        sim.AdvanceDay();
        Assert.True(cust.Satisfaction < before || before == 0);
        Assert.False(TownRules.Repair(sim.State, fault!.Id, Tools.Ladder));
        Assert.True(TownRules.Repair(sim.State, fault.Id, Tools.Toolbox));
        Assert.Empty(sim.State.Faults);
    }

    [Fact]
    public void WorkNeedsTheRightKitOnSite()
    {
        var j = InstallJob.Generate(1, "c", "Ada");
        Assert.False(j.Do(InstallStep.DropWire, Tools.CableDrum));              // top clip is up a ladder
        Assert.Equal(Tools.Ladder, j.Missing(InstallStep.DropWire, Tools.CableDrum));
        Assert.True(j.Do(InstallStep.DropWire, Tools.CableDrum | Tools.Ladder));
        Assert.True(j.Do(InstallStep.DropWire, Tools.CableDrum));               // lower clips are in reach
    }

    [Fact]
    public void Version1SavesLoad()
    {
        var sim = TestContent.NewGame();
        var node = System.Text.Json.Nodes.JsonNode.Parse(SaveCodec.ToJson(sim.State))!.AsObject();
        node["version"] = 1; node.Remove("faults"); node.Remove("techInstalls");
        string v1 = node.ToJsonString();
        var s = SaveCodec.FromJson(v1);
        Assert.Equal(GameState.CurrentVersion, s.Version);
        Assert.NotNull(s.Faults);
    }
}

public class PoleLineTests
{
    [Fact]
    public void ThePoleLineIsBuiltPoleBySpan_AndConnectsTheFarm()
    {
        var sim = TestContent.NewGame();
        var gen = new Telo.Sim.Planet.PlanetGen(1983);
        var farm = sim.Content.Premises["prem-adeyemi-farm"];
        var sites = Telo.Sim.Build.LineRules.PoleSites(gen, farm);
        Assert.InRange(sites.Count, 4, 40);
        Assert.Equal(sites, Telo.Sim.Build.LineRules.PoleSites(gen, farm));
        Assert.False(Telo.Sim.Ledger.LedgerRules.Serve(sim.State, sim.Content, farm.Id)); // can't be bought any more
        sim.State.Cash = 1000;
        Assert.False(Telo.Sim.Build.LineRules.Plant(sim.State, gen, farm, Tools.Ladder));  // no pole to hand
        Assert.False(Telo.Sim.Build.LineRules.String(sim.State, sim.Content, gen, farm, Tools.All)); // nothing to string between
        for (int i = 0; i < sites.Count; i++) Assert.True(Telo.Sim.Build.LineRules.Plant(sim.State, gen, farm, Tools.Pole));
        Assert.Equal(1000 - sites.Count * farm.PoleLine!.PoleCost, sim.State.Cash);
        for (int i = 0; i < sites.Count; i++) Assert.True(Telo.Sim.Build.LineRules.String(sim.State, sim.Content, gen, farm, Tools.All));
        Assert.Equal(PromiseStatus.Served, Telo.Sim.Ledger.LedgerRules.StatusOf(sim.State, farm.Id));
        Assert.Contains(sim.State.Customers, c => c.PremiseId == farm.Id);
        var reloaded = SaveCodec.FromJson(SaveCodec.ToJson(sim.State));
        Assert.Equal(sites.Count, reloaded.Lines[farm.Id].Strung);
    }
}
