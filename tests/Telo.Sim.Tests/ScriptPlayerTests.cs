using Telo.Sim.Content;
using Telo.Sim.Minigames.Install;
using Telo.Sim.Act;
using Telo.Sim.Ledger;
using Telo.Sim.Planet;
using Telo.Sim.Save;
using Telo.Sim.Town;
using Telo.Sim.World;

namespace Telo.Sim.Tests;

/// <summary>
/// The spine's proof: a player made of rules plays today's game from the first message to the farm line, choosing one action
/// at a time from the same list the player has. No god keys, no direct state edits.
/// </summary>
public class ScriptPlayerTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _out;
    public ScriptPlayerTests(Xunit.Abstractions.ITestOutputHelper output) => _out = output;

    private static (Simulation Sim, ActorState Me, PlanetGen Gen) NewGame()
    {
        var sim = Simulation.NewGame(TestContent.Db, 1983);
        return (sim, sim.Actor(), new PlanetGen(1983));
    }

    private static bool FarmServed(Simulation sim) =>
        sim.Content.Premises.Values.Where(p => p.PoleLine is not null).All(p => LedgerRules.StatusOf(sim.State, p.Id) == PromiseStatus.Served);

    [Fact]
    public void The_bot_plays_from_the_first_message_to_the_farm_line()
    {
        var (sim, me, gen) = NewGame();
        var log = new List<string>();
        var money = new SortedDictionary<int, string>();
        var chapters = new List<int> { Plot.Index(sim.State, sim.Content) };
        var taken = Bot.Play(sim, me, gen, () => FarmServed(sim) || sim.State.Day > Simulation.StartDate.Day + 1100, 120000,
            (id, why) => { log.Add($"{sim.State.Date} {Clock.Text(sim.State)} {id}  ({why})"); if (Plot.Index(sim.State, sim.Content) != chapters[^1]) chapters.Add(Plot.Index(sim.State, sim.Content)); if (sim.State.Day % 45 == 0 && !money.ContainsKey(sim.State.Day)) money[sim.State.Day] = $"{sim.State.Date}: ${sim.State.Cash} subs={sim.State.LiveSubscribers} staff={sim.State.StaffMembers.Count} act={sim.State.ActIndex} mailbill={sim.State.MailBillCents}"; });

        Assert.True(FarmServed(sim), $"farm line not built. at={me.Place} van={me.VanAt} holding={me.Holding} kit={string.Join(",", me.Kit.Select(k => k.Key + "@" + k.Value))} lines={string.Join(",", sim.State.Lines.Select(l => l.Key + ":" + l.Value.Planted + "/" + l.Value.Strung))}\nacts: {string.Join(" | ", Actions.For(sim, me, gen).Where(x => !x.Id.StartsWith("go:lot")).Select(x => x.Id + (x.Ready ? "" : "[" + x.Blocked + "]")))}\nmoney:\n{string.Join("\n", money.Values)}\nLast actions:\n" + string.Join("\n", log.TakeLast(6)));
        Assert.Equal(OnboardingStep.Free, Onboarding.Current(sim.State, sim.Content));
        Assert.Contains(taken, id => id == "messages");
        Assert.Contains(taken, id => id.StartsWith("install:do:"));
        Assert.Contains(taken, id => id == "switch:throw");
        Assert.Contains(taken, id => id.StartsWith("knock:"));
        Assert.Contains(taken, id => id == "pole:plant");
        Assert.True(sim.State.LiveSubscribers >= 4);
        // one plot line, no overlap: every chapter, in order, never going back, at least as far as the one after the farm line
        Assert.Equal(Enumerable.Range(0, chapters.Count), chapters);
        Assert.True(chapters[^1] > sim.Content.Plot.ToList().FindIndex(ch => ch.Id == "ch-farm"));
    }

    /// <summary>
    /// Early access is Acts 0 to 5: the bot plays from the first message into the mobile years by choosing actions, no cheats,
    /// so every step is reachable from the one before (nothing gated behind something that never comes).
    /// </summary>
    [Fact]
    public void The_bot_plays_from_the_first_message_into_act_5()
    {
        var (sim, me, gen) = NewGame();
        var s = sim.State;
        var log = new List<string>();
        var acts = new List<string>();
        var taken = Bot.Play(sim, me, gen, () => s.ActIndex >= 5 || s.Day > Simulation.StartDate.Day + 365 * 30, 400000,
            (id, why) =>
            {
                log.Add($"{s.Date} {Clock.Text(s)} {id}  ({why})");
                if (acts.Count == 0 || !acts[^1].StartsWith($"act {s.ActIndex}:"))
                    acts.Add($"act {s.ActIndex}: {s.Date} subs={s.TotalSubscribers} cash={s.Cash} projects={s.ProjectsDone.Count}");
            });
        _out.WriteLine(string.Join("\n", acts));
        Assert.True(s.ActIndex >= 5,
            $"stuck in act {s.ActIndex} on {s.Date}: subs={s.TotalSubscribers} cash={s.Cash} archive={s.Archive} projects={string.Join(",", s.ProjectsDone)}\n" +
            $"next: {Guidance.Next(s, sim.Content).Text}\n" + string.Join("\n", acts) + "\nLast actions:\n" + string.Join("\n", log.TakeLast(12)));
        // and through the acts' own work, not around it
        Assert.Contains("proj-first-mast", s.ProjectsDone.Concat(s.Projects.Keys).Append(Telo.Sim.Build.ProjectRules.Next(s, sim.Content)?.Id ?? ""));
    }

    [Fact]
    public void The_same_seed_plays_the_same_game_and_actors_survive_a_save()
    {
        string Run()
        {
            var (sim, me, gen) = NewGame();
            var ids = Bot.Play(sim, me, gen, () => sim.State.LiveSubscribers >= 2, 4000);
            return string.Join(",", ids) + $"|{sim.State.Cash}|{sim.State.Day}|{me.Place}";
        }
        Assert.Equal(Run(), Run());

        var (s2, a2, g2) = NewGame();
        Bot.Play(s2, a2, g2, () => s2.State.Customers.Count > 0 && a2.Place != Places.Room, 500);
        var back = SaveCodec.FromJson(SaveCodec.ToJson(s2.State), TestContent.Db);
        Assert.Equal(a2.Place, back.Actors["player"].Place);
        Assert.Equal(a2.VanAt, back.Actors["player"].VanAt);
    }
}

public class PhoneTests
{
    [Fact]
    public void One_text_in_each_morning_and_the_night_text_ends_the_day()
    {
        var sim = Simulation.NewGame(TestContent.Db, 7);
        Assert.Single(sim.State.Texts);
        Assert.Equal(sim.Content.Plot[0].Text, sim.State.Texts[0].Body);
        int day = sim.State.Day;
        Telo.Sim.Phone.PhoneRules.SendNight(sim);
        Assert.Equal(day + 1, sim.State.Day);
        Assert.Equal(2, sim.State.Texts.Count(t => !t.Out));       // the next morning's text came in
        sim.AdvanceDay();
        Assert.Equal(3, sim.State.Texts.Count(t => !t.Out));
        Assert.Single(sim.State.Texts, t => t.Out);
    }
}

public class PlannerTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _out;
    public PlannerTests(Xunit.Abstractions.ITestOutputHelper output) => _out = output;

    [Fact]
    public void A_day_is_what_fits_with_travel_between_and_grey_where_nobody_is_home()
    {
        var sim = Simulation.NewGame(TestContent.Db, 11);
        var gen = new PlanetGen(11);
        var s = sim.State;
        s.Modems = 20;
        sim.AddCustomer("Ada Firth", "family", 30); sim.AddCustomer("Bo Wren", "family", 30);   // past the first hour: people keep hours now
        for (int i = 0; i < 12; i++) { s.MessagesWaiting = 1; sim.TakeEnquiry(); }
        for (int i = 0; i < 6; i++) sim.OfferJob();
        int tomorrow = s.Day + 1;
        while (Clock.IsWeekend(tomorrow)) tomorrow++;
        var plan = Telo.Sim.Plan.PlanRules.AutoPlan(s, sim.Content, gen, tomorrow);

        Assert.NotEmpty(plan.Slots);
        int end = Telo.Sim.Plan.PlanRules.DayStarts;
        foreach (var slot in plan.Slots)
        {
            Assert.True(slot.Start >= end + slot.TravelMinutes - 1, "slots overlap or skip the travel");
            Assert.Null(slot.Grey);
            end = slot.Start + slot.Minutes;
        }
        Assert.True(end <= Telo.Sim.Plan.PlanRules.DayEnds);
        _out.WriteLine($"Jobs that fit in one day, alone: {plan.Slots.Count} " +
                       string.Join(", ", plan.Slots.Select(x => $"{Telo.Sim.Plan.PlanRules.Clock(x.Start)} {x.Title} ({x.Minutes}m, {x.TravelMinutes}m travel)")));

        // a family that's out at work: pinned for eleven on a weekday, it shows grey
        var away = Telo.Sim.Work.StaffRules.Tickets(s, sim.Content).FirstOrDefault(t => t.LotId is { } l && sim.Content.Lots[l].Archetype == "family");
        if (away is not null)
        {
            var sheet = Telo.Sim.Plan.PlanRules.PlanFor(s, tomorrow);
            sheet.Slots.Clear();
            sheet.Slots.Add(new Telo.Sim.Plan.PlanSlot { TicketKey = away.Key });
            Telo.Sim.Plan.PlanRules.Lay(s, sim.Content, gen, sheet);
            sheet.Slots[0].Start = 5 * 60;   // 11:00
            Assert.NotNull(Telo.Sim.Plan.PlanRules.AwayAt(s, sim.Content, sim.Content.Lots[away.LotId!], tomorrow, 5 * 60));
        }
    }
}

public class AssemblyTests
{
    private static readonly string[] Verbs = { "Undo", "Test", "Replace", "Do", "Fit" };

    [Fact]
    public void Every_assembly_makes_a_job_of_five_steps_or_fewer_and_the_same_job_has_the_same_fault()
    {
        foreach (var a in TestContent.Db.Assemblies.Values)
            for (ulong seed = 1; seed < 40; seed++)
            {
                var (steps, fault) = Telo.Sim.Minigames.Assembly.AssemblyRules.Steps(a, seed);
                Assert.InRange(steps.Count, 2, 5);
                Assert.All(steps, st => Assert.Contains(Verbs, v => st.Verb.StartsWith(v)));
                Assert.Equal(fault, Telo.Sim.Minigames.Assembly.AssemblyRules.Steps(a, seed).Fault);
                if (a.Mode == "repair") Assert.True(fault!.CanFail);
            }
        // different jobs find different faults (the radio has three things that can go)
        var radio = TestContent.Db.Assemblies["asm-radio"];
        var faults = Enumerable.Range(1, 30).Select(i => Telo.Sim.Minigames.Assembly.AssemblyRules.Steps(radio, (ulong)i).Fault!.Id).Distinct().Count();
        Assert.True(faults >= 2);
    }
}

public class NezTests
{
    [Fact]
    public void Nez_notices_competes_then_takes_a_house_a_week_but_never_the_last_house_on_the_road()
    {
        var sim = Simulation.NewGame(TestContent.Db, 21);
        var s = sim.State;
        Assert.Equal(Telo.Sim.Nez.NezStage.Background, Telo.Sim.Nez.NezRules.Stage(s));
        for (int i = 0; i < 10; i++) sim.AddCustomer($"Sub {i}", "family", 30);
        while (s.Day % 7 != 6) sim.AdvanceDay();
        sim.AdvanceDay();
        Assert.True(s.NezNoticed);
        Assert.Empty(s.NezLots);                                       // noticed, not yet taking houses
        for (int i = 0; i < 6; i++) sim.AddCustomer($"More {i}", "family", 30);
        // a line that stays down keeps your service short of a nine better than NezTel's, so nobody switches back
        // and Nez's own pace is what's measured here (switching is ServiceTests' job)
        s.Faults.Add(new Fault { Id = "fault-test", CustomerId = s.Customers[0].Id, Text = "a crackle", OpenedDay = s.Day });
        Assert.Equal(Telo.Sim.Nez.NezStage.Territory, Telo.Sim.Nez.NezRules.Stage(s));
        sim.AdvanceDays(7);
        Assert.Single(s.NezLots);                                      // slow: one a week
        sim.AdvanceDays(7 * 30);
        Assert.All(s.NezLots, id => Assert.Null(sim.Content.Lots[id].Premise));   // never a Promise Ledger home
        Assert.All(s.NezLots, id => Assert.Equal(LotKind.House, sim.Content.Lots[id].Kind));
        var taken = s.NezLots.First();
        Assert.Equal(KnockResult.AlreadySigned, sim.Knock(taken));
        Assert.DoesNotContain(TownRules.Prospects(s, sim.Content), l => l.Id == taken);
        Assert.True(Telo.Sim.Nez.NezRules.InstallPar > InstallJob.Steps.Sum(d => d.Clicks * d.MinutesPerClick));
        var back = Telo.Sim.Save.SaveCodec.FromJson(Telo.Sim.Save.SaveCodec.ToJson(s), sim.Content);
        Assert.Equal(s.NezLots, back.NezLots);
    }
}

public class BookingTests
{
    private static Simulation PastFirstHour(ulong seed)
    {
        var sim = Simulation.NewGame(TestContent.Db, seed);
        sim.AddCustomer("Ada Firth", "family", 30); sim.AddCustomer("Bo Wren", "family", 30);
        return sim;
    }

    [Fact]
    public void A_day_holds_three_jobs_and_a_long_wait_goes_to_NezTel()
    {
        var sim = PastFirstHour(5);
        var s = sim.State;
        for (int i = 0; i < BookingRules.PlayerCap; i++) { Assert.True(BookingRules.CanStartJob(s, sim.Content)); BookingRules.CountJob(s); }
        Assert.False(BookingRules.CanStartJob(s, sim.Content));          // the fourth waits
        sim.AdvanceDay();
        Assert.True(BookingRules.CanStartJob(s, sim.Content));           // a new day

        s.MessagesWaiting = 1; s.Modems = 0;
        var waiting = sim.TakeEnquiry()!;
        sim.AdvanceDays(BookingRules.GiveUpDays + 2);
        Assert.False(waiting.Live);
        Assert.Contains(waiting.LotId!, s.NezLots);
    }

    [Fact]
    public void Ads_bring_more_bookings_and_cost_money_every_week()
    {
        int Bookings(int ads, out long cash)
        {
            var sim = PastFirstHour(9);
            Assert.True(BookingRules.SetAds(sim.State, sim.Content, ads));
            int got = 0;
            for (int d = 0; d < 56; d++) { int before = sim.State.MessagesWaiting; sim.AdvanceDay(); got += Math.Max(0, sim.State.MessagesWaiting - before); sim.State.MessagesWaiting = 0; }
            cash = sim.State.Cash;
            return got;
        }
        int none = Bookings(0, out long cashNone), leaflets = Bookings(1, out long cashAds);
        Assert.True(leaflets > none, $"leaflets {leaflets} vs none {none}");
        Assert.True(cashAds < cashNone);
        Assert.False(BookingRules.SetAds(PastFirstHour(9).State, TestContent.Db, 3));   // radio isn't for sale yet
    }
}
