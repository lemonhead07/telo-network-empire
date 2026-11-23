using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.World;

namespace Telo.Sim.Town;

/// <summary>How someone feels about their line, as you'd read it from their face in the street.</summary>
public enum Mood { Happy, Fine, Fed }

/// <summary>Whose line a household is on.</summary>
public enum Provider { Yours, NezTel, None }

/// <summary>Where a person is in their day.</summary>
public enum WhereKind { Home, Walking, There }

/// <summary>
/// One trip out of the house and back: leave at <see cref="Leave"/> (minutes since six this morning, like
/// <see cref="GameState.Minute"/>), walk to <see cref="To"/> (a lot id on the same site), stay a while, walk home.
/// </summary>
public sealed record Outing(string LotId, string To, int Leave, int WalkMinutes, int StayMinutes, string Why)
{
    public int Arrive => Leave + WalkMinutes;
    public int StartBack => Arrive + StayMinutes;
    public int Back => StartBack + WalkMinutes;
}

/// <summary>Where someone is right now. While walking, <see cref="T"/> runs 0..1 from <see cref="From"/> to <see cref="To"/>.</summary>
public readonly record struct Whereabouts(WhereKind Kind, string From, string To, double T, Outing? Outing);

/// <summary>
/// People who walk (D44, EA2c). Every household keeps a small daily routine: most days someone pops out once, to Sol's
/// or round to a neighbour on the same street, and comes back. It is a pure schedule from the seed, the day and the
/// household, so nothing is saved and the game and the sim always agree. While they are out, nobody answers their door
/// (<see cref="HomeRules.WhyNot"/>): you may have just passed them on the pavement.
/// Far-off regions are counted, not walked (only lots in <c>lots.json</c> have people).
/// </summary>
public static class PeopleRules
{
    /// <summary>How fast people walk, in real metres a second (the player walks 2.8). A game minute is a third of a real second.</summary>
    public const double WalkMps = 1.8;
    /// <summary>Furthest anyone goes on foot, measured along the streets (east-west plus north-south).</summary>
    public const double MaxErrandM = 50;

    /// <summary>Today's trips out for one household (none, or one). Pure: the same seed, day and lot give the same day.</summary>
    public static IReadOnlyList<Outing> Day(GameState s, ContentDb c, LotDef lot, int day)
    {
        if (lot.Kind != LotKind.House || lot.Premise is not null) return Array.Empty<Outing>();
        var rng = new DetRng(DetRng.Derive(s.Seed, $"walk:{lot.Id}:{day}"));
        bool weekend = Clock.IsWeekend(day);
        bool commuter = HomeRules.HasCar(lot);
        // when they might go out (hours of the clock) and how likely it is today
        var (from, to, chance) = weekend ? (10.0, 17.0, 400)
            : commuter ? (17.5, 19.5, 300)      // home from work, then round to a neighbour
            : (9.5, 15.5, 500);                  // at home all day: a trip to the shop, or next door
        if (!rng.ChancePermille(chance)) return Array.Empty<Outing>();
        int leave = (int)((from - Clock.DayStartsAtHour) * 60) + rng.Next((int)((to - from) * 60));
        double leaveHour = Clock.DayStartsAtHour + leave / 60.0;

        // somewhere on the same site within a short walk: the shop if it's open then, else a neighbour
        var near = c.Lots.Values
            .Where(l => l.Id != lot.Id && l.Site == lot.Site && l.Kind is LotKind.House or LotKind.Shop && Along(lot, l) <= MaxErrandM)
            .Where(l => l.Kind != LotKind.Shop || ShopOpen(day, leaveHour))
            .OrderBy(l => l.Id, StringComparer.Ordinal).ToList();
        if (near.Count == 0) return Array.Empty<Outing>();
        var shop = near.FirstOrDefault(l => l.Kind == LotKind.Shop);
        var dest = shop is not null && rng.ChancePermille(600) ? shop : near[rng.Next(near.Count)];
        int walk = WalkMinutesFor(Along(lot, dest));
        int stay = dest.Kind == LotKind.Shop ? rng.Range(10, 20) : rng.Range(15, 30);
        string why = dest.Kind == LotKind.Shop ? $"at {dest.Resident}" : $"round at {dest.Resident}'s";
        return new[] { new Outing(lot.Id, dest.Id, leave, walk, stay, why) };
    }

    /// <summary>Where this household's person is at this minute of today (pass a fractional minute for smooth walking).</summary>
    public static Whereabouts Now(GameState s, ContentDb c, LotDef lot, double minute)
    {
        if (!Active(s, c, lot)) return new(WhereKind.Home, lot.Id, lot.Id, 0, null);
        foreach (var o in Day(s, c, lot, s.Day))
        {
            if (minute < o.Leave || minute >= o.Back) continue;
            if (minute < o.Arrive) return new(WhereKind.Walking, lot.Id, o.To, (minute - o.Leave) / o.WalkMinutes, o);
            if (minute < o.StartBack) return new(WhereKind.There, o.To, o.To, 1, o);
            return new(WhereKind.Walking, o.To, lot.Id, (minute - o.StartBack) / o.WalkMinutes, o);
        }
        return new(WhereKind.Home, lot.Id, lot.Id, 0, null);
    }

    public static Whereabouts Now(GameState s, ContentDb c, LotDef lot) => Now(s, c, lot, s.Minute);

    /// <summary>Out of the house right now (walking or at the other end), with the trip; null when they're in.</summary>
    public static Outing? Out(GameState s, ContentDb c, LotDef lot) =>
        Now(s, c, lot) is { Kind: not WhereKind.Home, Outing: { } o } ? o : null;

    /// <summary>On their own doorstep: why nobody answers while they're out. Null when they're in.</summary>
    public static string? WhyOut(GameState s, ContentDb c, LotDef lot) =>
        Out(s, c, lot) is { } o ? $"out {o.Why}. Back by {Clock.TextAt(o.Back)}" : null;

    /// <summary>
    /// Routines only run once the first hour is learned (like the rest of <see cref="HomeRules"/>), and never on a day
    /// they've booked you in: they stay in for you.
    /// </summary>
    private static bool Active(GameState s, ContentDb c, LotDef lot) =>
        Onboarding.Reached(s, c, OnboardingStep.Free) && Plan.AppointmentRules.Booked(s, lot.Id) is null;

    // ---- how they feel and whose line they're on ---------------------------------------------------
    public static Provider ProviderOf(GameState s, ContentDb c, LotDef lot) =>
        TownRules.CustomerAt(s, lot.Id) is not null ? Provider.Yours
        : lot.Premise is not null ? Provider.None
        : Provider.NezTel;

    /// <summary>Your customers wear their satisfaction; everyone else just puts up with the monopoly's line.</summary>
    public static Mood MoodOf(GameState s, ContentDb c, LotDef lot) =>
        TownRules.CustomerAt(s, lot.Id) is { } cust
            ? cust.Satisfaction >= 70 ? Mood.Happy : cust.Satisfaction >= 50 ? Mood.Fine : Mood.Fed
            : Mood.Fine;

    public static string MoodWord(Mood m) => m switch { Mood.Happy => "happy", Mood.Fine => "getting by", _ => "fed up" };

    public static string ProviderWord(Provider p) => p switch
    {
        Provider.Yours => "on your line", Provider.NezTel => "on NezTel's line", _ => "no line at all",
    };

    /// <summary>What you learn looking at them: "Dot Brennan · happy · on your line".</summary>
    public static string Describe(GameState s, ContentDb c, LotDef lot) =>
        $"{lot.Resident} · {MoodWord(MoodOf(s, c, lot))} · {ProviderWord(ProviderOf(s, c, lot))}";

    // ---- helpers -----------------------------------------------------------------------------------
    /// <summary>Metres along the streets between two lots on the same site (east-west plus north-south).</summary>
    public static double Along(LotDef a, LotDef b) => Math.Abs(a.East - b.East) + Math.Abs(a.South - b.South);

    public static int WalkMinutesFor(double metres) => Math.Max(1, (int)Math.Ceiling(metres / WalkMps * Act.Places.MinutesPerSecond));

    private static bool ShopOpen(int day, double hour) => Clock.Weekday(day) != DayOfWeek.Sunday && hour is >= 9 and < 17;
}
