using Telo.Sim.Build;
using Telo.Sim.Content;
using Telo.Sim.Ledger;
using Telo.Sim.Planet;
using Telo.Sim.Town;
using Telo.Sim.World;

namespace Telo.Sim.Act;

/// <summary>
/// A player made of rules: it reads the same "what next" line the player reads, then picks one action at a time from
/// <see cref="Actions.For"/>. No cheats: it walks, carries kit one piece at a time, waits for people to come home.
/// The script-player test, the on-screen autoplay and (later) Nez all use it.
/// </summary>
public static class Bot
{
    /// <summary>The next action this actor should take, and why (the line it is following).</summary>
    public static (string ActionId, string Why) Choose(Simulation sim, ActorState a, PlanetGen gen)
    {
        var acts = Actions.For(sim, a, gen);
        var s = sim.State; var c = sim.Content;
        string Go(string place) => $"go:{place}";

        // 1-2. Work under way (here, or somewhere we walked off from): carry on with it, fetching kit when a step needs it.
        string? job = sim.Installs.ContainsKey(a.Place) || sim.Benches.ContainsKey(a.Place) || TownRules.FaultAt(s, a.Place) is not null && sim.Installs.Count + sim.Benches.Count == 0
            ? a.Place : sim.Installs.Keys.Concat(sim.Benches.Keys).FirstOrDefault();
        if (job is not null)
        {
            // what the job needs next, judged as if we were standing there with what we hold
            var there = job == a.Place ? acts : Actions.For(sim, new ActorState { Id = a.Id, Place = job, VanAt = a.VanAt, Holding = a.Holding, Kit = a.Kit, IgnoreHours = a.IgnoreHours }, gen);
            var work = there.FirstOrDefault(x => x.Id.StartsWith("install:do:") || x.Id == "install:test" || x.Id == "install:sign"
                                                 || x.Id.StartsWith("bench:do") || x.Id == "bench:handback" || x.Id.StartsWith("repair:"));
            if (work is not null && NeededKit(work.Blocked) is { } need) return (Fetch(sim, a, gen, job, need, FullNeeds(sim, a, gen, job, work.Id)), $"fetch the {Actions.Cargo(need)}");
            if (job != a.Place)
            {
                bool useful = work is not null && (FullNeeds(sim, a, gen, job, work.Id) & a.Holding) != Tools.None;
                return (a.Holding != Tools.None && !useful && a.VanAt == a.Place ? $"stow:{a.Holding}" : Go(job), "back to the job");
            }
            if (work is { Ready: true }) return (work.Id, work.Label);
            if (work is not null) return ("wait", work.Blocked!);
        }

        // 4. Follow the line everyone reads.
        var (text, lotId) = Guidance.Next(s, c);
        var (place, want) = Target(sim, gen, text, lotId);
        // nobody in over there and it's night: sleep instead of walking over to a dark door
        if (place != a.Place && !a.IgnoreHours && c.Lots.TryGetValue(place, out var dest) && HomeRules.WhyNot(s, c, dest) is not null
            && (Clock.Hour(s) >= 17.5 || Clock.Hour(s) < 6) && sim.Installs.Count + sim.Benches.Count == 0)
            return (a.Place == Places.Room ? "sleep" : Go(Places.Room), $"{text} (closed till morning)");
        if (place != a.Place)
        {
            // tidy up before leaving: kit lying here goes back on the van
            if (a.Kit.Any(k => k.Value == a.Place) && a.VanAt == a.Place && !NeededHere(sim, a, gen))
                return (a.Holding != Tools.None ? $"stow:{a.Holding}" : $"take:{a.Kit.First(k => k.Value == a.Place).Key}", "pack the van");
            if (a.Holding != Tools.None && a.VanAt == a.Place) return ($"stow:{a.Holding}", "pack the van");
            // far away and the van is nearer than the job: fetch the van first
            if (a.Holding == Tools.None && a.VanAt != a.Place && Places.DistanceM(gen, c, a.Place, place) > Places.DriveOverM
                && Places.DistanceM(gen, c, a.Place, a.VanAt) < Places.DistanceM(gen, c, a.Place, place) / 2)
                return (Go(a.VanAt), "walk to the van");
            return (Go(place), text);
        }
        // here: the most specific thing that matches what the line asks for
        var hits = want.SelectMany(prefix => acts.Where(x => x.Id.StartsWith(prefix) && !x.Id.StartsWith("go:"))).ToList();
        if (hits.FirstOrDefault(x => x.Ready) is { } ready) return (ready.Id, text);
        if (hits.FirstOrDefault(x => NeededKit(x.Blocked) is not null) is { } kitless)
        {
            var need = NeededKit(kitless.Blocked)!.Value;
            return (Fetch(sim, a, gen, a.Place, need, FullNeeds(sim, a, gen, a.Place, kitless.Id)), $"fetch the {Actions.Cargo(need)}");
        }
        if (hits.FirstOrDefault() is { } hit)
        {
            // nobody in and it's evening: go home to bed rather than wait on the step all night
            if (Clock.Hour(s) >= 17.5 || Clock.Hour(s) < 6) return (a.Place == Places.Room ? "sleep" : Go(Places.Room), $"{text} ({hit.Blocked}): home to bed");
            return ("wait", $"{text} ({hit.Blocked})");
        }
        return (a.Place == Places.Room ? "sleep" : Go(Places.Room), text);
    }

    /// <summary>Play until <paramref name="done"/> or the action budget runs out. Returns the actions taken, newest last.</summary>
    public static List<string> Play(Simulation sim, ActorState a, PlanetGen gen, Func<bool> done, int maxActions = 20000, Action<string, string>? log = null)
    {
        var taken = new List<string>();
        for (int i = 0; i < maxActions && !done(); i++)
        {
            var (id, why) = Choose(sim, a, gen);
            if (!Actions.Perform(sim, a, gen, id)) Actions.Perform(sim, a, gen, "wait");
            taken.Add(id);
            log?.Invoke(id, why);
        }
        return taken;
    }

    /// <summary>Where the "what next" line points, and the actions (by id prefix) that answer it there.</summary>
    public static (string Place, string[] Want) Target(Simulation sim, PlanetGen gen, string text, string? lotId)
    {
        var s = sim.State; var c = sim.Content;
        if (lotId is not null)
            return (lotId, text.Contains("modem") && text.Contains("Buy") ? new[] { "buy:modem" }
                : text.Contains("line card") ? new[] { "buy:card" }
                : new[] { "repair:", "install:", "socket:", "order:", "oddjob:", "knock:", "lease" });
        if (text.Contains("Pole line"))
        {
            var p = c.Premises.Values.First(p => p.PoleLine is not null && LedgerRules.StatusOf(s, p.Id) != PromiseStatus.Served && p.Act <= s.ActIndex + 1);
            var line = LineRules.ProgressOf(s, p.Id);
            bool str = line.Strung < line.Planted - 1 || line.Planted >= LineRules.PoleSites(gen, p).Count;   // every pole in: string the rest
            return (Places.Pole(p.Id, str ? line.Strung : line.Planted), new[] { str ? "pole:string" : "pole:plant" });
        }
        string[] room =
            text.Contains("answering machine") ? new[] { "messages" }
            : text.Contains("line card") ? new[] { "card:fit" }
            : text.Contains("switchboard is full") ? new[] { "switch:panel" }
            : text.Contains("switchboard") || text.Contains("Wire") ? new[] { "switch:read:", "switch:test:", "switch:jumper:", "switch:throw" }
            : text.Contains("ticket") ? new[] { "ticket:" }
            : text.Contains("Hire") ? new[] { "invest:install" }
            : text.Contains("terminal") || text.Contains("uploads") ? new[] { "board:letin:", "board:mail", "board:file:" }
            : text.Contains("recall") ? new[] { "recall:" }
            : new[] { "sleep" };
        return (Places.Room, room);
    }

    // ---- kit ---------------------------------------------------------------------------------------
    /// <summary>The first missing piece of kit named in a "what's stopping it" line, if that's the problem.</summary>
    private static Tools? NeededKit(string? blocked)
    {
        if (blocked is null) return null;
        foreach (var t in ActorState.AllKit) if (blocked.Contains(Actions.Cargo(t))) return t;
        return null;
    }

    /// <summary>Every piece of kit an action needs, judged with empty hands and nothing put down.</summary>
    private static Tools FullNeeds(Simulation sim, ActorState a, PlanetGen gen, string place, string actionId) =>
        AllNeeded(Actions.For(sim, new ActorState { Id = a.Id, Place = place, VanAt = a.VanAt, IgnoreHours = a.IgnoreHours }, gen)
            .FirstOrDefault(x => x.Id == actionId)?.Blocked);

    private static Tools AllNeeded(string? blocked) =>
        blocked is null ? Tools.None : ActorState.AllKit.Where(t => blocked.Contains(Actions.Cargo(t))).Aggregate(Tools.None, (x, t) => x | t);

    private static bool NeededHere(Simulation sim, ActorState a, PlanetGen gen) =>
        sim.Installs.ContainsKey(a.Place) || sim.Benches.ContainsKey(a.Place) || TownRules.FaultAt(sim.State, a.Place) is not null;

    /// <summary>One move towards having <paramref name="tool"/> lying at <paramref name="at"/>.</summary>
    public static string Fetch(Simulation sim, ActorState a, PlanetGen gen, string at, Tools tool, Tools alsoNeeded = Tools.None)
    {
        // something the job needs is already in our hands: take it there first
        if (a.Holding == tool || (a.Holding != Tools.None && (alsoNeeded & a.Holding) == a.Holding)) return a.Place == at ? $"put:{a.Holding}" : $"go:{at}";
        if (a.Kit.TryGetValue(tool, out var lying))
        {
            if (a.Place != lying) return a.Holding != Tools.None ? $"put:{a.Holding}" : $"go:{lying}";
            return a.Holding != Tools.None ? $"put:{a.Holding}" : $"take:{tool}";
        }
        // on the van
        if (a.VanAt == at)
        {
            if (a.Place != at) return a.Holding != Tools.None ? $"put:{a.Holding}" : $"go:{at}";
            return a.Holding != Tools.None ? $"put:{a.Holding}" : $"take:{tool}";
        }
        // the van is somewhere else: bring it here (empty hands drive)
        if (a.Place == a.VanAt)
        {
            if (a.Holding != Tools.None) return $"stow:{a.Holding}";
            bool near = Places.DistanceM(gen, sim.Content, a.Place, at) <= Places.DriveOverM;
            return near ? $"take:{tool}" : $"go:{at}";   // close enough to carry; otherwise drive the van over
        }
        return a.Holding != Tools.None ? $"put:{a.Holding}" : $"go:{a.VanAt}";
    }
}
