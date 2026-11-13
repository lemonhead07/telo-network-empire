using Telo.Sim.Content;
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
    public void TheTechnicianWorksOnlyOnTicketsTheyAreHanded_TwoADay_PaidByTheJobOnFriday()
    {
        var sim = Hired();
        var s = sim.State; var c = sim.Content;
        s.Modems = 1;
        var a = sim.TakeEnquiry()!; var b = sim.TakeEnquiry()!; var third = sim.TakeEnquiry()!;
        sim.AdvanceDay();
        Assert.False(a.Installed);                                     // no ticket, no work
        var jory = s.StaffMembers.Single();
        Assert.Equal("Jory Pike", jory.Name);
        Assert.True(StaffRules.Assign(s, c, $"install:{a.Id}", jory.Id));
        Assert.True(StaffRules.Assign(s, c, $"install:{b.Id}", jory.Id));
        Assert.False(StaffRules.Assign(s, c, $"install:{third.Id}", jory.Id));   // a day's worth at a time
        Assert.False(StaffRules.Assign(s, c, $"job:nothing", jory.Id));
        sim.AdvanceDay();
        Assert.True(a.Installed && !b.Installed);                      // one modem, one install
        s.Modems = 1;
        sim.AdvanceDay();
        Assert.True(b.Installed);
        Assert.Equal(2, s.TechInstalls);
        long cash = s.Cash;
        for (int i = 0; i < 7 && !s.Log.Any(e => e.Text.StartsWith("Pay day")); i++) { cash = s.Cash; sim.AdvanceDay(); }
        Assert.Contains(s.Log, e => e.Text.StartsWith("Pay day"));
        Assert.True(s.Cash <= cash - jory.PieceRate * 2 + 400 && jory.JobsThisWeek == 0);
    }

    [Fact]
    public void WithADepotStaffCollectTheirOwnTickets_AndTheClockDoesTheWorkDuringTheDay()
    {
        var sim = Hired();
        var s = sim.State; var c = sim.Content;
        s.Modems = 5; s.Cash = 1000;
        Assert.True(StaffRules.LeaseDepot(s));
        var a = sim.TakeEnquiry()!;
        while (!Clock.IsWorkingDay(s.Day + 1)) sim.AdvanceDay();
        sim.Sleep();                                                   // morning of a working day: the ticket is collected
        Assert.Contains(StaffRules.Tickets(s, c), t => t.AssignedTo == "install");
        Assert.False(a.Installed);
        sim.AdvanceMinutes(5 * 60);                                    // seven until noon
        Assert.True(a.Installed);
        Assert.Equal(1, s.StaffMembers[0].JobsToday);
    }

    [Fact]
    public void PeopleKeepHours_OnceTheFirstHourIsOver()
    {
        var sim = Hired();
        var s = sim.State; var c = sim.Content;
        var family = c.Lots.Values.First(l => l.Archetype == "family" && l.Kind == LotKind.House);
        var retired = c.Lots.Values.First(l => l.Archetype == "tech-inexperienced");
        var shop = c.Lots.Values.First(l => l.Kind == LotKind.Shop);
        while (Clock.Weekday(s.Day) != DayOfWeek.Tuesday) sim.AdvanceDay();
        s.Minute = 5 * 60;                                             // eleven in the morning
        Assert.False(HomeRules.IsHome(s, c, family));
        Assert.True(HomeRules.IsHome(s, c, retired) && HomeRules.IsHome(s, c, shop));
        Assert.Equal(KnockResult.NobodyHome, sim.Knock(family.Id));
        s.Minute = 13 * 60;                                            // seven in the evening
        Assert.True(HomeRules.IsHome(s, c, family) && !HomeRules.IsHome(s, c, shop));
        s.Minute = 17 * 60;                                            // eleven at night
        Assert.False(HomeRules.IsHome(s, c, retired));
        var fresh = TestContent.NewGame();
        fresh.State.Minute = 17 * 60;
        Assert.True(HomeRules.IsHome(fresh.State, fresh.Content, family));   // a new player's first hour is spared
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
