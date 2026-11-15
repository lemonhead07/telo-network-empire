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
        var taken = Bot.Play(sim, me, gen, () => FarmServed(sim) || sim.State.Day > Simulation.StartDate.Day + 1100, 120000,
            (id, why) => { log.Add($"{sim.State.Date} {Clock.Text(sim.State)} {id}  ({why})"); if (sim.State.Day % 45 == 0 && !money.ContainsKey(sim.State.Day)) money[sim.State.Day] = $"{sim.State.Date}: ${sim.State.Cash} subs={sim.State.LiveSubscribers} staff={sim.State.StaffMembers.Count} act={sim.State.ActIndex} mailbill={sim.State.MailBillCents}"; });

        Assert.True(FarmServed(sim), $"farm line not built. at={me.Place} van={me.VanAt} holding={me.Holding} kit={string.Join(",", me.Kit.Select(k => k.Key + "@" + k.Value))} lines={string.Join(",", sim.State.Lines.Select(l => l.Key + ":" + l.Value.Planted + "/" + l.Value.Strung))}\nacts: {string.Join(" | ", Actions.For(sim, me, gen).Where(x => !x.Id.StartsWith("go:lot")).Select(x => x.Id + (x.Ready ? "" : "[" + x.Blocked + "]")))}\nmoney:\n{string.Join("\n", money.Values)}\nLast actions:\n" + string.Join("\n", log.TakeLast(6)));
        Assert.Equal(OnboardingStep.Free, Onboarding.Current(sim.State, sim.Content));
        Assert.Contains(taken, id => id == "messages");
        Assert.Contains(taken, id => id.StartsWith("install:do:"));
        Assert.Contains(taken, id => id == "switch:throw");
        Assert.Contains(taken, id => id.StartsWith("knock:"));
        Assert.Contains(taken, id => id == "pole:plant");
        Assert.True(sim.State.LiveSubscribers >= 4);
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
