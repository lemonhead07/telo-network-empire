using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.Work;
using Telo.Sim.World;

namespace Telo.Sim.Town;

/// <summary>A job someone has asked for, with the day they want it done. Saved (v12).</summary>
public sealed class Booking
{
    /// <summary>The job ticket it is for ("install:cust-3", "order:order-9").</summary>
    public required string Key { get; set; }
    public int MadeDay { get; set; }
    public int WantedDay { get; set; }
}

/// <summary>
/// The bookings loop (D37, BOOKINGS_AND_NEZTEL.md section 2). Word of mouth (free, grows with reputation) and leaflets
/// (a weekly cost you set at the ad board) ring the answering machine; every new line or order lands on the calendar with
/// the day the customer wants it. Each person does a few jobs a day: you three, a new hire one or two, nobody more than five.
/// Too many bookings and they won't fit; left too long, the customer goes to the national phone monopoly instead.
/// So the choice is always the same, and easy: hire someone, or take a leaflet down.
/// </summary>
public static class BookingRules
{
    /// <summary>Jobs the player can do by hand in a day (installs, orders, odd jobs; repairs are never refused).</summary>
    public const int PlayerJobsPerDay = 3;
    /// <summary>Nobody, however good, does more than this many jobs in a day.</summary>
    public const int MaxJobsPerDay = 5;
    /// <summary>A customer wants the job done between one and four days after they ask.</summary>
    public const int LeadMin = 1, LeadMax = 4;
    /// <summary>Days past the wanted day before they give up and go elsewhere.</summary>
    public const int GraceDays = 3;
    public const int MaxLeaflets = 4;
    public const long LeafletPerWeek = 6;
    /// <summary>Each leaflet up brings this chance (permille) of one more message on the machine each day.</summary>
    public const int LeafletPermille = 450;

    public const string FullDay = "That's a full day: three jobs done. The rest is booked for tomorrow.";

    /// <summary>Why the player can't start another job today, or null.</summary>
    public static string? DayFull(GameState s) => s.HandJobsToday >= PlayerJobsPerDay ? FullDay : null;

    /// <summary>A job done by hand: one of today's three.</summary>
    public static void HandJobDone(GameState s) => s.HandJobsToday++;

    /// <summary>How the town feels about you: everyone's satisfaction, averaged (B1: on top of satisfaction, not instead). 50 with nobody connected.</summary>
    public static int Reputation(GameState s)
    {
        var live = s.Customers.Where(x => x.Live && x.Installed).ToList();
        return live.Count == 0 ? 50 : (int)live.Average(x => x.Satisfaction);
    }

    // ---- the calendar ----------------------------------------------------------------------------
    /// <summary>The jobs that take a booking: a new line still to be put in at the house, and catalogue orders.</summary>
    private static IEnumerable<string> BookableKeys(GameState s)
    {
        foreach (var cust in s.Customers.Where(x => x.Live && !x.Installed && !x.SocketFitted && x.PremiseId is null)) yield return $"install:{cust.Id}";
        foreach (var o in s.Orders) yield return $"order:{o.Id}";
    }

    /// <summary>Every bookable job has a booking, and every booking a job. The wanted day is seeded by the job, so it is the same every run.</summary>
    public static void Sync(GameState s)
    {
        var keys = BookableKeys(s).ToHashSet();
        foreach (var gone in s.Bookings.Keys.Where(k => !keys.Contains(k)).ToList()) s.Bookings.Remove(gone);
        foreach (var k in keys.Where(k => !s.Bookings.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal))
            s.Bookings[k] = new Booking { Key = k, MadeDay = s.Day, WantedDay = s.Day + Lead(s, k) };
    }

    private static int Lead(GameState s, string key) => LeadMin + (int)(DetRng.Derive(s.Seed, $"book:{key}") % (ulong)(LeadMax - LeadMin + 1));

    public static Booking? Of(GameState s, string ticketKey) => s.Bookings.GetValueOrDefault(ticketKey);

    /// <summary>How many jobs of this kind one person can still take on a day.</summary>
    private static int Room(GameState s, int day, StaffMember? m, TicketKind kind)
    {
        bool today = day == s.Day;
        if (today && Clock.Hour(s) >= Clock.WorkEndsHour) return 0;   // the day is as good as over
        if (m is null) return Math.Max(0, PlayerJobsPerDay - (today ? s.HandJobsToday : 0));
        if (!m.Does.Contains(kind) || !Clock.IsWorkingDay(day)) return 0;
        return Math.Max(0, Math.Min(MaxJobsPerDay, m.JobsPerDay) - (today ? m.JobsToday : 0));
    }

    /// <summary>
    /// Lay every booking on the calendar, earliest wanted first, into the first day someone has room (staff before you).
    /// Returns the day each one fits, or -1 if nothing within <paramref name="horizon"/> days. Pure: it changes nothing.
    /// </summary>
    public static Dictionary<string, int> Fit(GameState s, int horizon = 14)
    {
        var used = new Dictionary<(int Day, string Who), int>();
        var fit = new Dictionary<string, int>();
        foreach (var b in s.Bookings.Values.OrderBy(b => b.WantedDay).ThenBy(b => b.MadeDay).ThenBy(b => b.Key, StringComparer.Ordinal))
        {
            var kind = b.Key.StartsWith("order:") ? TicketKind.Order : TicketKind.Install;
            int at = -1;
            for (int d = s.Day; d < s.Day + horizon && at < 0; d++)
                foreach (var m in s.StaffMembers.Cast<StaffMember?>().Append(null))
                {
                    var who = m?.Id ?? "player";
                    if (used.GetValueOrDefault((d, who)) >= Room(s, d, m, kind)) continue;
                    used[(d, who)] = used.GetValueOrDefault((d, who)) + 1;
                    at = d; break;
                }
            fit[b.Key] = at;
        }
        return fit;
    }

    /// <summary>Bookings that won't be done by the day the customer wants (already late, or no room in time).</summary>
    public static int Late(GameState s)
    {
        var fit = Fit(s);
        return s.Bookings.Values.Count(b => b.WantedDay < s.Day || fit[b.Key] < 0 || fit[b.Key] > b.WantedDay);
    }

    /// <summary>The two numbers on the phone: bookings wanted in the next seven days (late ones included), and jobs your people can do in them.</summary>
    public static (int Booked, int Room) Week(GameState s)
    {
        int booked = s.Bookings.Values.Count(b => b.WantedDay < s.Day + 7);
        int room = 0;
        for (int d = s.Day; d < s.Day + 7; d++)
        {
            room += Room(s, d, null, TicketKind.Install);
            foreach (var m in s.StaffMembers) room += Math.Max(Room(s, d, m, TicketKind.Install), Room(s, d, m, TicketKind.Order));
        }
        return (booked, room);
    }

    // ---- the day turns over ------------------------------------------------------------------------
    /// <summary>
    /// A new morning: yesterday's tally is wiped, leaflets are paid for on Sundays, and anyone left waiting too long
    /// goes to the national phone monopoly. A job already started, already handed to someone, or a Promise Ledger home never does.
    /// </summary>
    public static void NewDay(GameState s, ContentDb c)
    {
        s.HandJobsToday = 0;
        if (s.Day % 7 == 0 && s.Leaflets > 0) s.Cash -= s.Leaflets * LeafletPerWeek;
        Sync(s);
        if (!Onboarding.Reached(s, c, OnboardingStep.Free)) return;
        foreach (var b in s.Bookings.Values.Where(b => s.Day > b.WantedDay + GraceDays).OrderBy(b => b.Key, StringComparer.Ordinal).ToList())
        {
            if (s.Assigned.ContainsKey(b.Key)) continue;
            if (b.Key.StartsWith("install:") && s.Customers.FirstOrDefault(x => $"install:{x.Id}" == b.Key) is { } cust)
            {
                if (cust.PremiseId is not null || cust.SocketFitted || s.Installs.ContainsKey(cust.Id)) continue;
                cust.Live = false;
                s.Bookings.Remove(b.Key);
                if (cust.LotId is { } lotId && c.Lots.TryGetValue(lotId, out var lot) && lot.Kind == LotKind.House && lot.Premise is null) s.NezLots.Add(lotId);
                else if (cust.LotId is { } other) TownRules.HouseholdOf(s, other).Status = HouseholdStatus.Declined;
                s.Emit(EventKind.Info, $"{cust.Name} waited {s.Day - b.MadeDay} days for their line and signed with the national phone monopoly instead.", cust.LotId);
            }
            else if (b.Key.StartsWith("order:") && s.Orders.FirstOrDefault(o => $"order:{o.Id}" == b.Key) is { } order)
            {
                s.Orders.Remove(order);
                s.Bookings.Remove(b.Key);
                if (s.Customers.FirstOrDefault(x => x.Id == order.CustomerId) is not { } buyer) continue;
                buyer.Satisfaction = Math.Max(0, buyer.Satisfaction - 3);
                s.Emit(EventKind.Info, $"{buyer.Name} stopped waiting and got their {c.Products[order.ProductId].Name.ToLowerInvariant()} from the national phone monopoly.", buyer.LotId);
            }
        }
    }

    /// <summary>The ad board: pin a leaflet up (more calls, ${LeafletPerWeek} a week each) or take one down.</summary>
    public static bool Pin(GameState s, ContentDb c)
    {
        if (s.Leaflets >= MaxLeaflets || !Onboarding.Reached(s, c, OnboardingStep.Free)) return false;
        s.Leaflets++;
        return true;
    }

    public static bool Unpin(GameState s)
    {
        if (s.Leaflets <= 0) return false;
        s.Leaflets--;
        return true;
    }

    /// <summary>Messages the leaflets bring in today (each one up is a chance of another call).</summary>
    public static int LeafletCalls(GameState s, DetRng rng)
    {
        int n = 0;
        for (int i = 0; i < s.Leaflets; i++) if (rng.ChancePermille(LeafletPermille)) n++;
        return n;
    }
}
