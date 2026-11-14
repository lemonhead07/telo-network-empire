using Telo.Sim.Almanac;
using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.Ledger;
using Telo.Sim.Build;
using Telo.Sim.Minigames.Install;
using Telo.Sim.Minigames.OddJob;
using Telo.Sim.Net;
using Telo.Sim.Planet;
using Telo.Sim.Town;
using Telo.Sim.Work;

namespace Telo.Sim.World;

/// <summary>Facade the presentation layer talks to. All mutation goes through here or the *Rules classes.</summary>
public sealed class Simulation
{
    public ContentDb Content { get; }
    public GameState State { get; }
    private readonly DetRng _rng;

    public static readonly GameDate StartDate = GameDate.From(1983, 1, 3);
    public const long StartingCash = 400;
    public const long RentPerMonth = 120;
    /// <summary>A new line pays for its modem (sold at cost) and its first month, the day it goes live.</summary>
    public static long ConnectionFee(Customer c) => TownRules.ModemPrice + c.MonthlyFee;

    public Simulation(ContentDb content, GameState state)
    {
        Content = content;
        State = state;
        _rng = new DetRng(state.RngState);
        AlmanacRules.RefreshStatuses(State, Content, quiet: State.Entries.Count == 0); // derived state is recomputed, never trusted from disk
    }

    public static Simulation NewGame(ContentDb content, ulong seed)
    {
        var s = new GameState { Seed = seed, RngState = seed, Day = StartDate.Day, Cash = StartingCash, MessagesWaiting = 1 };
        foreach (var w in content.Work.Keys) s.WorkStage[w] = 0;
        s.Emit(EventKind.Info, "PIP: \"…signal. Low battery. Year reads 1983. That can't be right.\"");
        var sim = new Simulation(content, s);
        return sim; // no odd jobs yet: a new player meets one thing at a time (see Town/Onboarding)
    }

    public void AdvanceDays(int days) { for (int i = 0; i < days; i++) AdvanceDay(); }

    public void AdvanceDay()
    {
        var s = State;
        s.Day++;
        AlmanacRules.Tick(s, Content);
        LedgerRules.Tick(s, Content);
        BoardRules.ApplyEvening(s, Content, s.Day - 1);   // last night at the Board
        BoardService.EndOfDay(s, s.Day - 1);
        BoardService.CoSysopDay(s, Content);
        if (s.Day % 7 == 0) BoardService.Week(s, Content, _rng);
        TownRules.Tick(s, Content, _rng);
        OrderRules.Tick(s, Content);
        SwitchRules.Tidy(s);
        StaffRules.EndOfDay(s, Content, _rng, s.Day - 1);
        SurfaceDefects();
        DriftSatisfaction();
        // Word of mouth: happy subscribers bring enquiries.
        int happy = s.Customers.Count(c => c.Live && c.Installed && c.Satisfaction >= 60);
        if (Onboarding.PhoneRings(s, Content) && s.MessagesWaiting < 9 && _rng.ChancePermille(Math.Min(900, 250 + 40 * happy + 8 * BoardService.Appeal(s)))) s.MessagesWaiting++;
        if (s.Date.IsFirstOfMonth) MonthEnd();
        while (ActRules.TryAdvance(s, Content)) AlmanacRules.RefreshStatuses(s, Content);
        s.RngState = _rng.State;
    }

    private void MonthEnd()
    {
        var s = State;
        long revenue = s.Customers.Where(c => c.Live && c.Installed).Sum(c => c.MonthlyFee);
        long costs = RentPerMonth + (s.HasDepot ? StaffRules.DepotRentPerMonth : 0) + WorkRules.MonthlyUpkeep(s, Content) + BoardRules.MonthlyRental(s) + BoardService.TakeMonthlyBill(s);
        s.RevenueLastMonth = revenue;
        s.RevenueThisMonth = 0;
        s.Cash += revenue - costs;
        s.Emit(EventKind.Money, $"Month closed: billed ${revenue}, paid ${costs}. Cash ${s.Cash}.");
        if (s.EngagedThisMonth > 0) s.Emit(EventKind.Defect, $"The tally sheet for the month: {s.EngagedThisMonth} engaged tones on {BoardRules.Home(s).LinesIn} lines.");
        s.EngagedThisMonth = 0;
        if (s.Cash < 0) s.Emit(EventKind.Money, "The bank has written. In red ink, which feels personal.");
    }

    private void SurfaceDefects()
    {
        var s = State;
        foreach (var d in s.Defects.Where(d => d.SurfaceDay <= s.Day).ToList())
        {
            s.Defects.Remove(d);
            var cust = s.Customers.FirstOrDefault(c => c.Id == d.CustomerId);
            if (cust is { Live: true, LotId: not null })
            {
                // A fault at a real address stays broken until someone drives out and fixes it.
                cust.Satisfaction = Math.Max(0, cust.Satisfaction - 10);
                s.Faults.Add(new Fault { Id = s.NewId("fault"), CustomerId = cust.Id, Text = d.Text, Source = d.Source, OpenedDay = s.Day });
                s.Emit(EventKind.Defect, $"Fault on {cust.Name}'s line — {d.Text}. It stays broken until you go and fix it.", d.Id);
                continue;
            }
            if (cust is not null) cust.Satisfaction = Math.Max(0, cust.Satisfaction - 25);
            s.Emit(EventKind.Defect, cust is null ? $"Fault: {d.Text}" : $"Fault on {cust.Name}'s line — {d.Text}", d.Id);
        }
    }

    /// <summary>
    /// Move the clock. Time passes only as the player lives it: nothing jumps it forward except sleep.
    /// Staff finish their tickets as their working day goes by; at six in the morning a new day begins.
    /// </summary>
    public void AdvanceMinutes(int minutes)
    {
        while (minutes > 0)
        {
            int step = Math.Min(minutes, Clock.MinutesPerDay - State.Minute);
            State.Minute += step; minutes -= step;
            if (State.Minute >= Clock.MinutesPerDay) { State.Minute = 0; AdvanceDay(); }
            else StaffRules.Clocked(State, Content, _rng);
        }
        State.RngState = _rng.State;
    }

    /// <summary>Go to bed: it is seven the next morning.</summary>
    public void Sleep() { AdvanceDay(); State.Minute = 60; }

    /// <summary>A line goes live: the customer starts paying, beginning with a connection fee in your hand today.</summary>
    private void GoLive(Customer c) => GoLive(State, Content, c);

    internal static void GoLive(GameState s, ContentDb content, Customer c)
    {
        c.Installed = true;
        // once there is a sysop's terminal, a new caller has to be let in (by you, or by your co-sysop)
        c.HasAccount = !BoardService.TerminalOpen(s) || BoardService.Delegated(s, content);
        long fee = ConnectionFee(c);
        s.Cash += fee;
        if (s.LiveSubscribers == 1) s.Emit(EventKind.Milestone, $"First customer: {c.Name}. A dial tone, a modem shriek, a thank-you, and ${fee} for the connection.", c.Id);
        else s.Emit(EventKind.Money, $"{c.Name}'s line is live. ${fee} connection fee, then ${c.MonthlyFee} a month.", c.Id);
    }

    /// <summary>Sleep through the quiet: days pass until something needs the player (a message, a fault, a job, the monthly bill). Returns days slept.</summary>
    public int RestUntilNeeded(int maxDays = 30)
    {
        int n = 0;
        do { Sleep(); n++; }
        while (n < maxDays && Guidance.IsQuiet(State, Content) && !State.Date.IsFirstOfMonth);
        return n;
    }

    /// <summary>A subscriber rings to order something from the catalogue (used by the phone, and by checks).</summary>
    public Order? OfferOrder()
    {
        var order = OrderRules.Offer(State, Content, _rng);
        State.RngState = _rng.State;
        return order;
    }

    public bool CompleteOrder(string orderId, bool bodged)
    {
        bool ok = OrderRules.Complete(State, Content, _rng, orderId, bodged);
        State.RngState = _rng.State;
        return ok;
    }

    public OddJobTask? NewOddJobTask(string jobId) =>
        State.Jobs.FirstOrDefault(j => j.Id == jobId) is { } job ? new OddJobTask(job.Id, job.Title, job.Hours) : null;

    // ---- god keys: skip ahead to see later stages (testing and impatient owners) ----------------
    public string GodCash(long amount) { State.Cash += amount; return $"+${amount}."; }

    /// <summary>Finish whatever the game is currently asking for, by decree.</summary>
    public string GodNextStage(PlanetGen? gen)
    {
        var s = State; var c = Content;
        Customer? SignNext() => TownRules.Prospects(s, c).FirstOrDefault() is { } lot ? TownRules.SignUp(s, lot) : null;
        void Connect(Customer cust)
        {
            if (!cust.SocketFitted) { s.Modems++; TownRules.FitSocket(s, cust.Id); }
            if (SwitchRules.Pos(s, cust.Id) is null && SwitchRules.Full(s)) { s.Cash = Math.Max(s.Cash, SwitchRules.PanelPrice); SwitchRules.AddPanel(s); }
            SwitchRules.AutoWire(s, c, cust, sloppy: false);
            cust.HasAccount = true;
        }
        string done;
        switch (Onboarding.Current(s, c))
        {
            case OnboardingStep.Enquiry:
                s.MessagesWaiting = Math.Max(1, s.MessagesWaiting);
                done = TakeEnquiry() is { } first ? $"{first.Name} signed up." : "Nobody left to sign."; break;
            case OnboardingStep.Install:
                foreach (var cust in s.Customers.Where(x => x.Live && !x.SocketFitted).ToList()) { s.Modems++; TownRules.FitSocket(s, cust.Id); }
                done = "House installs done."; break;
            case OnboardingStep.Frame:
                foreach (var cust in s.Customers.Where(x => x.Live && !x.Installed).ToList()) Connect(cust);
                done = "Lines wired and live."; break;
            case OnboardingStep.OddJob:
                if (s.Jobs.Count == 0) TownRules.OfferJob(s, c, _rng);
                done = s.Jobs.FirstOrDefault() is { } job && TownRules.CompleteJob(s, c, job.Id) is not null ? "Odd job done." : "No odd job to do."; break;
            case OnboardingStep.Knock:
                done = SignNext() is { } knocked ? $"{knocked.Name} said yes on the doorstep." : "Nobody left to ask."; break;
            default:
                foreach (var cust in s.Customers.Where(x => x.Live && !x.Installed).ToList()) Connect(cust);
                if (s.Staff == 0)
                {
                    while (s.LiveSubscribers < (WorkRules.Next(s, c, "install")?.RequiresSubscribers ?? 0) && SignNext() is { } extra) Connect(extra);
                    s.Cash = Math.Max(s.Cash, WorkRules.Next(s, c, "install")?.Cost ?? 0);
                    done = WorkRules.Upgrade(s, c, "install") == UpgradeResult.Ok ? "Technician hired." : "Could not hire.";
                }
                else if (gen is not null && c.Premises.Values.FirstOrDefault(p => p.PoleLine is not null && !LineRules.Complete(s, gen, p)) is { } far)
                {
                    int n = LineRules.PoleSites(gen, far).Count;
                    s.Cash += n * far.PoleLine!.PoleCost;
                    for (int i = 0; i < n; i++) LineRules.Plant(s, gen, far, Tools.All);
                    for (int i = 0; i < n; i++) LineRules.String(s, c, gen, far, Tools.All);
                    done = $"Pole line to {far.Name} built.";
                }
                else if (TownRules.Prospects(s, c).Any())
                {
                    while (SignNext() is { } rest) Connect(rest);
                    done = "The whole town is connected.";
                }
                else if (c.Work.ContainsKey(BoardService.WorkId) && !BoardService.Delegated(s, c) && WorkRules.Next(s, c, BoardService.WorkId) is { } coSysop)
                {
                    s.Cash = Math.Max(s.Cash, coSysop.Cost);
                    done = WorkRules.Upgrade(s, c, BoardService.WorkId) == UpgradeResult.Ok ? "Tobi is co-sysop." : GodNextAct();
                }
                else done = GodNextAct();
                foreach (var cust in BoardService.Waiting(s)) cust.HasAccount = true;
                break;
        }
        s.RngState = _rng.State;
        return done;
    }

    /// <summary>Jump to the next act (age), whatever the calendar and the milestones say.</summary>
    public string GodNextAct()
    {
        var s = State;
        if (s.ActIndex + 1 >= Content.Acts.Count) return "This is the last act.";
        var next = Content.Acts[s.ActIndex + 1];
        if (next.GateAnnouncement is { } g) s.FiredAnnouncements.Add(g);
        s.ActIndex++;
        s.Emit(EventKind.ActChanged, $"Act {next.Index}: {next.Name}. {next.BusinessModel}. What holds you back now: {next.BindingConstraint}.", next.Id);
        AlmanacRules.RefreshStatuses(s, Content);
        return $"Act {next.Index}: {next.Name} ({next.Years}).";
    }

    private void DriftSatisfaction()
    {
        foreach (var f in State.Faults.ToList())
        {
            var c = State.Customers.FirstOrDefault(x => x.Id == f.CustomerId);
            if (c is null || !c.Live) { State.Faults.Remove(f); continue; }
            c.Satisfaction = Math.Max(0, c.Satisfaction - 2); // every day on a bad line is remembered
        }
        foreach (var c in State.Customers.Where(c => c.Live))
        {
            if (c.Satisfaction < 70 && _rng.ChancePermille(200)) c.Satisfaction++;
            if (c.Satisfaction < 20 && _rng.ChancePermille(100))
            {
                c.Live = false;
                State.Emit(EventKind.Info, $"{c.Name} cancelled. The letter is polite, which is worse.", c.Id);
            }
        }
    }

    public Customer AddCustomer(string name, string archetype, long monthlyFee, bool installed = true)
    {
        var c = new Customer { Id = State.NewId("cust"), Name = name, Archetype = archetype, MonthlyFee = monthlyFee, JoinedDay = State.Day, Installed = installed };
        State.Customers.Add(c);
        if (installed && State.LiveSubscribers == 1) State.Emit(EventKind.Milestone, $"First customer: {name}.", c.Id);
        return c;
    }

    // Placeholder name pool until customers are generated from town data.
    private static readonly string[] First = { "Ada", "Bernard", "Chidi", "Dot", "Eamon", "Fatima", "Gus", "Hilde", "Ines", "Jory", "Kofi", "Lorna", "Marek", "Nell", "Oswin", "Priya", "Quentin", "Rosa", "Sol", "Tamsin" };
    private static readonly string[] Last = { "Hale", "Okafor", "Brennan", "Vasquez", "Lindqvist", "Moss", "Achebe", "Pike", "Duarte", "Kowalski", "Reyes", "Thorne" };
    private static readonly (string Archetype, long Fee)[] Archetypes =
        { ("curious", 8), ("student", 6), ("family", 10), ("gamer", 12), ("small-business", 25), ("price-sensitive", 5), ("reliability-sensitive", 15), ("tech-inexperienced", 9) };

    /// <summary>
    /// A message on the answering machine: word of mouth has reached a neighbour and they want a line.
    /// If everyone in reach has already signed, it is someone with an odd job instead (returns null).
    /// </summary>
    public Customer? TakeEnquiry()
    {
        if (State.MessagesWaiting > 0) State.MessagesWaiting--;
        Customer? c = null;
        var prospects = TownRules.Prospects(State, Content).ToList();
        // once the round is learned, some calls are existing subscribers wanting something from the catalogue
        bool free = Onboarding.Reached(State, Content, OnboardingStep.Free);
        if (free && (prospects.Count == 0 || _rng.ChancePermille(400)) && OrderRules.Offer(State, Content, _rng) is not null) { }
        else if (prospects.Count > 0) c = TownRules.SignUp(State, prospects[_rng.Next(prospects.Count)]);
        else if (Content.Lots.Count == 0)
        {
            var (arch, fee) = Archetypes[_rng.Next(Archetypes.Length)];
            c = AddCustomer($"{First[_rng.Next(First.Length)]} {Last[_rng.Next(Last.Length)]}", arch, fee, installed: false);
        }
        else if (TownRules.OfferJob(State, Content, _rng) is { } job)
            State.Emit(EventKind.Info, $"Message: \"{job.Title}\" — ${job.Pay} if you can come by.", job.LotId);
        State.RngState = _rng.State;
        return c;
    }

    /// <summary>Someone rings with an odd job (used when the phone has nothing better, and by checks).</summary>
    public OddJob? OfferJob()
    {
        var job = TownRules.OfferJob(State, Content, _rng);
        State.RngState = _rng.State;
        return job;
    }

    public KnockResult Knock(string lotId)
    {
        var r = TownRules.Knock(State, Content, _rng, lotId);
        State.RngState = _rng.State;
        return r;
    }

    // ---- the install at the customer's house ---------------------------------------------------
    /// <summary>Start (or restart) the house install for a signed customer. Needs a modem in stock. Same customer, same house, same hidden fault.</summary>
    public InstallJob? NewInstallJob(string customerId)
    {
        var cust = State.Customers.FirstOrDefault(c => c.Id == customerId && c.Live);
        if (cust is null || cust.SocketFitted || State.Modems <= 0) return null;
        var job = InstallJob.Generate(DetRng.Derive(State.Seed, $"install:{cust.Id}"), cust.Id, cust.Name);
        return State.Installs.TryGetValue(cust.Id, out var saved) ? job.Restore(saved) : job;
    }

    /// <summary>Remember how far a house install has got, so a save in the middle of it comes back to the same step.</summary>
    public void RecordInstall(InstallJob job)
    {
        if (State.Customers.Any(c => c.Id == job.CustomerId && c.Live && !c.SocketFitted)) State.Installs[job.CustomerId] = job.Snapshot();
    }

    /// <summary>Sign off a house install: the modem leaves stock, the socket is in, and any bodges go into the wall as latent defects.</summary>
    public bool ApplyInstallOutcome(InstallOutcome o)
    {
        var s = State;
        if (!o.Complete || !TownRules.FitSocket(s, o.CustomerId)) return false;
        // mistakes compound: every extra bodge on the same line brings all of them to the surface sooner
        foreach (var text in o.Defects)
            s.Defects.Add(new LatentDefect { Id = s.NewId("defect"), SurfaceDay = s.Day + Math.Max(3, _rng.Range(10, 45) / o.Defects.Count), Source = "install", CustomerId = o.CustomerId, Text = text });
        if (o.OvertimeMinutes > 0 && s.Customers.FirstOrDefault(c => c.Id == o.CustomerId) is { } cust)
            cust.Satisfaction = Math.Clamp(cust.Satisfaction - Math.Min(5, 1 + o.OvertimeMinutes / 30), 0, 100);
        s.RngState = _rng.State;
        return true;
    }

    /// <summary>Throw the switchboard's test switch: every waiting subscriber with a jumper is tried, and the good ones go live.</summary>
    public List<(string CustomerId, ThrowResult Result)> ThrowSwitch()
    {
        var s = State;
        var results = SwitchRules.Throw(s, Content, _rng);
        // the first line is live: now, and only now, the neighbours start asking for odd jobs
        if (Onboarding.Current(s, Content) == OnboardingStep.OddJob && s.Jobs.Count == 0) TownRules.OfferJob(s, Content, _rng);
        s.RngState = _rng.State;
        return results;
    }
}
