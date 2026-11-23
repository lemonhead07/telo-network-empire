using System.Runtime.CompilerServices;
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

    // When people go out, in minutes since six (the sim's clock), and how likely it is on a given day (per thousand).
    private const int WeekendFrom = 4 * 60, WeekendTo = 11 * 60, WeekendChance = 400;          // 10:00-17:00
    private const int EveningFrom = 11 * 60 + 30, EveningTo = 13 * 60 + 30, EveningChance = 300; // 17:30-19:30, home from work
    private const int DaytimeFrom = 3 * 60 + 30, DaytimeTo = 9 * 60 + 30, DaytimeChance = 450;  // 09:30-15:30, at home all day
    /// <summary>Home again by half past nine: nobody is still out when the lights go off at ten.</summary>
    private const int LatestBack = 15 * 60 + 30;
    /// <summary>Sol's and the village businesses keep shop hours (as <see cref="HomeRules.ShopShut"/>): nine till half past five, shut on Sundays.</summary>
    private const int OpensAt = 3 * 60, ShutsAt = 11 * 60 + 30;
    /// <summary>The out-at-work households are out nine till five on a weekday (as <see cref="HomeRules.OutAtWork"/>).</summary>
    private const int WorkFrom = 3 * 60, WorkTo = 11 * 60;
    private const int ShopStayMin = 10, ShopStayMax = 20, VisitStayMin = 15, VisitStayMax = 30, ShopPermille = 600;

    /// <summary>Today's trips out for one household (none, or one). Pure: the same seed, day and lot give the same day.</summary>
    public static IReadOnlyList<Outing> Day(GameState s, ContentDb c, LotDef lot, int day)
    {
        if (Intent(s, lot, day, out int leave) is not { } rng) return Array.Empty<Outing>();

        // somewhere within a short walk where they'll find someone in: the shop while it's open, else a neighbour who's home
        var near = Near(c, lot);
        var open = new List<LotDef>(near.Length);
        foreach (var l in near)
        {
            int arrive = leave + WalkMinutesFor(Along(lot, l));
            if (Welcome(s, l, day, arrive, arrive + (IsShop(l) ? ShopStayMax : VisitStayMax))) open.Add(l);
        }
        if (open.Count == 0) return Array.Empty<Outing>();
        var shops = open.Where(IsShop).ToList();
        var dest = shops.Count > 0 && rng.ChancePermille(ShopPermille) ? shops[rng.Next(shops.Count)] : open[rng.Next(open.Count)];
        int walk = WalkMinutesFor(Along(lot, dest));
        int stay = IsShop(dest) ? rng.Range(ShopStayMin, ShopStayMax + 1) : rng.Range(VisitStayMin, VisitStayMax + 1);
        if (leave + walk + stay + walk > LatestBack) return Array.Empty<Outing>();   // too late to go far: they stay in tonight
        string why = IsShop(dest) ? $"at {Called(dest)}" : $"round at {Possessive(dest)}";
        return new[] { new Outing(lot.Id, dest.Id, leave, walk, stay, why) };
    }

    /// <summary>Where this household's person is at this minute of today (pass a fractional minute for smooth walking).</summary>
    public static Whereabouts Now(GameState s, ContentDb c, LotDef lot, double minute)
    {
        foreach (var o in Day(s, c, lot, s.Day))
        {
            if (minute < o.Leave || minute >= o.Back) continue;
            if (!Active(s, c, lot)) break;
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
    /// Would they be out on an errand at that minute of that day, if nobody asked them to stay in? Planning ahead uses it: a
    /// household that would only have popped out can be asked to be in, and a booking keeps them in all day.
    /// </summary>
    public static Outing? OutAt(GameState s, ContentDb c, LotDef lot, int day, double minute)
    {
        if (!Onboarding.Reached(s, c, OnboardingStep.Free)) return null;
        foreach (var o in Day(s, c, lot, day)) if (minute >= o.Leave && minute < o.Back) return o;
        return null;
    }

    /// <summary>
    /// Routines only run once the first hour is learned (like the rest of <see cref="HomeRules"/>). Never on a day they've
    /// booked you in (they stay in for you, before the window as well as during it), and not while an install is half done
    /// in their house (they let you in; they're waiting for you to finish).
    /// </summary>
    private static bool Active(GameState s, ContentDb c, LotDef lot)
    {
        if (!Onboarding.Reached(s, c, OnboardingStep.Free)) return false;
        foreach (var a in s.Appointments) if (a.LotId == lot.Id && a.Day == s.Day) return false;
        return TownRules.CustomerAt(s, lot.Id) is not { } cust || !s.Installs.ContainsKey(cust.Id);
    }

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

    /// <summary>Homes keep routines. Shops, businesses, the Yard, the depot and the Last Mile farms don't go anywhere.</summary>
    private static bool Walks(LotDef lot) => lot.Kind == LotKind.House && lot.Premise is null && !lot.Business;

    /// <summary>Somewhere you pop into rather than round to: Sol's, or a village business.</summary>
    private static bool IsShop(LotDef l) => l.Kind == LotKind.Shop || l.Business;

    /// <summary>
    /// Whether they go out today and when they leave (wherever to), and the dice to choose the rest. Its own seed per lot and
    /// day, so a neighbour's plans can be read without knowing yours.
    /// </summary>
    private static DetRng? Intent(GameState s, LotDef lot, int day, out int leave)
    {
        leave = 0;
        if (!Walks(lot)) return null;
        var rng = new DetRng(DetRng.Derive(s.Seed, $"walk:{lot.Id}:{day}"));
        var (from, to, chance) = Clock.IsWeekend(day) ? (WeekendFrom, WeekendTo, WeekendChance)
            : HomeRules.HasCar(lot) ? (EveningFrom, EveningTo, EveningChance)   // home from work, then round to a neighbour
            : (DaytimeFrom, DaytimeTo, DaytimeChance);                          // at home all day: a trip to the shop, or next door
        if (!rng.ChancePermille(chance)) return null;
        leave = from + rng.Next(to - from);
        return rng;
    }

    /// <summary>The longest any trip can last, there and back.</summary>
    private static int LongestTrip => 2 * WalkMinutesFor(MaxErrandM) + VisitStayMax;

    /// <summary>
    /// Will they find someone in from <paramref name="from"/> to <paramref name="until"/>? A shop must be open the whole time;
    /// a neighbour must not be out at work or out on an errand of their own (judged from when they leave, wherever to).
    /// </summary>
    private static bool Welcome(GameState s, LotDef dest, int day, int from, int until)
    {
        if (IsShop(dest)) return Clock.Weekday(day) != DayOfWeek.Sunday && from >= OpensAt && until <= ShutsAt;
        if (HomeRules.HasCar(dest) && Clock.IsWorkingDay(day) && from < WorkTo && until > WorkFrom) return false;
        if (Intent(s, dest, day, out int theirs) is not null && from < theirs + LongestTrip && until > theirs) return false;
        return true;
    }

    /// <summary>Everywhere within a short walk on the same site, in a fixed (ordinal) order. Worked out once per content.</summary>
    private static LotDef[] Near(ContentDb c, LotDef lot) =>
        NearCache.GetValue(c, BuildNear).TryGetValue(lot.Id, out var near) ? near : Array.Empty<LotDef>();

    private static readonly ConditionalWeakTable<ContentDb, Dictionary<string, LotDef[]>> NearCache = new();

    private static Dictionary<string, LotDef[]> BuildNear(ContentDb c)
    {
        var all = c.Lots.Values.OrderBy(l => l.Id, StringComparer.Ordinal).ToList();
        return all.Where(Walks).ToDictionary(lot => lot.Id, lot => all
            .Where(l => l.Id != lot.Id && l.Site == lot.Site && l.Premise is null && l.Kind is LotKind.House or LotKind.Shop && Along(lot, l) <= MaxErrandM)
            .ToArray());
    }

    /// <summary>A lot's name as you'd say it mid-sentence: "the Plough", "the vet's surgery", "Sol's Electronics".</summary>
    private static string Called(LotDef l)
    {
        string n = l.Resident;
        int paren = n.IndexOf(" (", StringComparison.Ordinal);
        if (paren > 0) n = n[..paren];
        if (n.StartsWith("The ", StringComparison.Ordinal)) return "the " + n[4..];
        // a common noun with a capital at the front ("Vet's surgery"): the vet's surgery
        if (n.Split(' ').Skip(1).Any(w => w.Length > 0 && char.IsLower(w[0]))) return "the " + char.ToLowerInvariant(n[0]) + n[1..];
        return n;
    }

    /// <summary>"Hilde Moss's", "the Lindqvists'".</summary>
    private static string Possessive(LotDef l)
    {
        string n = Called(l);
        return n.StartsWith("the ", StringComparison.Ordinal) && n.EndsWith('s') ? n + "'" : n + "'s";
    }
}
