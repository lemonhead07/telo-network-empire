using Telo.Sim.Content;
using Telo.Sim.World;

namespace Telo.Sim.Town;

/// <summary>
/// The time of day and the day of the week. The sim's day begins at six in the morning; <see cref="GameState.Minute"/>
/// counts from there. Time only moves as fast as the player lives it: work never jumps the clock.
/// </summary>
public static class Clock
{
    public const int MinutesPerDay = 1440, DayStartsAtHour = 6;
    public const int WorkStartsHour = 8, WorkEndsHour = 17;

    public static double Hour(GameState s) => (DayStartsAtHour + s.Minute / 60.0) % 24;
    public static DayOfWeek Weekday(int day) => new Core.GameDate(day).ToDate().DayOfWeek;
    public static bool IsWeekend(int day) => Weekday(day) is DayOfWeek.Saturday or DayOfWeek.Sunday;
    public static bool IsWorkingDay(int day) => !IsWeekend(day);
    public static bool WorkingHours(GameState s) => IsWorkingDay(s.Day) && Hour(s) >= WorkStartsHour && Hour(s) < WorkEndsHour;
    public static bool Dark(GameState s) => Hour(s) is >= 17.5 or < 7.5;
    public static string Text(GameState s) { double h = Hour(s); return $"{(int)h:00}:{(int)(h * 60) % 60:00}"; }
}

/// <summary>
/// Who is where. People keep hours: the time of day decides who is home, and that decides what you can do.
/// A new player's first hour is spared this: everyone is in until the basic round is learned.
/// </summary>
public static class HomeRules
{
    /// <summary>Households where everyone is out at work or school on a weekday.</summary>
    private static readonly HashSet<string> OutByDay = new() { "family", "student", "gamer", "curious" };

    public static bool HasCar(LotDef lot) => lot.Kind == LotKind.House && OutByDay.Contains(lot.Archetype);

    /// <summary>Their car is gone: nobody in until five.</summary>
    public static bool OutAtWork(GameState s, ContentDb c, LotDef lot) =>
        HasCar(lot) && Onboarding.Reached(s, c, OnboardingStep.Free) && Clock.IsWorkingDay(s.Day) && Clock.Hour(s) is >= 9 and < 17;

    public static bool Asleep(GameState s, ContentDb c, LotDef lot) =>
        lot.Kind != LotKind.Shop && Onboarding.Reached(s, c, OnboardingStep.Free) && Clock.Hour(s) is >= 22 or < 7;

    public static bool ShopShut(GameState s, ContentDb c, LotDef lot) =>
        lot.Kind == LotKind.Shop && Onboarding.Reached(s, c, OnboardingStep.Free)
        && (Clock.Weekday(s.Day) == DayOfWeek.Sunday || Clock.Hour(s) is < 9 or >= 17.5);

    /// <summary>Will someone come to the door right now?</summary>
    public static bool IsHome(GameState s, ContentDb c, LotDef lot) => WhyNot(s, c, lot) is null;

    /// <summary>Why nobody answers, in the words you'd use standing on the step. Null if they are in.</summary>
    public static string? WhyNot(GameState s, ContentDb c, LotDef lot)
    {
        if (ShopShut(s, c, lot)) return Clock.Weekday(s.Day) == DayOfWeek.Sunday ? "shut on Sundays" : "shut. Open nine till half past five";
        if (Asleep(s, c, lot)) return "the lights are off. Come back in the morning";
        if (OutAtWork(s, c, lot)) return "the car's gone. Back after five";
        return null;
    }
}
