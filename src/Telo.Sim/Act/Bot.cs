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

        // 0. Bed by ten. A job under way may run late, to eleven at most (a half-done install keeps till morning).
        //    Every day ends in bed and starts there: staying up only borrows from tomorrow (Town/SleepRules).
        double hour = Clock.Hour(s);
        bool busy = sim.Installs.Count + sim.Benches.Count > 0;
        if ((hour >= 21.75 || hour < 6) && (!busy || hour >= 23 || hour < 6))
        {
            if (a.Holding != Tools.None) return (a.VanAt == a.Place ? $"stow:{a.Holding}" : $"put:{a.Holding}", "bedtime: hands free");
            return (a.Place == Places.Room ? "sleep" : Go(Places.Room), "bedtime");
        }

        // 1-2. Work under way (here, or somewhere we walked off from): carry on with it, fetching kit when a step needs it.
        string? job = sim.Installs.ContainsKey(a.Place) || sim.Benches.ContainsKey(a.Place) || TownRules.FaultAt(s, a.Place) is not null && sim.Installs.Count + sim.Benches.Count == 0
            ? a.Place : sim.Installs.Keys.Concat(sim.Benches.Keys).FirstOrDefault();
        if (job is not null)
        {
            // what the job needs next, judged as if we were standing there with what we hold
            var there = job == a.Place ? acts : Actions.For(sim, new ActorState { Id = a.Id, Place = job, VanAt = a.VanAt, Holding = a.Holding, Kit = a.Kit, IgnoreHours = a.IgnoreHours }, gen);
            var work = there.FirstOrDefault(x => x.Id.StartsWith("install:do:") || x.Id == "install:test" || x.Id == "install:sign"
                                                 || x.Id.StartsWith("bench:do") || x.Id == "bench:handback" || x.Id.StartsWith("repair:"));
            // the job needs a modem and there are none left (the technician used them): to the shop first
            if (work is not null && NeededKit(work.Blocked) == Tools.ModemBox && s.Modems <= 0
                && c.Lots.Values.FirstOrDefault(l => l.Kind == LotKind.Shop) is { } shop && s.Cash >= TownRules.ModemPrice)
            {
                if (a.Place != shop.Id) return (a.Holding != Tools.None && a.VanAt == a.Place ? $"stow:{a.Holding}" : Go(shop.Id), "out of modems: to the shop");
                if (acts.FirstOrDefault(x => x.Id == "buy:modem") is { } buy) return (buy.Ready ? buy.Id : "wait", buy.Ready ? "buy a modem" : $"buy a modem ({buy.Blocked})");
            }
            if (work is not null && NeededKit(work.Blocked) is { } need) return (Fetch(sim, a, gen, job, need, FullNeeds(sim, a, gen, job, work.Id)), $"fetch the {Actions.Cargo(need)}");
            if (job != a.Place)
            {
                bool useful = work is not null && (FullNeeds(sim, a, gen, job, work.Id) & a.Holding) != Tools.None;
                return (a.Holding != Tools.None && !useful && a.VanAt == a.Place ? $"stow:{a.Holding}" : Go(job), "back to the job");
            }
            if (work is { Ready: true }) return (work.Id, work.Label);
            if (work is not null) return ("wait", work.Blocked!);
        }

        // 3. See one thing through. A job we committed to keeps us until it's done or nobody's in for it.
        //    If the line points at a door nobody will answer for hours, don't stand on the step: take the most
        //    advanced job someone is in for, commit to it, and come back to the other later.
        var (text, lotId) = Guidance.Next(s, c);
        bool daytime = Clock.Hour(s) is >= 6 and < 17.5;
        bool booked = text.StartsWith("Booked");   // a booked window comes first: they're staying in for you
        if (!booked && Committed(sim, a) is { } focus) (text, lotId) = ($"{focus.Title}: see it through", focus.LotId);
        else if (!booked && daytime && !a.IgnoreHours && BookingRules.CanStartJob(s, c) && lotId is not null && c.Lots.TryGetValue(lotId, out var shut)
                 && HomeRules.WhyNot(s, c, shut) is not null)
        {
            if (MostAdvanced(sim, a, lotId) is { } alt)
            {
                a.Focus = alt.Key;
                (text, lotId) = ($"{alt.Title} ({shut.Resident} is out)", alt.LotId);
            }
            // nothing else open: find the next customer instead of standing on an empty doorstep
            else if (Onboarding.CanKnock(s, c) && TownRules.Prospects(s, c).FirstOrDefault(l => l.Id != lotId && l.Kind == LotKind.House && s.Day >= TownRules.HouseholdOf(s, l.Id).RetryDay
                         && HomeRules.WhyNot(s, c, l) is null) is { } door)
                (text, lotId) = ($"Knock on {door.Resident}'s door while {shut.Resident} is out", door.Id);
        }

        // at the shop anyway: buy enough modems for the connections waiting (a few at a time), not one per trip
        if (acts.FirstOrDefault(x => x.Id == "buy:modem" && x.Ready) is { } modem && s.Modems < Math.Min(4, s.PendingInstalls) && s.Cash >= 100)
            return (modem.Id, "stock up on modems while you're here");

        // 4. Follow the line everyone reads.
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
            // three jobs done today: the rest keeps till tomorrow
            if (hit.Blocked == BookingRules.CapReason) return (a.Place == Places.Room ? "sleep" : Go(Places.Room), $"{text} (done for today)");
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
            int n = LineRules.PoleSites(gen, p).Count;
            // work forward from the newest pole: a span waiting behind it is strung from here before moving on
            bool str = line.Strung < line.Planted - 1 || line.Planted >= n;
            return (Places.Pole(p.Id, str ? LineRules.StringSite(line.Strung, n) : line.Planted), new[] { str ? "pole:string" : "pole:plant" });
        }
        string[] room =
            text.Contains("answering machine") ? new[] { "messages" }
            : text.Contains("line card") ? new[] { "card:fit" }
            : text.Contains("switchboard is full") ? new[] { "switch:panel" }
            : text.Contains("switchboard") || text.Contains("Wire") ? new[] { "switch:read:", "switch:test:", "switch:jumper:", "switch:throw" }
            : text.Contains("ticket") ? new[] { "ticket:" }
            : text.Contains("Hire") ? new[] { "invest:install" }
            : text.Contains("Put up an ad") ? new[] { "ads:up" }
            : text.Contains("terminal") || text.Contains("uploads") ? new[] { "board:letin:", "board:mail", "board:file:" }
            : text.Contains("recall") ? new[] { "recall:" }
            : new[] { "sleep" };
        return (Places.Room, room);
    }

    // ---- committing to a job ----------------------------------------------------------------------
    /// <summary>The job this actor committed to, while it's still open and they're still in for it; otherwise let it go.</summary>
    private static Work.Ticket? Committed(Simulation sim, ActorState a)
    {
        if (a.Focus is null) return null;
        var t = Work.StaffRules.Tickets(sim.State, sim.Content).FirstOrDefault(x => x.Key == a.Focus);
        if (t is { AssignedTo: null, LotId: { } lot } && Plan.AppointmentRules.HouseWorkLeft(sim.State, t.Key)
            && (a.IgnoreHours || !sim.Content.Lots.TryGetValue(lot, out var def) || HomeRules.WhyNot(sim.State, sim.Content, def) is null))
            return t;
        a.Focus = null;
        return null;
    }

    /// <summary>
    /// The most advanced job someone is in for right now: a fault first (a customer without a working line), then a
    /// connection, then an order, then an odd job; longest waiting first. Never one that only needs the switchboard.
    /// </summary>
    private static Work.Ticket? MostAdvanced(Simulation sim, ActorState a, string notAt)
    {
        var s = sim.State; var c = sim.Content;
        int Rank(Work.TicketKind k) => k switch { Work.TicketKind.Repair => 0, Work.TicketKind.Install => 1, Work.TicketKind.Order => 2, _ => 3 };
        return Work.StaffRules.Tickets(s, c)
            .Where(t => t.AssignedTo is null && t.LotId is { } l && l != notAt && c.Lots.TryGetValue(l, out var lot)
                        && HomeRules.WhyNot(s, c, lot) is null && Plan.AppointmentRules.HouseWorkLeft(s, t.Key)
                        && (t.Kind != Work.TicketKind.Install || s.Modems > 0))
            .OrderBy(t => Rank(t.Kind)).ThenByDescending(t => t.WaitingDays).FirstOrDefault();
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
