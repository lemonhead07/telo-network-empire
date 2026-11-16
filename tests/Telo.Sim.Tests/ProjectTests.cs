using Telo.Sim.Almanac;
using Telo.Sim.Build;
using Telo.Sim.Town;
using Telo.Sim.World;
using Xunit;

namespace Telo.Sim.Tests;

public class ProjectTests
{
    private static void Build(Simulation sim, string id)
    {
        var s = sim.State; var p = sim.Content.Project(id)!;
        for (int guard = 0; guard < 200 && !ProjectRules.Done(s, id); guard++)
        {
            if (ProjectRules.ProgressOf(s, id).WaitUntil > s.Day) { sim.Sleep(); continue; }
            Assert.True(ProjectRules.Do(s, sim.Content, p, Tools.All), ProjectRules.Blocked(s, sim.Content, p, Tools.All));
        }
        Assert.True(ProjectRules.Done(s, id));
    }

    [Fact]
    public void Each_project_opens_the_next_part_of_the_game_and_nothing_comes_before_what_it_needs()
    {
        var sim = Simulation.NewGame(TestContent.Db, 11);
        var s = sim.State; var c = sim.Content;
        while (!Onboarding.Reached(s, c, OnboardingStep.Free)) sim.GodNextStage(null);
        s.Cash = 10_000;
        var church = c.Project("proj-church-poles")!;

        // locked until the farm is served and there are six subscribers; Church Lane can't be signed until it's built
        Assert.NotNull(ProjectRules.Locked(s, c, church));
        Assert.DoesNotContain(TownRules.Prospects(s, c), l => l.Id.StartsWith("lot-cl-"));
        for (int i = 0; i < 6; i++) sim.AddCustomer($"Sub {i}", "family", 20);
        Ledger.LedgerRules.Serve(s, c, "prem-adeyemi-farm", alreadyBuilt: true);   // the farm line is up
        Assert.Null(ProjectRules.Locked(s, c, church));
        Assert.Equal(church.Id, ProjectRules.Next(s, c)?.Id);

        // kit matters: a pole for the first step, nothing happens without it
        Assert.False(ProjectRules.Do(s, c, church, Tools.Toolbox));
        Assert.Contains("pole", ProjectRules.Blocked(s, c, church, Tools.None)!);
        long cash = s.Cash;
        Build(sim, church.Id);
        Assert.Equal(cash - church.Cost, s.Cash);
        Assert.Contains(TownRules.Prospects(s, c), l => l.Id.StartsWith("lot-cl-"));

        // the village waits for its act; the modem bank waits for its Almanac page and the depot
        var cabinet = c.Project("proj-eastgate-cabinet")!;
        for (int i = 0; i < 6; i++) sim.AddCustomer($"More {i}", "family", 20);
        Assert.Contains("not until", ProjectRules.Locked(s, c, cabinet)!);
        s.ActIndex = 1;
        Build(sim, cabinet.Id);
        Assert.Contains(TownRules.Prospects(s, c), l => l.Id.StartsWith("lot-eg-"));

        var bank = c.Project("proj-modem-bank")!;
        s.ActIndex = 2;
        Assert.Contains("Almanac", ProjectRules.Locked(s, c, bank)!);
        s.Entries["alm-14k"] = EntryStatus.Recalled;
        Assert.Contains("depot", ProjectRules.Locked(s, c, bank)!);
        s.HasDepot = true;   // the lease (StaffRules.LeaseDepot) wants staff first; only the depot itself matters here
        int lines = Net.BoardRules.Home(s).LinesIn;
        Build(sim, bank.Id);
        Assert.Equal(lines + 8, Net.BoardRules.Home(s).LinesIn);

        // the leased line: a form, then NezTel's engineer in ten days; it brings income and a bill
        var leased = c.Project("proj-leased-line")!;
        int start = s.Day;
        Build(sim, leased.Id);
        Assert.True(s.Day - start >= 10);
        Assert.Equal(5L * s.LiveSubscribers, ProjectRules.MonthlyRevenue(s, c));
        Assert.Equal(120L, ProjectRules.MonthlyCost(s, c));

        // your own microwave hop ends NezTel's bill
        for (int i = 0; i < 14; i++) sim.AddCustomer($"Later {i}", "family", 20);
        Build(sim, "proj-microwave");
        Assert.Equal(0L, ProjectRules.MonthlyCost(s, c));
        Assert.NotNull(ProjectRules.Locked(s, c, c.Project("proj-cold-store")!));   // the datacentre is the boom's (Act 3)
    }
}
