using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.Net;
using Telo.Sim.Work;
using Telo.Sim.World;

namespace Telo.Sim.Town;

/// <summary>
/// The booking loop (D37): word of mouth (free, from reputation) and ads (a weekly cost) bring bookings; each person does 1 to 5
/// jobs a day; bookings left too long go to NezTel. Two numbers and one lever, on purpose: hire, or turn the ads down.
/// </summary>
public static class BookingRules
{
    /// <summary>Jobs you can start in a day, and the most anyone ever does.</summary>
    public const int PlayerCap = 3, MaxCap = 5;
    /// <summary>A new customer waits this long for their install, then signs with NezTel.</summary>
    public const int GiveUpDays = 21;

    /// <summary>Ads, cheapest first: weekly cost and extra bookings a week. Each unlocks as the company grows.</summary>
    public static readonly (string Name, long WeeklyCost, int PerWeek)[] Ads =
    {
        ("no ads", 0, 0), ("leaflets through doors", 10, 1), ("an ad in the paper", 25, 2), ("a spot on the local radio", 60, 4),
    };

    /// <summary>The town's opinion of you: the average satisfaction of everyone you've connected (50 before anyone).</summary>
    public static int Reputation(GameState s)
    {
        var mine = s.Customers.Where(c => c.Live && c.Installed).ToList();
        return mine.Count == 0 ? 50 : (int)mine.Average(c => c.Satisfaction);
    }

    /// <summary>The best ad you can buy yet: leaflets once the first hour is done, the paper at 6 subscribers, radio in Act 1.</summary>
    public static int MaxAdLevel(GameState s, ContentDb c) =>
        !Onboarding.PhoneRings(s, c) ? 0 : s.ActIndex >= 1 ? 3 : s.LiveSubscribers >= 6 ? 2 : 1;

    public static bool SetAds(GameState s, ContentDb c, int level)
    {
        if (level < 0 || level > MaxAdLevel(s, c)) return false;
        s.AdLevel = level;
        return true;
    }

    /// <summary>Each morning: word of mouth and ads may each bring a new booking (a message on the machine).</summary>
    public static void Day(GameState s, ContentDb c, DetRng rng)
    {
        if (!Onboarding.PhoneRings(s, c) || s.MessagesWaiting >= 9) return;
        int happy = s.Customers.Count(x => x.Live && x.Installed && x.Satisfaction >= 60);
        int wordOfMouth = Math.Min(900, 150 + 40 * happy + 4 * (Reputation(s) - 50) + 8 * BoardService.Appeal(s));
        if (rng.ChancePermille(Math.Max(50, wordOfMouth))) s.MessagesWaiting++;
        if (s.AdLevel > 0 && rng.ChancePermille(Ads[s.AdLevel].PerWeek * 1000 / 7)) s.MessagesWaiting++;
        // customers who have waited too long for their install give up and go to NezTel
        foreach (var cust in s.Customers.Where(x => x.Live && !x.SocketFitted && !x.Installed && s.Day - x.JoinedDay > GiveUpDays).ToList())
        {
            cust.Live = false;
            s.Installs.Remove(cust.Id);
            if (cust.LotId is { } lot) s.NezLots.Add(lot);
            s.Emit(EventKind.Info, $"{cust.Name} waited three weeks for their line, gave up, and signed with NezTel.", cust.LotId);
        }
    }

    /// <summary>Ads are paid weekly, like rent.</summary>
    public static void Week(GameState s)
    {
        long cost = Ads[s.AdLevel].WeeklyCost;
        if (cost <= 0) return;
        s.Cash -= cost;
        if (s.Cash < 0) s.AdLevel = 0;   // can't pay: the paper drops your ad
    }

    // ---- the daily cap ---------------------------------------------------------------------------
    public static int JobsToday(GameState s) => s.PlayerJobsDay == s.Day ? s.PlayerJobsToday : 0;
    public static bool CanStartJob(GameState s, ContentDb c) => !Onboarding.PhoneRings(s, c) || JobsToday(s) < PlayerCap;
    public static string CapReason => $"you've done {PlayerCap} jobs today; it'll keep till tomorrow";

    public static void CountJob(GameState s)
    {
        if (s.PlayerJobsDay != s.Day) { s.PlayerJobsDay = s.Day; s.PlayerJobsToday = 0; }
        s.PlayerJobsToday++;
    }

    // ---- the two numbers on the phone ------------------------------------------------------------------
    /// <summary>Bookings waiting: messages not yet played and every open ticket.</summary>
    public static int Booked(GameState s, ContentDb c) => s.MessagesWaiting + StaffRules.Tickets(s, c).Count;

    /// <summary>Jobs your people can do in a five-day week.</summary>
    public static int WeeklyCapacity(GameState s) =>
        5 * (PlayerCap + s.StaffMembers.Where(m => m.Does.Count > 0).Sum(m => Math.Min(m.JobsPerDay, MaxCap)));
}
