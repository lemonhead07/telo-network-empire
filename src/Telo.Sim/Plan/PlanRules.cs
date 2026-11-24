using Telo.Sim.Act;
using Telo.Sim.Content;
using Telo.Sim.Minigames.Install;
using Telo.Sim.Planet;
using Telo.Sim.Town;
using Telo.Sim.Work;
using Telo.Sim.World;

namespace Telo.Sim.Plan;

/// <summary>One ticket pinned into tomorrow's timesheet.</summary>
public sealed class PlanSlot
{
    public required string TicketKey { get; set; }
    /// <summary>Minutes since 06:00 (the sim's clock), when the work starts after travelling there.</summary>
    public int Start { get; set; }
    public int Minutes { get; set; }
    public int TravelMinutes { get; set; }
    /// <summary>Why it won't work at that hour (nobody home, shop shut), or null. Shown grey on the sheet.</summary>
    public string? Grey { get; set; }
    public string Title { get; set; } = "";
    /// <summary>The appointment for it, in words ("asked them to be in", "booked 10:00-12:00"), or null.</summary>
    public string? Booking { get; set; }
}

/// <summary>A person's day on the timesheet. Saved (v10).</summary>
public sealed class DayPlan
{
    public int Day { get; set; }
    public string Who { get; set; } = "player";
    public List<PlanSlot> Slots { get; set; } = new();
}

/// <summary>
/// The timesheet beside the job board (spine step 5): evening is for planning tomorrow, morning for doing it.
/// Tickets are pinned into hours; each knows how long it takes, where it is, and whether anyone is home then.
/// A day is what fits. The plan is a suggestion to yourself, never a lock (D34, N6).
/// </summary>
public static class PlanRules
{
    public const int DayStarts = 60, DayEnds = 16 * 60;   // 07:00 to 22:00 (the clock starts at 06:00)

    /// <summary>How long a ticket's work takes the player, in game minutes.</summary>
    public static int Duration(GameState s, ContentDb c, Ticket t) => t.Kind switch
    {
        TicketKind.Install => InstallJob.Steps.Sum(d => d.Clicks * d.MinutesPerClick) + InstallJob.TestMinutes,
        TicketKind.Repair => TownRules.RepairMinutes,
        TicketKind.Order => s.Orders.FirstOrDefault(o => $"order:{o.Id}" == t.Key) is { } o && c.Products.TryGetValue(o.ProductId, out var p)
            ? p.Steps.Sum(x => x.Clicks * x.Minutes) : 30,
        TicketKind.OddJob => s.Jobs.FirstOrDefault(j => $"job:{j.Id}" == t.Key) is { } j ? j.Hours * 60 : 60,
        _ => 60,
    };

    public static DayPlan PlanFor(GameState s, int day, string who = "player") =>
        s.Plans.FirstOrDefault(p => p.Day == day && p.Who == who) ?? Add(s, new DayPlan { Day = day, Who = who });

    /// <summary>Pin a ticket at the end of a day's sheet (or take it off again).</summary>
    public static void Toggle(GameState s, ContentDb c, PlanetGen gen, int day, string ticketKey)
    {
        var plan = PlanFor(s, day);
        if (plan.Slots.RemoveAll(x => x.TicketKey == ticketKey) == 0) plan.Slots.Add(new PlanSlot { TicketKey = ticketKey });
        Lay(s, c, gen, plan);
    }

    /// <summary>Fill a day by rule: open tickets in the order the "what next" line would take them, until the day is full.
    /// The script player plans this way; so will Nez.</summary>
    public static DayPlan AutoPlan(GameState s, ContentDb c, PlanetGen gen, int day)
    {
        var plan = PlanFor(s, day);
        plan.Slots.Clear();
        int modems = s.Modems;   // don't book a connection there's no modem for
        foreach (var t in StaffRules.Tickets(s, c).Where(t => t.AssignedTo is null && t.LotId is not null && AppointmentRules.HouseWorkLeft(s, t.Key))
                     .Where(t => t.Kind != TicketKind.Install || modems-- > 0)
                     .OrderByDescending(t => t.Kind == TicketKind.Repair).ThenByDescending(t => t.WaitingDays))
        {
            plan.Slots.Add(new PlanSlot { TicketKey = t.Key });
            Lay(s, c, gen, plan);
            var last = plan.Slots[^1];
            if (last.Start + last.Minutes > DayEnds || last.Grey is not null) { plan.Slots.RemoveAt(plan.Slots.Count - 1); Lay(s, c, gen, plan); }
        }
        return plan;
    }

    /// <summary>Work out start times: from home at seven, travel to each in turn, work, move on. Grey where nobody will be in.</summary>
    public static void Lay(GameState s, ContentDb c, PlanetGen gen, DayPlan plan)
    {
        var tickets = StaffRules.Tickets(s, c).ToDictionary(t => t.Key);
        plan.Slots.RemoveAll(x => !tickets.ContainsKey(x.TicketKey));   // done or gone
        int clock = DayStarts; string at = Places.Room;
        foreach (var slot in plan.Slots)
        {
            var t = tickets[slot.TicketKey];
            var place = t.LotId ?? Places.Room;
            double m = Places.DistanceM(gen, c, at, place);
            slot.TravelMinutes = Places.TravelMinutes(gen, c, at, place, drive: m > Places.DriveOverM);   // the ferry's sailing too, to Farland
            slot.Start = clock + slot.TravelMinutes;
            slot.Minutes = Duration(s, c, t);
            slot.Title = t.Title;
            slot.Grey = c.Lots.TryGetValue(place, out var lot) ? AwayAt(s, c, lot, plan.Day, slot.Start) : null;
            slot.Booking = AppointmentRules.For(s, slot.TicketKey) is { } booked && booked.Day == plan.Day ? AppointmentRules.Label(booked) : null;
            // planning ahead: someone out at work, or who'd only have popped out, can be asked to be in (the appointment below),
            // so it isn't grey
            if (slot.Grey is not null && lot is not null && plan.Day > s.Day
                && (AppointmentRules.CouldBeIn(s, c, lot, plan.Day, slot.Start) || PeopleRules.OutAt(s, c, lot, plan.Day, slot.Start) is not null)) slot.Grey = null;
            clock = slot.Start + slot.Minutes; at = place;
        }
        AppointmentRules.Sync(s, c, plan, tickets);
    }

    /// <summary>The next thing on today's sheet, for the phone's notes.</summary>
    public static PlanSlot? Next(GameState s) =>
        s.Plans.FirstOrDefault(p => p.Day == s.Day && p.Who == "player")?.Slots.FirstOrDefault(x => x.Start + x.Minutes > s.Minute);

    public static string Clock(int minuteSince6) => $"{(6 + minuteSince6 / 60) % 24:00}:{minuteSince6 % 60:00}";

    /// <summary>Would nobody answer at that hour? Asked of the rules as if the clock were there, then put back.</summary>
    public static string? AwayAt(GameState s, ContentDb c, LotDef lot, int day, int minute)
    {
        int d = s.Day, mn = s.Minute;
        s.Day = day; s.Minute = minute;
        try { return HomeRules.WhyNot(s, c, lot); }
        finally { s.Day = d; s.Minute = mn; }
    }

    private static DayPlan Add(GameState s, DayPlan p)
    {
        s.Plans.RemoveAll(x => x.Day < s.Day - 7);   // last week's sheets go in the bin
        s.Plans.Add(p);
        return p;
    }
}
