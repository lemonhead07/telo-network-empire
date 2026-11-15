using Telo.Sim.Build;
using Telo.Sim.Content;
using Telo.Sim.Ledger;
using Telo.Sim.Minigames.Install;
using Telo.Sim.Minigames.OddJob;
using Telo.Sim.Net;
using Telo.Sim.Planet;
using Telo.Sim.Town;
using Telo.Sim.Work;
using Telo.Sim.World;

namespace Telo.Sim.Act;

/// <summary>
/// One thing an actor could do now. <see cref="Blocked"/> says, in plain words, what is stopping it (null when it can be done).
/// <see cref="Minutes"/> is how long it takes the player; staff and Nez scale it.
/// </summary>
public sealed record GameAction(string Id, string Label, int Minutes, string? Blocked, Action Run, ActionScale Scale = ActionScale.Task)
{
    public bool Ready => Blocked is null;
}

/// <summary>How big an action is: a two-minute clip, a half-day job, or a weeks-long project a crew holds (AGES.md §6).</summary>
public enum ActionScale { Task, Job, Project }

/// <summary>
/// The one question every player, test, member of staff and opponent asks: "what can this actor do now, and what is stopping the rest?"
/// The rules stay where they were (TownRules, SwitchRules, InstallJob…); this is the single list of verbs over them.
/// </summary>
public static class Actions
{
    public static List<GameAction> For(Simulation sim, ActorState a, PlanetGen gen)
    {
        var list = new List<GameAction>();
        var s = sim.State; var c = sim.Content;
        var here = a.Place;
        var onSite = a.OnSite();

        // ---- moving about -------------------------------------------------------------------
        foreach (var place in Destinations(sim, gen).Concat(a.Kit.Values).Append(a.VanAt).Distinct())
        {
            if (place == here) continue;
            double m = Places.DistanceM(gen, c, here, place);
            bool drive = a.Holding == Tools.None && a.VanAt == here && m > Places.DriveOverM;
            int min = drive ? Places.DriveMinutes(m, a.Vehicle) : Places.WalkMinutes(m);
            list.Add(new($"go:{place}", $"{(drive ? "Drive" : "Walk")} to {Name(c, place)}", min, null, () =>
            {
                if (drive) a.VanAt = place;
                a.Place = place;
            }));
        }

        // ---- kit: one thing in your hands at a time ---------------------------------------------
        foreach (var t in ActorState.AllKit)
        {
            bool onVan = !a.Kit.ContainsKey(t) && a.Holding != t;
            bool lyingHere = a.Kit.TryGetValue(t, out var at) && at == here;
            if ((onVan && a.VanAt == here && Stocked(sim, t)) || lyingHere)
                list.Add(new($"take:{t}", $"Pick up the {Cargo(t)}", 1, a.Holding != Tools.None ? "your hands are full" : null, () =>
                {
                    a.Kit.Remove(t); a.Holding = t;
                }));
        }
        if (a.Holding is var held and not Tools.None)
        {
            list.Add(new($"put:{held}", $"Put the {Cargo(held)} down here", 1, null, () => { a.Kit[held] = here; a.Holding = Tools.None; }));
            if (a.VanAt == here)
                list.Add(new($"stow:{held}", $"Put the {Cargo(held)} back on the van", 1, null, () => { a.Kit.Remove(held); a.Holding = Tools.None; }));
        }

        list.Add(new("wait", "Wait half an hour", 30, null, () => { }));
        list.Add(new("phone:night", "Text PIP goodnight (ends the day)", 0, Clock.Hour(s) >= 17 || Clock.Hour(s) < 6 ? null : "it's still the working day",
            () => Phone.PhoneRules.SendNight(sim)));
        if (here == Places.Room) Room(sim, a, list, onSite);
        else if (c.Lots.TryGetValue(here, out var lot)) Lot(sim, a, lot, list, onSite);
        else if (Places.IsPole(here, out var pid, out int k) && c.Premises.TryGetValue(pid, out var premise)) Pole(sim, gen, premise, k, list, onSite, a);
        return list;
    }

    /// <summary>Do it, if nothing is stopping it. The clock moves by the action's minutes unless the presentation lives the time itself.</summary>
    public static bool Perform(Simulation sim, ActorState a, PlanetGen gen, string id, bool advanceClock = true)
    {
        var act = For(sim, a, gen).FirstOrDefault(x => x.Id == id);
        if (act is not { Ready: true }) return false;
        act.Run();
        if (advanceClock && act.Minutes > 0) sim.AdvanceMinutes(act.Minutes);
        return true;
    }

    /// <summary>Why can't this be done? Null if it can (or if there is no such action here).</summary>
    public static string? WhyNot(Simulation sim, ActorState a, PlanetGen gen, string id) =>
        For(sim, a, gen).FirstOrDefault(x => x.Id == id)?.Blocked;

    // ---- the room: answering machine, switchboard, notebook, bed, rail, rack -------------------
    private static void Room(Simulation sim, ActorState a, List<GameAction> list, Tools onSite)
    {
        var s = sim.State; var c = sim.Content;
        list.Add(new("messages", s.MessagesWaiting > 0 ? $"Play messages ({s.MessagesWaiting} waiting)" : "No new messages", 2,
            s.MessagesWaiting > 0 ? null : "no messages", () => sim.TakeEnquiry()));

        // the switchboard: read smudged tags, test ports, jumper, throw the test switch
        foreach (var p in SwitchRules.Pending(s))
        {
            var who = s.Customers.First(x => x.Id == p.CustomerId).Name;
            if (!p.TagRead) list.Add(new($"switch:read:{p.CustomerId}", $"Tone-probe {who}'s smudged tag", 3, null, () => SwitchRules.ReadTag(s, p.CustomerId)));
            if (p.Jumper < 0)
            {
                if (SwitchRules.KnownGood(s, p.AssignedPort) is null)
                    list.Add(new($"switch:test:{p.AssignedPort}", $"Test port {p.AssignedPort + 1}", 2, null, () => SwitchRules.TestPort(s, p.AssignedPort)));
                int port = SwitchRules.KnownGood(s, p.AssignedPort) == false
                    ? SwitchRules.SparePorts.Where(i => SwitchRules.OnPort(s, i) is null).DefaultIfEmpty(-1).First() : p.AssignedPort;
                string? why = !p.TagRead ? "the tag is smudged: tone-probe it first" : port < 0 ? "no spare port left" : null;
                list.Add(new($"switch:jumper:{p.CustomerId}", $"Jumper {who} to port {port + 1}", 3, why, () => SwitchRules.Jumper(s, p.CustomerId, port)));
            }
        }
        if (SwitchRules.Pending(s).Any(p => p.Jumper >= 0))
            list.Add(new("switch:throw", "Throw the test switch", 1, null, () => sim.ThrowSwitch()));

        // the notebook: hire help
        foreach (var w in c.Work.Keys)
            if (WorkRules.Next(s, c, w) is { } next)
            {
                var can = WorkRules.CanUpgrade(s, c, w);
                list.Add(new($"invest:{w}", $"{c.Work[w].Name}: {next.Description} (${next.Cost})", 5, can == UpgradeResult.Ok ? null : can.ToString(), () => WorkRules.Upgrade(s, c, w)));
            }

        // tickets go from the rail into a person's hand
        foreach (var m in s.StaffMembers.Where(m => m.Does.Count > 0))
            foreach (var t in StaffRules.Tickets(s, c).Where(t => t.AssignedTo is null && m.Does.Contains(t.Kind)))
                list.Add(new($"ticket:{t.Key}:{m.Id}", $"Hand {m.Name.Split(' ')[0]} the ticket: {t.Title}", 2,
                    s.HasDepot ? "staff collect their own tickets at the depot" : StaffRules.CanTake(s, c, m, t) ? null : $"{m.Name.Split(' ')[0]} has a full day already",
                    () => StaffRules.Assign(s, c, t.Key, m.Id)));

        if (s.LineCards > 0)
            list.Add(new("card:fit", "Slot the line card into the rack", BoardRules.FitMinutes,
                (onSite & BoardRules.FitNeeds) == BoardRules.FitNeeds ? null : "the line card is still on the van", () =>
                {
                    BoardRules.FitCard(s, onSite);
                    if (a.Holding == Tools.LineCard) a.Holding = Tools.None; else a.Kit.Remove(Tools.LineCard);
                }));

        // the Board's terminal (number keys in the game)
        if (BoardService.TerminalOpen(s) && !BoardService.Delegated(s, c))
        {
            foreach (var w in BoardService.Waiting(s))
                list.Add(new($"board:letin:{w.Id}", $"Let {w.Name} in on the Board", BoardService.LetInMinutes, null, () => BoardService.LetIn(s, w.Id)));
            if (BoardService.MailOpen(s) && !BoardService.MailRunToday(s))
                list.Add(new("board:mail", "Run tonight's mail call", BoardService.MailSetupMinutes + 20, null, () => BoardService.RunMail(s)));
            foreach (var f in BoardService.NewFiles(s))
                list.Add(new($"board:file:{f.Id}", $"Sort the upload \"{f.Title}\"", BoardService.FileMinutes, null,
                    () => BoardService.Decide(s, f.Id, f.Kind == FileKind.Useful || f.Kind == FileKind.Fun ? FileStatus.Kept : FileStatus.Hidden)));
        }
        // PIP's Almanac: remember the future
        foreach (var e in c.Almanac.Values.Where(e => s.Entries.GetValueOrDefault(e.Id) == EntryStatus.Readable))
            list.Add(new($"recall:{e.Id}", $"Recall \"{e.Title}\" ({e.ArchiveCost} Archive)", 5, s.Archive >= e.ArchiveCost ? null : "not enough Archive",
                () => Almanac.AlmanacRules.Recall(s, c, e.Id)));
        if (SwitchRules.Unseated(s).Count > 0 && SwitchRules.Full(s))
            list.Add(new("switch:panel", $"Bolt on another panel (${SwitchRules.PanelPrice})", 20, SwitchRules.CanAddPanel(s) ? null : "not enough cash", () => SwitchRules.AddPanel(s)));

        // the ad board: one lever
        if (s.AdLevel < BookingRules.MaxAdLevel(s, c))
            list.Add(new("ads:up", $"Advertise: {BookingRules.Ads[s.AdLevel + 1].Name} (${BookingRules.Ads[s.AdLevel + 1].WeeklyCost} a week)", 2, null, () => BookingRules.SetAds(s, c, s.AdLevel + 1)));
        if (s.AdLevel > 0)
            list.Add(new("ads:down", $"Cancel {BookingRules.Ads[s.AdLevel].Name}", 2, null, () => BookingRules.SetAds(s, c, s.AdLevel - 1)));

        list.Add(new("sleep", Guidance.IsQuiet(s, c) ? "Sleep until something needs you" : "Sleep until morning", 0, null, () =>
        {
            if (Guidance.IsQuiet(s, c)) sim.RestUntilNeeded(); else sim.Sleep();
        }));
    }

    // ---- at someone's door, or inside their house -------------------------------------------
    private static void Lot(Simulation sim, ActorState a, LotDef lot, List<GameAction> list, Tools onSite)
    {
        var s = sim.State; var c = sim.Content;
        bool house = lot.Kind == LotKind.House;

        // work already under way here: an install, or something on the rug
        if (sim.Installs.TryGetValue(lot.Id, out var job)) { InstallSteps(sim, job, list, onSite); return; }
        if (sim.Benches.TryGetValue(lot.Id, out var bench)) { BenchSteps(sim, lot, bench, list, onSite); return; }

        if (lot.Kind == LotKind.Shop)
        {
            string? shut = !a.IgnoreHours && HomeRules.ShopShut(s, c, lot) ? HomeRules.WhyNot(s, c, lot) : null;
            list.Add(new("buy:modem", $"Buy a modem (${TownRules.ModemPrice})", 5, shut ?? (s.Cash < TownRules.ModemPrice ? $"a modem is ${TownRules.ModemPrice}; you have ${s.Cash}" : null),
                () => TownRules.BuyModem(s)));
            if (BoardRules.CanAddLine(s))
                list.Add(new("buy:card", $"Buy a line card for the Board (${BoardRules.CardPrice})", 5, shut ?? (s.Cash < BoardRules.CardPrice ? "not enough cash" : null),
                    () => BoardRules.BuyCard(s)));
        }
        if (lot.Kind == LotKind.Depot)
        {
            list.Add(new("lease", $"Sign the lease on {lot.Resident} (${StaffRules.DepotDeposit} down)", 10,
                StaffRules.CanLeaseDepot(s) ? null : s.HasDepot ? "already yours" : "not yet: no staff, or not enough cash", () => StaffRules.LeaseDepot(s)));
            return;
        }

        // nobody answers when nobody is in
        string? away = a.IgnoreHours ? null : HomeRules.WhyNot(s, c, lot);
        string? capped = BookingRules.CanStartJob(s, c) ? null : BookingRules.CapReason;

        if (TownRules.FaultAt(s, lot.Id) is { } fault)
        {
            list.Add(new($"repair:{fault.Id}", $"Fix the line: {fault.Text}", TownRules.RepairMinutes,
                away ?? capped ?? ((onSite & TownRules.RepairNeeds) == TownRules.RepairNeeds ? null : "bring the toolbox from the van"),
                () => { if (TownRules.Repair(s, fault.Id, onSite)) BookingRules.CountJob(s); }));
            return;
        }
        if (house && OrderRules.OrderAt(s, lot.Id) is { } order && c.Products.TryGetValue(order.ProductId, out var product))
            list.Add(new($"order:{order.Id}", $"Fit the {product.Name.ToLowerInvariant()} they ordered", 1, away ?? capped,
                () => { sim.Benches[lot.Id] = new Bench(OrderRules.NewTask(c, order), "order", order.Id); BookingRules.CountJob(s); }));
        if (TownRules.JobAt(s, lot.Id) is { } odd)
            list.Add(house
                ? new($"oddjob:{odd.Id}", $"{odd.Title} (+${odd.Pay}): they let you in", 1, away ?? capped,
                    () => { if (sim.NewOddJobTask(odd.Id) is { } t) { sim.Benches[lot.Id] = new Bench(t, "job", odd.Id); BookingRules.CountJob(s); } })
                : new($"oddjob:{odd.Id}", $"{odd.Title} (+${odd.Pay})", odd.Hours * 60, away ?? capped, () => { TownRules.CompleteJob(s, c, odd.Id); BookingRules.CountJob(s); }));

        var cust = TownRules.CustomerAt(s, lot.Id);
        if (cust is { SocketFitted: false })
        {
            string? why = away ?? capped ?? (s.Modems <= 0 ? "you have no modems (Sol's Electronics sells them)" : null);
            if (house) list.Add(new($"install:{cust.Id}", $"Start {cust.Name}'s install", 1, why,
                () => { if (sim.NewInstallJob(cust.Id) is { } j) { sim.Installs[lot.Id] = j; BookingRules.CountJob(s); } }));
            else list.Add(new($"socket:{cust.Id}", $"Fit the wall socket for {cust.Name}", 30, why, () => { if (TownRules.FitSocket(s, cust.Id)) BookingRules.CountJob(s); }));
        }
        if (cust is null && lot.Premise is null && lot.Kind != LotKind.Shop)
        {
            var h = TownRules.HouseholdOf(s, lot.Id);
            string? why = s.NezLots.Contains(lot.Id) ? "they signed with the national phone monopoly"
                : !Onboarding.CanKnock(s, c) ? "not yet: see one customer through first"
                : s.Day < h.RetryDay ? (h.Status == HouseholdStatus.Declined ? "they said no; try again later" : "nobody home; try tomorrow") : away;
            list.Add(new($"knock:{lot.Id}", $"Knock on {lot.Resident}'s door", 5, why, () => sim.Knock(lot.Id)));
        }
    }

    private static void InstallSteps(Simulation sim, InstallJob job, List<GameAction> list, Tools onSite)
    {
        if (job.NextStep is { } step)
        {
            var d = InstallJob.Def(step);
            var missing = job.Missing(step, onSite);
            list.Add(new($"install:do:{step}", $"{d.Verb} ({job.Progress(step) + 1} of {d.Clicks})", d.MinutesPerClick,
                missing == Tools.None ? null : $"you need the {Names(missing)} here", () => { if (job.Do(step, onSite) && sim.TiredSlip()) job.Slip(step); sim.RecordInstall(job); }));
            if (d.CanRush)
                list.Add(new($"install:bodge:{step}", $"Bodge the {d.Title.ToLowerInvariant()}", InstallJob.RushMinutes,
                    job.Missing(step, onSite, rush: true) == Tools.None ? null : "nothing to bodge it with", () => { job.Rush(step, onSite); sim.RecordInstall(job); }));
            return;
        }
        if (!job.Tested || job.KnownFault is not null)
            list.Add(new("install:test", "Lift the handset and listen for dial tone", InstallJob.TestMinutes, null, () => job.Test()));
        if (job.Tested && job.KnownFault is null)
            list.Add(new("install:sign", "Sign the job sheet", 2, null, () =>
            {
                if (sim.ApplyInstallOutcome(job.Evaluate())) foreach (var k in sim.Installs.Where(x => x.Value == job).Select(x => x.Key).ToList()) sim.Installs.Remove(k);
            }));
    }

    private static void BenchSteps(Simulation sim, LotDef lot, Bench bench, List<GameAction> list, Tools onSite)
    {
        var t = bench.Task;
        if (!t.Done)
        {
            var d = t.Next!;
            var missing = t.Missing(onSite);
            list.Add(new("bench:do", $"{d.Verb} ({t.NextProgress + 1} of {d.Clicks})", t.MinutesFor(d), missing == Tools.None ? null : $"you need the {Names(missing)} here", () => t.Do(onSite)));
            list.Add(new("bench:thump", "Give it a thump and call it done", OddJobTask.BodgeMinutes, null, () => t.Bodge()));
            return;
        }
        list.Add(new("bench:handback", $"Hand it back to {lot.Resident}", 2, null, () =>
        {
            if (bench.Kind == "order") sim.CompleteOrder(bench.RefId, t.Bodged);
            else TownRules.CompleteJob(sim.State, sim.Content, bench.RefId, t.Bodged);
            sim.Benches.Remove(lot.Id);
        }));
    }

    // ---- a pole site along a road -------------------------------------------------------------
    private static void Pole(Simulation sim, PlanetGen gen, PremiseDef p, int k, List<GameAction> list, Tools onSite, ActorState a)
    {
        var s = sim.State;
        var line = LineRules.ProgressOf(s, p.Id);
        if (k == line.Planted && LineRules.CanPlant(s, gen, p))
            list.Add(new("pole:plant", $"Plant pole {k + 1} (${p.PoleLine!.PoleCost})", LineRules.PlantMinutes,
                s.Cash < p.PoleLine.PoleCost ? "not enough cash for a pole" : (onSite & LineRules.PlantNeeds) == LineRules.PlantNeeds ? null : "bring a pole from the van",
                () =>
                {
                    if (!LineRules.Plant(s, gen, p, onSite)) return;
                    if (a.Holding == Tools.Pole) a.Holding = Tools.None; else a.Kit.Remove(Tools.Pole); // the next pole is on the rack
                }));
        if (k == LineRules.StringSite(line.Strung, LineRules.PoleSites(gen, p).Count) && LineRules.CanString(s, gen, p))
            list.Add(new("pole:string", $"String span {k + 1}", LineRules.StringMinutes,
                (onSite & LineRules.StringNeeds) == LineRules.StringNeeds ? null : $"you need the {Names(LineRules.StringNeeds & ~onSite)} here",
                () => LineRules.String(s, sim.Content, gen, p, onSite)));
    }

    // ---- helpers ---------------------------------------------------------------------------------
    /// <summary>Places worth going to now: home, every building, and the next pole sites of any line being built.</summary>
    public static IEnumerable<string> Destinations(Simulation sim, PlanetGen gen)
    {
        yield return Places.Room;
        foreach (var id in sim.Content.Lots.Keys) yield return id;
        foreach (var p in sim.Content.Premises.Values.Where(p => p.PoleLine is not null))
        {
            int n = LineRules.PoleSites(gen, p).Count;
            if (n == 0) continue;
            var line = LineRules.ProgressOf(sim.State, p.Id);
            if (line.Planted < n) yield return Places.Pole(p.Id, line.Planted);
            if (LineRules.CanString(sim.State, gen, p)) yield return Places.Pole(p.Id, LineRules.StringSite(line.Strung, n));
        }
    }

    /// <summary>The boxed modem and line cards are only on the van while there is stock for them.</summary>
    private static bool Stocked(Simulation sim, Tools t) => t switch
    {
        Tools.ModemBox => sim.State.Modems - sim.Installs.Values.Count(j => j.IsDone(InstallStep.Modem)) > 0,
        Tools.LineCard => sim.State.LineCards > 0,
        _ => true,
    };

    public static string Name(ContentDb c, string place) =>
        place == Places.Room ? "your room" : c.Lots.TryGetValue(place, out var l) ? $"{l.Resident}'s" : Places.IsPole(place, out var pid, out int k) ? $"pole site {k + 1}" : place;

    public static string Cargo(Tools t) => t switch
    {
        Tools.CableDrum => "cable drum", Tools.Ladder => "ladder", Tools.Toolbox => "toolbox", Tools.ModemBox => "boxed modem", Tools.Pole => "pole", Tools.LineCard => "line card", _ => "kit",
    };

    public static string Names(Tools set) => string.Join(" and ", ActorState.AllKit.Where(t => (set & t) != 0).Select(Cargo));
}

/// <summary>Something on the rug in a customer's front room: an odd job or a catalogue order, worked step by step.</summary>
public sealed record Bench(OddJobTask Task, string Kind, string RefId);
