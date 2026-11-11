using Telo.Sim.Almanac;
using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.Ledger;
using Telo.Sim.Minigames.Frame;
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
    public const long WagePerStaffPerMonth = 900;

    public Simulation(ContentDb content, GameState state)
    {
        Content = content;
        State = state;
        _rng = new DetRng(state.RngState);
        AlmanacRules.RefreshStatuses(State, Content); // derived state is recomputed, never trusted from disk
    }

    public static Simulation NewGame(ContentDb content, ulong seed)
    {
        var s = new GameState { Seed = seed, RngState = seed, Day = StartDate.Day, Cash = StartingCash, MessagesWaiting = 3 };
        foreach (var w in content.Work.Keys) s.WorkStage[w] = 0;
        s.Emit(EventKind.Info, "PIP: \"…signal. Low battery. Year reads 1983. That can't be right.\"");
        var sim = new Simulation(content, s);
        if (content.Lots.Count > 0) { TownRules.OfferJob(s, content, sim._rng); TownRules.OfferJob(s, content, sim._rng); s.RngState = sim._rng.State; }
        return sim;
    }

    public void AdvanceDays(int days) { for (int i = 0; i < days; i++) AdvanceDay(); }

    public void AdvanceDay()
    {
        var s = State;
        s.Day++;
        AlmanacRules.Tick(s, Content);
        LedgerRules.Tick(s, Content);
        TownRules.Tick(s, Content, _rng);
        SurfaceDefects();
        DriftSatisfaction();
        // Word of mouth: happy subscribers bring enquiries.
        int happy = s.Customers.Count(c => c.Live && c.Installed && c.Satisfaction >= 60);
        if (s.MessagesWaiting < 9 && _rng.ChancePermille(Math.Min(900, 250 + 40 * happy))) s.MessagesWaiting++;
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
            if (cust is not null) cust.Satisfaction = Math.Max(0, cust.Satisfaction - 25);
            s.Emit(EventKind.Defect, cust is null ? $"Fault: {d.Text}" : $"Fault on {cust.Name}'s line — {d.Text}", d.Id);
        }
    }

    private void DriftSatisfaction()
    {
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

    public KnockResult Knock(string lotId)
    {
        var r = TownRules.Knock(State, Content, _rng, lotId);
        State.RngState = _rng.State;
        return r;
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
            c.Installed = true;
            if (s.LiveSubscribers == 1) s.Emit(EventKind.Milestone, $"First customer: {c.Name}. A dial tone, a modem shriek, a thank-you.", c.Id);
        }
        foreach (var c in s.Customers) c.Satisfaction = Math.Clamp(c.Satisfaction, 0, 100);
        s.RngState = _rng.State;
    }
}
