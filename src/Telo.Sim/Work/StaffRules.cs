using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.Net;
using Telo.Sim.Town;
using Telo.Sim.World;

namespace Telo.Sim.Work;

public enum TicketKind { Install, Order, Repair, OddJob }

/// <summary>A piece of work on a slip of paper. Derived from the state every time: a ticket exists while its job does.</summary>
public sealed record Ticket(string Key, TicketKind Kind, string Title, string? LotId, int WaitingDays, string? AssignedTo);

/// <summary>
/// People and paper. Every job is a ticket. A member of staff only works on tickets they have been handed:
/// in person, by you, each morning, until you lease a depot where they collect their own. Until then they are paid
/// by the job; with a depot, by the week. They work eight till five, Monday to Friday, on the kinds of job they do.
/// Their mistakes look fine today and surface weeks later.
/// </summary>
public static class StaffRules
{
    public const int MinutesPerJob = 240;
    public const long DepotDeposit = 150, DepotRentPerMonth = 60;

    public static List<Ticket> Tickets(GameState s, ContentDb c)
    {
        var list = new List<Ticket>();
        string? Who(string key) => s.Assigned.GetValueOrDefault(key);
        foreach (var f in s.Faults)
            if (s.Customers.FirstOrDefault(x => x.Id == f.CustomerId) is { } cust)
                list.Add(new Ticket($"repair:{f.Id}", TicketKind.Repair, $"Fault at {cust.Name}'s", cust.LotId, s.Day - f.OpenedDay, Who($"repair:{f.Id}")));
        foreach (var cust in s.Customers.Where(x => x.Live && !x.Installed))
            list.Add(new Ticket($"install:{cust.Id}", TicketKind.Install, $"Connect {cust.Name}", cust.LotId, s.Day - cust.JoinedDay, Who($"install:{cust.Id}")));
        foreach (var o in s.Orders)
            if (s.Customers.FirstOrDefault(x => x.Id == o.CustomerId) is { } buyer && c.Products.TryGetValue(o.ProductId, out var p))
                list.Add(new Ticket($"order:{o.Id}", TicketKind.Order, $"{p.Name} for {buyer.Name}", buyer.LotId, s.Day - o.PlacedDay, Who($"order:{o.Id}")));
        foreach (var j in s.Jobs)
            list.Add(new Ticket($"job:{j.Id}", TicketKind.OddJob, $"{j.Title} (${j.Pay})", j.LotId, 0, Who($"job:{j.Id}")));
        return list;
    }

    public static StaffMember? Member(GameState s, string id) => s.StaffMembers.FirstOrDefault(m => m.Id == id);
    public static int Queue(GameState s, ContentDb c, StaffMember m) => Tickets(s, c).Count(t => t.AssignedTo == m.Id);

    /// <summary>Can this person be handed this ticket? Only the kinds of work they do, and only a day's worth at a time.</summary>
    public static bool CanTake(GameState s, ContentDb c, StaffMember m, Ticket t) =>
        t.AssignedTo is null && m.Does.Contains(t.Kind) && Queue(s, c, m) < m.JobsPerDay;

    public static bool Assign(GameState s, ContentDb c, string ticketKey, string staffId)
    {
        if (Member(s, staffId) is not { } m || Tickets(s, c).FirstOrDefault(t => t.Key == ticketKey) is not { } t || !CanTake(s, c, m, t)) return false;
        s.Assigned[ticketKey] = staffId;
        return true;
    }

    /// <summary>Called as the clock moves: each person finishes a job at the end of every four hours of their working day.</summary>
    public static void Clocked(GameState s, ContentDb c, DetRng rng)
    {
        if (!Clock.IsWorkingDay(s.Day)) return;
        int due = (int)Math.Floor((Clock.Hour(s) - Clock.WorkStartsHour) * 60 / MinutesPerJob);
        foreach (var m in s.StaffMembers)
            while (m.JobsToday < Math.Min(m.JobsPerDay, due) && Work(s, c, rng, m)) { }
    }

    /// <summary>The day is over: anything handed out and not yet done gets done (so sleeping early never loses work), then a new day's sheet.</summary>
    public static void EndOfDay(GameState s, ContentDb c, DetRng rng, int dayEnded)
    {
        foreach (var key in s.Assigned.Keys.Except(Tickets(s, c).Select(t => t.Key)).ToList()) s.Assigned.Remove(key);
        foreach (var m in s.StaffMembers)
        {
            if (Clock.IsWorkingDay(dayEnded)) while (m.JobsToday < m.JobsPerDay && Work(s, c, rng, m)) { }
            m.JobsToday = 0;
        }
        // with a depot, people collect their own tickets in the morning: oldest first
        if (s.HasDepot && Clock.IsWorkingDay(s.Day))
            foreach (var m in s.StaffMembers)
                foreach (var t in Tickets(s, c).OrderByDescending(t => t.WaitingDays).ToList())
                    if (CanTake(s, c, m, t)) s.Assigned[t.Key] = m.Id;
        if (Clock.Weekday(s.Day) == DayOfWeek.Friday) PayDay(s, c);
    }

    /// <summary>Do the first ticket in this person's hand. Returns false if there is nothing they can do right now.</summary>
    private static bool Work(GameState s, ContentDb c, DetRng rng, StaffMember m)
    {
        var t = Tickets(s, c).FirstOrDefault(x => x.AssignedTo == m.Id);
        if (t is null) return false;
        if (t.Kind != TicketKind.Install || s.Customers.FirstOrDefault(x => $"install:{x.Id}" == t.Key) is not { } cust) { s.Assigned.Remove(t.Key); return false; }
        if (!cust.SocketFitted)
        {
            if (s.Modems <= 0)
            {
                if (s.Day % 7 == 0 && m.JobsToday == 0) s.Emit(EventKind.Defect, $"{m.Name} has a ticket and no modem to fit. Sol's Electronics sells them.");
                return false;
            }
            s.Modems--; cust.SocketFitted = true;
            s.Installs.Remove(cust.Id);
        }
        Simulation.GoLive(s, c, cust);
        s.Assigned.Remove(t.Key);
        m.JobsToday++; m.JobsThisWeek++; s.TechInstalls++;
        if (!rng.ChancePermille(m.AccuracyPermille))
            s.Defects.Add(new LatentDefect { Id = s.NewId("defect"), SurfaceDay = s.Day + rng.Range(7, 41), Source = "technician", CustomerId = cust.Id, Text = $"an install {m.Name.Split(' ')[0]} swore was tested" });
        if (s.TechInstalls == 1) s.Emit(EventKind.Milestone, $"{m.Name} connected {cust.Name} while you were doing something else. Nobody needed you. It is a strange feeling.", cust.Id);
        return true;
    }

    /// <summary>What this person is owed this Friday: by the job until there is a depot, by the week after.</summary>
    public static long Owed(GameState s, StaffMember m) => m.Does.Count == 0 || s.HasDepot ? m.WeeklyWage : m.PieceRate * m.JobsThisWeek;
    public static long OwedThisWeek(GameState s) => s.StaffMembers.Sum(m => Owed(s, m));

    private static void PayDay(GameState s, ContentDb c)
    {
        if (s.StaffMembers.Count == 0) return;
        long total = OwedThisWeek(s);
        string lines = string.Join(", ", s.StaffMembers.Select(m => $"{m.Name.Split(' ')[0]} ${Owed(s, m)}" + (m.Does.Count > 0 && !s.HasDepot ? $" ({m.JobsThisWeek} job{(m.JobsThisWeek == 1 ? "" : "s")})" : "")));
        s.Cash -= total;
        foreach (var m in s.StaffMembers) m.JobsThisWeek = 0;
        s.Emit(EventKind.Money, $"Pay day: {lines}. Cash ${s.Cash}.");
    }

    public static bool CanLeaseDepot(GameState s) => !s.HasDepot && s.StaffMembers.Any(m => m.Does.Count > 0) && s.Cash >= DepotDeposit;

    /// <summary>1 Main Street. Staff turn up there, take their own tickets and are paid by the week.</summary>
    public static bool LeaseDepot(GameState s)
    {
        if (!CanLeaseDepot(s)) return false;
        s.Cash -= DepotDeposit; s.HasDepot = true;
        s.Emit(EventKind.Milestone, $"You sign the lease on 1 Main Street: ${DepotDeposit} down, ${DepotRentPerMonth} a month. Your staff collect their tickets there from tomorrow, and are paid by the week.");
        return true;
    }

    /// <summary>The week ahead as it would be written on a wall calendar.</summary>
    public static List<(int Day, string Note)> WeekAhead(GameState s, ContentDb c, int days = 8)
    {
        var list = new List<(int, string)>();
        for (int d = s.Day; d < s.Day + days; d++)
        {
            var notes = new List<string>();
            if (Clock.Weekday(d) == DayOfWeek.Friday && s.StaffMembers.Count > 0)
                notes.Add(d == s.Day ? "PAY DAY (paid this morning)" : $"PAY DAY: ${(d - s.Day < 7 ? OwedThisWeek(s) : 0)} so far");
            if (new GameDate(d).IsFirstOfMonth) notes.Add($"bills in, rent ${Simulation.RentPerMonth + (s.HasDepot ? DepotRentPerMonth : 0)} out");
            if (Clock.Weekday(d) == DayOfWeek.Sunday) notes.Add("Sol's shut");
            if (Clock.IsWeekend(d) && s.StaffMembers.Count > 0) notes.Add("staff off");
            list.Add((d, string.Join("; ", notes)));
        }
        return list;
    }
}
