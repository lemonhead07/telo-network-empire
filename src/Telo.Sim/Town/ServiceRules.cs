using Telo.Sim.Core;
using Telo.Sim.World;

namespace Telo.Sim.Town;

/// <summary>
/// Service is the weapon (D38, D39). Every day each customer's line is up or down: down while it has a fault, or while they
/// have waited more than a few days to be connected. Over a rolling month that is your service level, shown in nines beside
/// NezTel's, which is stuck at 99%. Beat it by a nine and NezTel's customers start ringing you.
/// VIP bookings (D39) want near-perfect work: connected by the day they asked, no bodges, the line tested. That pays a bonus
/// and lifts the whole town's opinion of you; anything less and they go to NezTel, publicly. Nobody loses progress, only face.
/// </summary>
public static class ServiceRules
{
    public const int NezTelPpm = 990_000;           // 99%, for ever
    public const int GraceDays = 3;                 // a short wait for a new line is expected, not an outage
    public const int WindowDays = 30;
    public const int VipBonus = 120, VipLeadDays = 2, VipChancePermille = 400;

    /// <summary>Your service level in parts per million of line-days up (1,000,000 before there's anyone to serve).</summary>
    public static int Ppm(GameState s) => s.ServiceTotal <= 0 ? 1_000_000 : (int)Math.Min(1_000_000, s.ServiceUp * 1_000_000 / s.ServiceTotal);

    /// <summary>How many nines: 99% is 2, 99.9% is 3, and so on.</summary>
    public static int Nines(int ppm) => ppm >= 999_990 ? 5 : ppm >= 999_900 ? 4 : ppm >= 999_000 ? 3 : ppm >= 990_000 ? 2 : ppm >= 900_000 ? 1 : 0;

    /// <summary>"99.4%", "99.95%": as many decimals as the nines need.</summary>
    public static string Percent(int ppm)
    {
        if (ppm >= 1_000_000) return "100%";
        int decimals = Math.Clamp(Nines(ppm) - 1, 1, 4);
        return (ppm / 10_000.0).ToString("F" + decimals, System.Globalization.CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>Every morning: count yesterday's line-days, answer VIPs whose day has gone, and (weekly) people leave NezTel or a VIP books.</summary>
    internal static void Day(GameState s, Content.ContentDb c, DetRng rng)
    {
        // a rolling month, in thousandths of a line-day so the decay stays exact enough in whole numbers
        s.ServiceUp = s.ServiceUp * (WindowDays - 1) / WindowDays;
        s.ServiceTotal = s.ServiceTotal * (WindowDays - 1) / WindowDays;
        foreach (var cust in s.Customers.Where(x => x.Live))
        {
            bool waitingTooLong = !cust.Installed && s.Day - cust.JoinedDay > GraceDays;
            if (cust.Installed || waitingTooLong) s.ServiceTotal += 1000;
            if (cust.Installed && !s.Faults.Any(f => f.CustomerId == cust.Id)) s.ServiceUp += 1000;
        }

        // a VIP whose day has passed without their line in
        foreach (var vip in s.Customers.Where(x => x.Live && x.VipDay is { } d && d < s.Day && !x.SocketFitted).ToList())
            VipLost(s, c, vip, "their day came and went");

        if (s.Day % 7 != 0 || !Onboarding.Reached(s, c, OnboardingStep.Free)) return;
        // beating NezTel by a nine: its customers start to switch, a few a week, more as the gap grows
        int ppm = Ppm(s), gap = Nines(ppm) - Nines(NezTelPpm);
        for (int i = 0; i < gap && s.NezLots.Count > 0; i++)
        {
            var lot = s.NezLots.OrderBy(x => x).First();
            s.NezLots.Remove(lot);
            var h = TownRules.HouseholdOf(s, lot);
            h.Status = HouseholdStatus.Unknown; h.RetryDay = s.Day;
            s.MessagesWaiting = Math.Min(9, s.MessagesWaiting + 1);
            string who = c.Lots.TryGetValue(lot, out var l) ? l.Resident : "Someone";
            s.Emit(EventKind.Milestone, $"{who} is fed up with NezTel's {Percent(NezTelPpm)} and rang you. Your lines are up {Percent(ppm)} of the time.", lot);
        }
        // a VIP booking: someone waiting for a line who needs it by a set day
        if (s.LiveSubscribers >= 3 && s.Customers.FirstOrDefault(x => x.Live && !x.SocketFitted && x.VipDay is null && x.LotId is not null) is { } next
            && !s.Customers.Any(x => x.VipDay is not null) && rng.ChancePermille(VipChancePermille))
        {
            next.VipDay = s.Day + VipLeadDays;
            s.Emit(EventKind.Milestone, $"VIP: {next.Name} needs their line in by {new GameDate(next.VipDay.Value)}. On the day, no bodges, tested. ${VipBonus} if it's right; NezTel if it isn't.", next.LotId);
        }
    }

    /// <summary>A house install was signed off. For a VIP, judge it: in time, no bodges, tested.</summary>
    internal static void InstallSignedOff(GameState s, Content.ContentDb c, Customer cust, bool tested, int defects)
    {
        if (cust.VipDay is not { } day) return;
        if (s.Day <= day && tested && defects == 0) VipWon(s, c, cust);
        else VipLost(s, c, cust, s.Day > day ? "it was late" : !tested ? "nobody tested the line" : "it wasn't right");
    }

    internal static void VipWon(GameState s, Content.ContentDb c, Customer cust)
    {
        cust.VipDay = null;
        s.Cash += VipBonus;
        cust.Satisfaction = 100;
        foreach (var x in s.Customers.Where(x => x.Live && x.Installed)) x.Satisfaction = Math.Min(100, x.Satisfaction + 2);   // word gets round
        s.Emit(EventKind.Milestone, $"VIP job done right: {cust.Name}'s line, on the day, tested, not a wire out of place. ${VipBonus}, and the town noticed.", cust.LotId);
    }

    private static void VipLost(GameState s, Content.ContentDb c, Customer cust, string why)
    {
        cust.VipDay = null;
        cust.Live = false;
        s.Installs.Remove(cust.Id);
        if (cust.LotId is { } lot) s.NezLots.Add(lot);
        foreach (var x in s.Customers.Where(x => x.Live && x.Installed)) x.Satisfaction = Math.Max(0, x.Satisfaction - 2);      // only face
        s.Emit(EventKind.Defect, $"{cust.Name} went to NezTel: {why}. A Network Nerd's van was outside by teatime.", cust.LotId);
    }
}
