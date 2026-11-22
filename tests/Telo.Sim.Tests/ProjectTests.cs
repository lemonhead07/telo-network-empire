using Telo.Sim.Almanac;
using Telo.Sim.Build;
using Telo.Sim.Content;
using Telo.Sim.Town;
using Telo.Sim.World;
using Xunit;

namespace Telo.Sim.Tests;

public class ProjectTests
{
    private static void Build(Simulation sim, string id)
    {
        var s = sim.State; var p = sim.Content.Project(id)!;
        // EA2b: the parts first, at the Yard (a crate on the van, or a lorry load the next morning)
        if (!ProjectRules.Started(s, id) && YardRules.NeedsParts(p) && !YardRules.Bought(s, id))
            Assert.True(YardRules.Buy(s, sim.Content, p), YardRules.CantBuy(s, sim.Content, p));
        for (int guard = 0; guard < 200 && !ProjectRules.Done(s, id); guard++)
        {
            if (ProjectRules.ProgressOf(s, id).WaitUntil > s.Day || s.Parts.TryGetValue(id, out var o) && o.ArrivesDay > s.Day) { sim.Sleep(); continue; }
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

        // EA2b: it starts once its parts are bought at the Yard and the crate is at the site
        long cash = s.Cash;
        Assert.Contains("Yard", ProjectRules.Blocked(s, c, church, Tools.All)!);
        Assert.True(YardRules.Buy(s, c, church));
        Assert.Equal(cash - church.Cost, s.Cash);
        Assert.Contains("parts crate", ProjectRules.Blocked(s, c, church, Tools.Pole)!);
        Assert.True(ProjectRules.Do(s, c, church, Tools.Parts));
        // kit matters: a pole for the first step, nothing happens without it
        Assert.False(ProjectRules.Do(s, c, church, Tools.Toolbox));
        Assert.Contains("pole", ProjectRules.Blocked(s, c, church, Tools.None)!);
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
    [Fact]
    public void The_core_ends_when_the_board_is_lit_masts_wait_for_it_and_old_mobile_saves_move_up_an_act()
    {
        var sim = Simulation.NewGame(TestContent.Db, 11);
        var s = sim.State; var c = sim.Content;
        Assert.Equal("act5-core", c.Acts[5].Id);
        Assert.Equal("act5-mobile", c.Acts[6].Id);   // ids are labels: mobile keeps its id at index 6 (D46)
        var mast = c.Project("proj-first-mast")!;
        Assert.Equal(6, mast.Act);
        Assert.Contains("proj-dc-lit", mast.RequiresProjects);

        // every project but the finale built: still in The Core (it ends on the lit board, not a count)
        s.ActIndex = 5;
        s.FiredAnnouncements.Add("ann-smartphone");
        foreach (var p in c.Projects.Where(p => p.Act <= 5 && p.Id != "proj-dc-lit")) ProjectRules.Finish(s, c, p);
        Assert.False(ActRules.TryAdvance(s, c));
        ProjectRules.Finish(s, c, c.Project("proj-dc-lit")!);
        Assert.True(ActRules.TryAdvance(s, c));
        Assert.Equal(6, s.ActIndex);

        // a v18 save made in mobile (then index 5) is in mobile still; one made in Act 4 stays there
        var node = System.Text.Json.Nodes.JsonNode.Parse(Save.SaveCodec.ToJson(s))!;
        node["version"] = 18; node["actIndex"] = 5;
        Assert.Equal(6, Save.SaveCodec.FromJson(node.ToJsonString(), c).ActIndex);
        node["actIndex"] = 4;
        Assert.Equal(4, Save.SaveCodec.FromJson(node.ToJsonString(), c).ActIndex);
    }

    [Fact]
    public void A_project_cant_start_until_its_parts_are_bought_at_the_yard_and_on_site()
    {
        var sim = Simulation.NewGame(TestContent.Db, 11);
        var s = sim.State; var c = sim.Content;
        var gen = new Planet.PlanetGen(1983);
        while (!Onboarding.Reached(s, c, OnboardingStep.Free)) sim.GodNextStage(null);
        for (int i = 0; i < 6; i++) sim.AddCustomer($"Sub {i}", "family", 20);
        Ledger.LedgerRules.Serve(s, c, "prem-adeyemi-farm", alreadyBuilt: true);
        s.Cash = 5_000;
        var church = c.Project("proj-church-poles")!;
        var yard = YardRules.Lot(c)!;
        var me = new Act.ActorState { Place = yard.Id, VanAt = yard.Id, IgnoreHours = true };
        var site = Act.Places.Project(church.Id);

        // the site won't start without the parts (the script player follows the "what next" line there and back)
        Assert.Contains("Yard", Act.Actions.WhyNot(sim, new Act.ActorState { Place = site, VanAt = site, Holding = Tools.Pole }, gen, "project:do")!);

        // buy them at the counter: paid there, and a crate rides on the van
        long cash = s.Cash;
        Assert.True(Act.Actions.Perform(sim, me, gen, $"yard:buy:{church.Id}"));
        Assert.Equal(cash - church.Cost, s.Cash);
        Assert.DoesNotContain(Act.Actions.For(sim, me, gen), a => a.Id == $"yard:buy:{church.Id}");   // bought once

        // drive to the site with empty hands: still nothing until the crate is off the van and at the site
        Assert.True(Act.Actions.Perform(sim, me, gen, $"go:{site}"));
        Assert.Contains("parts crate", Act.Actions.WhyNot(sim, me, gen, "project:do")!);
        Assert.True(Act.Actions.Perform(sim, me, gen, "take:Parts"));
        Assert.True(Act.Actions.Perform(sim, me, gen, "project:do"));
        Assert.True(ProjectRules.Started(s, church.Id));
        Assert.Equal(Tools.None, me.Holding);                                        // the crate stays at the site
        Assert.DoesNotContain(Act.Actions.For(sim, me, gen), a => a.Id == "take:Parts");   // and none left on the van
        Assert.Empty(s.Parts);

        // big parts come on the Yard's lorry the next working morning (never a Sunday); a parts order survives a save
        var millbrook = c.Project("proj-millbrook")!;
        Assert.Equal(PartsBy.Lorry, millbrook.Parts);
        s.ActIndex = 2;
        foreach (var r in millbrook.RequiresProjects) ProjectRules.GodBuild(s, c, c.Project(r)!);
        for (int i = 0; i < 30; i++) sim.AddCustomer($"More {i}", "family", 20);
        Assert.Null(ProjectRules.Locked(s, c, millbrook));
        Assert.True(YardRules.Buy(s, c, millbrook));
        var loaded = Save.SaveCodec.FromJson(Save.SaveCodec.ToJson(s), c);
        Assert.True(loaded.Parts[millbrook.Id].Lorry);
        Assert.Contains("lorry", ProjectRules.Blocked(s, c, millbrook, Tools.All)!);
        Assert.NotEqual(DayOfWeek.Sunday, Clock.Weekday(s.Parts[millbrook.Id].ArrivesDay));
        while (s.Day < s.Parts[millbrook.Id].ArrivesDay) sim.Sleep();
        Assert.Null(ProjectRules.Blocked(s, c, millbrook, Tools.None));             // no crate to carry: it's at the site
        Assert.True(ProjectRules.Do(s, c, millbrook, Tools.None));

        // paperwork has nothing to buy: the leased line is paid for when you start it
        var leased = c.Project("proj-leased-line")!;
        Assert.Equal(PartsBy.None, leased.Parts);
        Assert.NotNull(YardRules.CantBuy(s, c, leased));

        // the Yard keeps trade hours: shut on Sundays
        while (Clock.Weekday(s.Day) != DayOfWeek.Sunday) sim.Sleep();
        Assert.NotNull(HomeRules.WhyNot(s, c, yard));
    }
}
