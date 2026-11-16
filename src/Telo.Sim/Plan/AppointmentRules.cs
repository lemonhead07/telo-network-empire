using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.Town;
using Telo.Sim.Work;
using Telo.Sim.World;

namespace Telo.Sim.Plan;

public enum AppointmentStatus { Asked, Confirmed }

/// <summary>A time a customer has agreed to be in for a job (saved, v13).</summary>
public sealed class Appointment
{
    public required string TicketKey { get; set; }
    public required string LotId { get; set; }
    public string Title { get; set; } = "";
    public int Day { get; set; }
    /// <summary>The window they'll be in, in minutes since 06:00.</summary>
    public int Start { get; set; }
    public int End { get; set; }
    public AppointmentStatus Status { get; set; }
    /// <summary>When work started inside the window (the job clock), and when it was finished. Null until then.</summary>
    public int? Begun { get; set; }
    public int? Done { get; set; }
}

/// <summary>
/// Appointments: jobs on your terms instead of on call. Plan tomorrow on the timesheet in the evening and every job on it
/// asks its customer for a two-hour window. They ring back overnight: by morning it's confirmed, or (if they can't get off
/// work) moved to the evening. Inside a confirmed window they are home, whatever their usual hours. Turn up and the job
/// can be done; don't, and they waited in for nothing and remember it. Dropping in on whoever happens to be home still works.
/// </summary>
public static class AppointmentRules
{
    public const int Window = 120, EarlyMinutes = 30, MissedSatisfaction = 6;
    public const int TimeOffPermille = 700;              // a household out at work takes the morning off for you, mostly
    public const int EveningStart = 11 * 60 + 30;        // 17:30, when the out-at-work households are back

    // ---- the job clock ---------------------------------------------------------------------------
    // A booked job fills its window. Once work starts inside it, the world slows so the two-hour window lasts four real
    // minutes: a relaxed clock. Done in under two real minutes (the first hour) earns a tip; past four, the window is
    // over, the world speeds up again, and the next booking may come and go before you get there.
    public const int JobRealSeconds = 240, BonusRealSeconds = 120;
    public const int BonusPay = 15, BonusSatisfaction = 3;
    /// <summary>Game minutes inside the window that earn the tip.</summary>
    public const int BonusMinutes = Window * BonusRealSeconds / JobRealSeconds;

    /// <summary>The booked job being worked on right now, if any: the world runs on the job clock while it is.</summary>
    public static Appointment? OnTheClock(GameState s) =>
        s.Appointments.FirstOrDefault(a => a.Day == s.Day && a.Begun is not null && a.Done is null && s.Minute < a.End);

    /// <summary>Real seconds spent on the job so far, as the timer shows it.</summary>
    public static int ClockSeconds(Appointment a, int minute) => (minute - (a.Begun ?? minute)) * JobRealSeconds / Window;

    /// <summary>
    /// Every few game minutes: start the job clock when work begins inside a booked window (a job under way at that house),
    /// and stop it when the job is done (its ticket gone), paying the tip if it was quick.
    /// </summary>
    internal static void Tick(GameState s, ContentDb c, ICollection<string> workingAt)
    {
        if (s.Appointments.Count == 0) return;
        HashSet<string>? open = null;
        foreach (var a in s.Appointments.Where(a => a.Day == s.Day && a.Status == AppointmentStatus.Confirmed && a.Done is null).ToList())
        {
            open ??= StaffRules.Tickets(s, c).Select(t => t.Key).ToHashSet();
            bool inWindow = s.Minute >= a.Start - EarlyMinutes && s.Minute < a.End;
            bool working = workingAt.Contains(a.LotId);
            if (a.Begun is null && inWindow && working) { a.Begun = Math.Max(s.Minute, a.Start); continue; }
            // the visit is over when the work at the house stops (an install still has the switchboard to go, back home),
            // or, for a quick job at the door, when its ticket is gone
            bool visitOver = a.Begun is not null ? !working : !open.Contains(a.TicketKey) || !HouseWorkLeft(s, a.TicketKey);
            if (!visitOver) continue;
            if (a.Begun is null && !inWindow) { s.Appointments.Remove(a); continue; }   // done some other time, or by someone else
            a.Done = s.Minute;
            a.Begun ??= s.Minute;   // a quick one (a repair at the door): done as soon as it started
            if (a.Done - a.Begun <= BonusMinutes && a.Done <= a.End)
            {
                s.Cash += BonusPay;
                if (s.Customers.FirstOrDefault(x => x.LotId == a.LotId) is { } cust) cust.Satisfaction = Math.Min(100, cust.Satisfaction + BonusSatisfaction);
                s.Emit(EventKind.Money, $"Done in good time: {(c.Lots.TryGetValue(a.LotId, out var l) ? l.Resident : "they")} tipped you ${BonusPay}.", a.LotId);
            }
        }
    }

    /// <summary>A confirmed window covering this minute (or starting within half an hour) at this house today.</summary>
    public static Appointment? Booked(GameState s, string lotId) =>
        s.Appointments.FirstOrDefault(a => a.Status == AppointmentStatus.Confirmed && a.LotId == lotId && a.Day == s.Day
                                           && s.Minute >= a.Start - EarlyMinutes && s.Minute < a.End);

    public static IEnumerable<Appointment> Today(GameState s) =>
        s.Appointments.Where(a => a.Day == s.Day && a.Status == AppointmentStatus.Confirmed && a.Done is null
                                  && (a.Begun is not null || HouseWorkLeft(s, a.TicketKey))).OrderBy(a => a.Start);

    public static Appointment? For(GameState s, string ticketKey) => s.Appointments.FirstOrDefault(a => a.TicketKey == ticketKey);

    /// <summary>Is there still work to do at the house for this ticket? A connection whose socket is in only has the switchboard left, back home.</summary>
    public static bool HouseWorkLeft(GameState s, string ticketKey) =>
        !ticketKey.StartsWith("install:") || s.Customers.FirstOrDefault(x => $"install:{x.Id}" == ticketKey) is not { SocketFitted: true };

    /// <summary>Is being out at work the only reason they wouldn't be in then? Then they can be asked to be in.</summary>
    public static bool CouldBeIn(GameState s, ContentDb c, LotDef lot, int day, int minute)
    {
        int d = s.Day, mn = s.Minute;
        s.Day = day; s.Minute = minute;
        try { return lot.Kind is LotKind.House or LotKind.Farm && HomeRules.OutAtWork(s, c, lot) && !HomeRules.Asleep(s, c, lot); }
        finally { s.Day = d; s.Minute = mn; }
    }

    /// <summary>After a future day's sheet is laid out: ask for a window for every house job on it, and drop the ones taken off.</summary>
    public static void Sync(GameState s, ContentDb c, DayPlan plan, IReadOnlyDictionary<string, Ticket> tickets)
    {
        if (plan.Day <= s.Day || plan.Who != "player") return;
        var keep = new HashSet<string>();
        foreach (var slot in plan.Slots)
        {
            if (!tickets.TryGetValue(slot.TicketKey, out var t) || t.LotId is null || !c.Lots.TryGetValue(t.LotId, out var lot)
                || lot.Kind is not (LotKind.House or LotKind.Farm) || !HouseWorkLeft(s, slot.TicketKey)) continue;
            keep.Add(slot.TicketKey);
            int start = slot.Start / 30 * 30;   // on the half hour
            var a = For(s, slot.TicketKey);
            if (a is not null && a.Day == plan.Day && Math.Abs(a.Start - start) < 60) { slot.Booking = Label(a); continue; }
            s.Appointments.RemoveAll(x => x.TicketKey == slot.TicketKey);
            a = new Appointment { TicketKey = slot.TicketKey, LotId = t.LotId, Title = t.Title, Day = plan.Day, Start = start, End = start + Window, Status = AppointmentStatus.Asked };
            s.Appointments.Add(a);
            slot.Booking = Label(a);
        }
        s.Appointments.RemoveAll(a => a.Day == plan.Day && !keep.Contains(a.TicketKey));
    }

    public static string Label(Appointment a) => a.Status == AppointmentStatus.Confirmed
        ? $"booked {PlanRules.Clock(a.Start)}-{PlanRules.Clock(a.End)}" : "asked them to be in";

    /// <summary>
    /// Overnight, before the new day starts (call after the day has advanced): yesterday's windows nobody turned up for are
    /// remembered, and today's requests are answered.
    /// </summary>
    internal static void Overnight(GameState s, ContentDb c, DetRng rng)
    {
        var open = StaffRules.Tickets(s, c).Select(t => t.Key).ToHashSet();
        foreach (var a in s.Appointments.Where(a => a.Day < s.Day).ToList())
        {
            s.Appointments.Remove(a);
            if (a.Status != AppointmentStatus.Confirmed || a.Begun is not null || !open.Contains(a.TicketKey) || !HouseWorkLeft(s, a.TicketKey)) continue;   // came, done, or never agreed
            var who = c.Lots.TryGetValue(a.LotId, out var l) ? l.Resident : "A customer";
            if (s.Customers.FirstOrDefault(x => x.LotId == a.LotId) is { } cust) cust.Satisfaction = Math.Max(0, cust.Satisfaction - MissedSatisfaction);
            TownRules.HouseholdOf(s, a.LotId).Goodwill = Math.Max(0, TownRules.HouseholdOf(s, a.LotId).Goodwill - 50);
            s.Emit(EventKind.Defect, $"{who} stayed in {PlanRules.Clock(a.Start)}-{PlanRules.Clock(a.End)} yesterday and nobody came.", a.LotId);
        }
        s.Appointments.RemoveAll(a => !open.Contains(a.TicketKey) || a.Done is not null);   // the job was done, or done another way
        foreach (var a in s.Appointments.Where(a => a.Status == AppointmentStatus.Asked && a.Day == s.Day))
        {
            var lot = c.Lots.GetValueOrDefault(a.LotId);
            if (lot is not null && CouldBeIn(s, c, lot, a.Day, a.Start) && !rng.ChancePermille(TimeOffPermille))
            {
                a.Start = EveningStart; a.End = EveningStart + Window;
                s.Emit(EventKind.Info, $"{lot.Resident} can't get the time off work. They'll be in {PlanRules.Clock(a.Start)}-{PlanRules.Clock(a.End)} instead.", a.LotId);
            }
            a.Status = AppointmentStatus.Confirmed;
        }
    }
}
