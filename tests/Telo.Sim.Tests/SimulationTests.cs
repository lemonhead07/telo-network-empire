using Telo.Sim.Content;
using Telo.Sim.Ledger;
using Telo.Sim.Save;
using Telo.Sim.Work;
using Telo.Sim.World;

namespace Telo.Sim.Tests;

public class SimulationTests
{
    [Fact]
    public void Deterministic()
    {
        string Run()
        {
            var sim = TestContent.NewGame(99);
            sim.AddCustomer("Ada", "family", 12);
            sim.AdvanceDays(400);
            return SaveCodec.ToJson(sim.State);
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void MonthlyBillingPaysRentAndCollectsFees()
    {
        var sim = TestContent.NewGame();
        sim.AddCustomer("Ada", "family", 30);
        long before = sim.State.Cash;
        while (!sim.State.Date.IsFirstOfMonth) sim.AdvanceDay();
        Assert.Equal(before + 30 - Simulation.RentPerMonth, sim.State.Cash);
    }

    [Fact]
    public void ActAdvancesOnMilestoneAndAnnouncementQueue()
    {
        var sim = TestContent.NewGame();
        sim.AddCustomer("Ada", "family", 10);
        sim.AddCustomer("Bo", "student", 10);
        sim.AdvanceDays(30);
        Assert.Equal(0, sim.State.ActIndex); // milestone met, but the world hasn't announced hobbyist networks yet
        sim.AdvanceDays(600);
        Assert.Equal(1, sim.State.ActIndex);
    }

    [Fact]
    public void GameplayChanges_InstallsBecomeDelegated()
    {
        var sim = TestContent.NewGame();
        Assert.Equal(WorkMode.Manual, WorkRules.Current(sim.State, sim.Content, "install").Mode);
        Assert.Equal(UpgradeResult.NeedsAct, WorkRules.CanUpgrade(sim.State, sim.Content, "install"));
        sim.State.ActIndex = 1; sim.State.Cash = 5000;
        Assert.Equal(UpgradeResult.Ok, WorkRules.Upgrade(sim.State, sim.Content, "install"));
        Assert.Equal(1, sim.State.Staff);
        var job = sim.NewFrameJob(new[] { sim.AddCustomer("A", "x", 1), sim.AddCustomer("B", "x", 1), sim.AddCustomer("C", "x", 1) });
        Assert.NotNull(sim.TryDelegateFrame(job));
    }

    [Fact]
    public void HotWiredDefectsSurfaceLater()
    {
        var sim = TestContent.NewGame();
        var custs = Enumerable.Range(0, 4).Select(i => sim.AddCustomer($"C{i}", "x", 5)).ToList();
        var job = sim.NewFrameJob(custs);
        foreach (var l in job.Lines.Where(l => !l.IsPartyLine)) job.HotWire(l.Index);
        sim.ApplyFrameOutcome(job.Evaluate());
        Assert.NotEmpty(sim.State.Defects);
        sim.AdvanceDays(60);
        Assert.Empty(sim.State.Defects);
        Assert.Contains(sim.State.Log, e => e.Kind == Core.EventKind.Defect);
    }

    [Fact]
    public void LedgerRemembersBrokenPromises()
    {
        var sim = TestContent.NewGame();
        Assert.True(LedgerRules.Promise(sim.State, sim.Content, "prem-adeyemi-farm", sim.State.Day + 10));
        sim.AdvanceDays(11);
        Assert.Equal(PromiseStatus.Broken, LedgerRules.StatusOf(sim.State, "prem-adeyemi-farm"));
        sim.State.Cash = 1000;
        Assert.True(LedgerRules.Serve(sim.State, sim.Content, "prem-adeyemi-farm"));
        Assert.Contains(sim.State.Customers, c => c.PremiseId == "prem-adeyemi-farm");
    }

    [Fact]
    public void SaveRoundTripsAndContinuesIdentically()
    {
        var a = TestContent.NewGame(5);
        a.AddCustomer("Ada", "family", 12);
        a.AdvanceDays(100);
        var b = new Simulation(a.Content, SaveCodec.FromJson(SaveCodec.ToJson(a.State)));
        a.AdvanceDays(200); b.AdvanceDays(200);
        Assert.Equal(SaveCodec.ToJson(a.State), SaveCodec.ToJson(b.State));
    }

    [Fact]
    public void RejectsSavesFromTheFuture()
    {
        var json = SaveCodec.ToJson(new GameState { Version = 999 });
        Assert.Throws<InvalidDataException>(() => SaveCodec.FromJson(json));
    }
}

public class InstallFlowTests
{
    [Fact]
    public void EnquiriesOnlyPayOnceWiredAtTheFrame()
    {
        var sim = TestContent.NewGame(3);
        for (int i = 0; i < 3; i++) sim.TakeEnquiry();
        Assert.Equal(0, sim.State.LiveSubscribers);
        Assert.Equal(3, sim.State.PendingInstalls);

        var job = sim.NewFrameJob(sim.FrameWorkOrder());
        foreach (var l in job.Lines.Where(l => !l.IsPartyLine))
        {
            int t = job.VisibleTarget(l.Index) ?? job.Probe(l.Index);
            job.Jumper(l.Index, job.TestPort(t) ? t : job.SparePorts[0]);
        }
        sim.ApplyFrameOutcome(job.Evaluate());
        Assert.Equal(3, sim.State.LiveSubscribers);
        Assert.Contains(sim.State.Log, e => e.Text.StartsWith("First customer"));
    }
}
