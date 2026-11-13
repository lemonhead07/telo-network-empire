using Telo.Sim.Almanac;
using Telo.Sim.Minigames.Install;
using Telo.Sim.Net;
using Telo.Sim.Save;
using Telo.Sim.Town;
using Telo.Sim.Work;
using Telo.Sim.World;

namespace Telo.Sim.Tests;

/// <summary>Phase A and B: the act can be started early, a job survives a save, the town pays its way, and evenings fill the Board.</summary>
public class CarryTests
{
    private static Simulation WholeTown()
    {
        var sim = TestContent.NewGame();
        var gen = new Telo.Sim.Planet.PlanetGen(1983);
        for (int i = 0; i < 8; i++) sim.GodNextStage(gen);   // five first-hour steps, the hire, the pole line, the rest of the town
        Assert.Empty(TownRules.Prospects(sim.State, sim.Content));
        return sim;
    }

    [Fact]
    public void EveningsAreRepeatable_AndMoreLinesMeanFewerEngagedTones()
    {
        var sim = WholeTown();
        var s = sim.State; var ex = BoardRules.Home(s);
        int Engaged(int lines) { ex.LinesIn = lines; return Enumerable.Range(0, 30).Sum(d => BoardRules.Evening(s, 500 + d, ex).Engaged); }
        int two = Engaged(2), four = Engaged(4), twelve = Engaged(12);
        Assert.Equal(two, Engaged(2));
        Assert.True(two > four && four > twelve, $"engaged tones over a month: {two} on 2 lines, {four} on 4, {twelve} on 12");
        Assert.True(two > 30, "a whole town on two lines should be visibly too many");
        Assert.Equal(0, twelve);
        ex.LinesIn = 2;
        var eve = BoardRules.Evening(s, 500, ex);
        Assert.True(eve.Peak <= 2 && eve.InUse.Length == BoardRules.Slots);
    }

    [Fact]
    public void ALineCardIsBoughtCarriedAndFitted_UpToWhatTheBoardCanAnswer()
    {
        var sim = WholeTown();
        var s = sim.State; s.Cash = 1000;
        Assert.True(BoardRules.BuyCard(s));
        Assert.False(BoardRules.FitCard(s, Tools.Toolbox));          // the card has to be there
        Assert.True(BoardRules.FitCard(s, Tools.LineCard));
        Assert.Equal(3, BoardRules.Home(s).LinesIn);
        Assert.True(BoardRules.BuyCard(s) && BoardRules.FitCard(s, Tools.All));
        Assert.False(BoardRules.BuyCard(s));                         // a home-made board answers four lines at most
        s.Entries[BoardRules.HuntGroupEntry] = EntryStatus.Recalled;
        Assert.True(BoardRules.BuyCard(s));
    }

    [Fact]
    public void AFullTownWithATechnicianPaysItsWay()
    {
        var sim = WholeTown();
        var s = sim.State;
        BoardRules.Home(s).LinesIn = 4;
        long revenue = s.Customers.Where(c => c.Live && c.Installed).Sum(c => c.MonthlyFee);
        long wages = s.StaffMembers.Sum(m => m.WeeklyWage) * 13 / 3 + StaffRules.DepotRentPerMonth;   // a month of weekly wages, working from the depot
        long costs = Simulation.RentPerMonth + wages + WorkRules.MonthlyUpkeep(s, sim.Content) + BoardRules.MonthlyRental(s);
        Assert.True(revenue > costs, $"monthly: in {revenue}, out {costs}");
        Assert.True(revenue - costs < 400, "but not by so much that nothing else needs building");
    }

    [Fact]
    public void TheNextActCanBeRecalledEarly_ForAHeadStart()
    {
        var sim = WholeTown();
        var s = sim.State; var c = sim.Content;
        Assert.Equal(0, s.ActIndex);
        var page = ActRules.EarlyStartEntry(s, c);
        Assert.NotNull(page);
        s.ArchiveMilli = 100_000;
        Assert.Equal(RecallResult.Ok, AlmanacRules.Recall(s, c, page!.Id));
        sim.AdvanceDay();
        Assert.Equal(1, s.ActIndex);
        Assert.True(s.HeadStartDays[c.Acts[1].Id] > 300);
        Assert.True(s.Echo > 0);
    }

    [Fact]
    public void AHalfDoneInstallSurvivesASave_AndOldSavesLoad()
    {
        var sim = TestContent.NewGame();
        var cust = sim.TakeEnquiry()!;
        var job = sim.NewInstallJob(cust.Id)!;
        job.Do(InstallStep.DropWire); job.Rush(InstallStep.DropWire);
        sim.RecordInstall(job);
        var reloaded = new Simulation(sim.Content, SaveCodec.FromJson(SaveCodec.ToJson(sim.State)));
        var again = reloaded.NewInstallJob(cust.Id)!;
        Assert.True(again.IsDone(InstallStep.DropWire) && again.IsRushed(InstallStep.DropWire));
        Assert.Equal(job.ElapsedMinutes, again.ElapsedMinutes);
        while (again.NextStep is { } step) again.Do(step);
        Assert.True(reloaded.ApplyInstallOutcome(again.Evaluate()));
        Assert.Empty(reloaded.State.Installs);

        var node = System.Text.Json.Nodes.JsonNode.Parse(SaveCodec.ToJson(sim.State))!.AsObject();
        node["version"] = 3; node.Remove("exchanges"); node.Remove("installs"); node.Remove("headStartDays");
        var old = SaveCodec.FromJson(node.ToJsonString());
        Assert.Equal(BoardRules.StartLines, BoardRules.Home(old).LinesIn);
    }
}
