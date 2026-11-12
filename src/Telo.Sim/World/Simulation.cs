using Telo.Sim.Almanac;
using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.Ledger;
using Telo.Sim.Minigames.Frame;
using Telo.Sim.Build;
using Telo.Sim.Minigames.Install;
using Telo.Sim.Minigames.OddJob;
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
    /// <summary>A part-time junior on 1983 small-town wages. (Economy is not balanced yet.)</summary>
    public const long WagePerStaffPerMonth = 90;
    /// <summary>A new line pays a one-off connection fee, this many months' worth, the day it goes live.</summary>
    public const int ConnectionFeeMonths = 2;

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
        TownRules.Tick(s, Content, _rng);
        TechnicianDay();
        SurfaceDefects();
        DriftSatisfaction();
        // Word of mouth: happy subscribers bring enquiries.
        int happy = s.Customers.Count(c => c.Live && c.Installed && c.Satisfaction >= 60);
        if (Onboarding.PhoneRings(s, Content) && s.MessagesWaiting < 9 && _rng.ChancePermille(Math.Min(900, 250 + 40 * happy))) s.MessagesWaiting++;
        if (s.Date.IsFirstOfMonth) MonthEnd();
        while (ActRules.TryAdvance(s, Content)) AlmanacRules.RefreshStatuses(s, Content);
        s.RngState = _rng.State;
    }

    private void MonthEnd()
    {
        var s = State;
        long revenue = s.Customers.Where(c => c.Live && c.Installed).Sum(c => c.MonthlyFee);
        long costs = RentPerMonth + s.Staff * WagePerStaffPerMonth + WorkRules.MonthlyUpkeep(s, Content);
        s.RevenueLastMonth = revenue;
        s.RevenueThisMonth = 0;
        s.Cash += revenue - costs;
        s.Emit(EventKind.Money, $"Month closed: billed ${revenue}, paid ${costs}. Cash ${s.Cash}.");
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
    /// Once installs are delegated, the technician connects one waiting customer a day without you: house and frame.
    /// They need modems in stock, and they are right most of the time. The rest goes into the wall as latent defects.
    /// </summary>
    private void TechnicianDay()
    {
        var s = State;
        var stage = WorkRules.Current(s, Content, "install");
        if (stage.Mode == WorkMode.Manual) return;
        var cust = s.Customers.FirstOrDefault(c => c.Live && !c.Installed);
        if (cust is null) return;
        if (!cust.SocketFitted)
        {
            if (s.Modems <= 0)
            {
                if (s.Day % 7 == 0) s.Emit(EventKind.Defect, $"Your technician has no modems. {s.PendingInstalls} customer(s) are waiting. Sol's Electronics sells them.");
                return;
            }
            s.Modems--; cust.SocketFitted = true;
        }
        GoLive(cust);
        s.TechInstalls++;
        if (!_rng.ChancePermille(stage.AccuracyPermille))
            s.Defects.Add(new LatentDefect { Id = s.NewId("defect"), SurfaceDay = s.Day + _rng.Range(7, 41), Source = "technician", CustomerId = cust.Id, Text = "an install the technician swore was tested" });
        if (s.TechInstalls == 1) s.Emit(EventKind.Milestone, $"Your technician connected {cust.Name} while you were doing something else. Nobody needed you. It is a strange feeling.", cust.Id);
    }

    /// <summary>A line goes live: the customer starts paying, beginning with a connection fee in your hand today.</summary>
    private void GoLive(Customer c)
    {
        var s = State;
        c.Installed = true;
        long fee = c.MonthlyFee * ConnectionFeeMonths;
        s.Cash += fee;
        if (s.LiveSubscribers == 1) s.Emit(EventKind.Milestone, $"First customer: {c.Name}. A dial tone, a modem shriek, a thank-you, and ${fee} for the connection.", c.Id);
        else s.Emit(EventKind.Money, $"{c.Name}'s line is live. ${fee} connection fee, then ${c.MonthlyFee} a month.", c.Id);
    }

    /// <summary>Sleep through the quiet: days pass until something needs the player (a message, a fault, a job, the monthly bill). Returns days slept.</summary>
    public int RestUntilNeeded(int maxDays = 30)
    {
        int n = 0;
        do { AdvanceDay(); n++; }
        while (n < maxDays && Guidance.IsQuiet(State, Content) && !State.Date.IsFirstOfMonth);
        return n;
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
        void Connect(Customer cust) { if (!cust.SocketFitted) { s.Modems++; TownRules.FitSocket(s, cust.Id); } if (!cust.Installed) GoLive(cust); }
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
                else done = GodNextAct();
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
        if (prospects.Count > 0) c = TownRules.SignUp(State, prospects[_rng.Next(prospects.Count)]);
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
        return InstallJob.Generate(DetRng.Derive(State.Seed, $"install:{cust.Id}"), cust.Id, cust.Name);
    }

    /// <summary>Send the technician if the 'install' work kind has been delegated; otherwise the player does it by hand.</summary>
    public InstallOutcome? TryDelegateInstall(InstallJob job)
    {
        var stage = WorkRules.Current(State, Content, "install");
        if (stage.Mode == WorkMode.Manual) return null;
        return job.Delegate(_rng.NextU64(), stage.AccuracyPermille);
    }

    /// <summary>Sign off a house install: the modem leaves stock, the socket is in, and any bodges go into the wall as latent defects.</summary>
    public bool ApplyInstallOutcome(InstallOutcome o)
    {
        var s = State;
        if (!o.Complete || !TownRules.FitSocket(s, o.CustomerId)) return false;
        foreach (var text in o.Defects)
            s.Defects.Add(new LatentDefect { Id = s.NewId("defect"), SurfaceDay = s.Day + _rng.Range(10, 45), Source = "install", CustomerId = o.CustomerId, Text = text });
        if (o.OvertimeMinutes > 0 && s.Customers.FirstOrDefault(c => c.Id == o.CustomerId) is { } cust)
            cust.Satisfaction = Math.Clamp(cust.Satisfaction - Math.Min(5, 1 + o.OvertimeMinutes / 30), 0, 100);
        s.RngState = _rng.State;
        return true;
    }

    /// <summary>Lines for the next frame visit: new customers whose sockets are in, then existing subscribers to re-terminate.</summary>
    public List<Customer> FrameWorkOrder(int max = 6) =>
        State.Customers.Where(c => c.Live && !c.Installed && c.SocketFitted)
            .Concat(State.Customers.Where(c => c.Live && c.Installed))
            .Take(max).ToList();

    /// <summary>Build a Frame job for the given customers. Seed is derived so the same job is reproducible.</summary>
    public FramePuzzle NewFrameJob(IReadOnlyList<Customer> customers) =>
        FramePuzzle.Generate(DetRng.Derive(State.Seed, $"frame:{State.Day}:{State.NextId}"),
            customers.Select(c => (c.Id, c.Name)).ToList());

    /// <summary>Delegate the job if the 'install' work kind has been delegated; otherwise the player must do it by hand.</summary>
    public FrameOutcome? TryDelegateFrame(FramePuzzle job)
    {
        var stage = WorkRules.Current(State, Content, "install");
        if (stage.Mode == WorkMode.Manual) return null;
        return job.Delegate(_rng.NextU64(), stage.AccuracyPermille);
    }

    public void ApplyFrameOutcome(FrameOutcome o)
    {
        var s = State;
        Customer? Find(string id) => s.Customers.FirstOrDefault(c => c.Id == id);
        foreach (var id in o.CrossedCustomerIds) if (Find(id) is { } c) c.Satisfaction -= 30;
        foreach (var id in o.DeadCustomerIds) if (Find(id) is { } c) c.Satisfaction -= 15;
        foreach (var id in o.DefectCustomerIds)
            s.Defects.Add(new LatentDefect
            {
                Id = s.NewId("defect"), SurfaceDay = s.Day + _rng.Range(10, 45), Source = "frame", CustomerId = id,
                Text = o.Delegated ? "a jumper the technician swore was fine" : "a dirty pair you hot-wired in a hurry",
            });
        if (o.OvertimeSeconds > 0)
            foreach (var c in s.Customers.Where(c => c.Live)) c.Satisfaction -= Math.Min(5, 1 + o.OvertimeSeconds / 30);
        if (o.Crossed > 0) s.Emit(EventKind.Defect, $"Crossed lines: {o.Crossed} customer(s) can hear a stranger's calls. Privacy incident logged.");
        if (o.PartyLineCut) s.Emit(EventKind.Defect, "You cut the party line. Next door has no dial tone and knows exactly who did it.");
        if (o.Perfect && !o.Delegated) s.Emit(EventKind.Milestone, "Every line live, first time. The frame is a thing of beauty.");
        var failed = o.CrossedCustomerIds.Concat(o.DeadCustomerIds).ToHashSet();
        foreach (var c in s.Customers.Where(c => !c.Installed && c.Live && !failed.Contains(c.Id) && o.Involved.Contains(c.Id)).ToList())
        {
            GoLive(c);
        }
        foreach (var c in s.Customers) c.Satisfaction = Math.Clamp(c.Satisfaction, 0, 100);
        // the first line is live: now, and only now, the neighbours start asking for odd jobs
        if (Onboarding.Current(s, Content) == OnboardingStep.OddJob && s.Jobs.Count == 0) TownRules.OfferJob(s, Content, _rng);
        s.RngState = _rng.State;
    }
}
