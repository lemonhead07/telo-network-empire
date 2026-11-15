using Telo.Sim.World;

namespace Telo.Sim.Town;

public enum Tiredness { Rested, Tired, Exhausted, Wrecked }

/// <summary>
/// The day is the waking day. You start every morning in bed, and the night is slept through, never lived: the clock
/// only runs from six in the morning to bedtime. Stay up past ten and you are borrowing from tomorrow; at two you
/// fall asleep where you stand and wake at home, late and worse for it. Short nights add up as sleep debt, and a
/// tired body works slower and makes mistakes it doesn't notice (latent defects, like a bodge).
/// Minutes here count from six in the morning, as <see cref="GameState.Minute"/> does.
/// </summary>
public static class SleepRules
{
    public const int Need = 8 * 60;                 // a proper night
    public const int Bedtime = 16 * 60;             // 22:00: the waking day ends here
    public const int Collapse = 20 * 60;            // 02:00: you drop where you stand
    public const int LatestWake = 3 * 60;           // 09:00: you never sleep past nine (the alarm, the bin men, PIP)
    public const int MaxRepayPerNight = 2 * 60;     // a long night pays back two hours of debt, no more
    public const int CollapsePenalty = 90;          // a night on the floor of the van is not a night's sleep

    /// <summary>When you wake (minutes after six next morning) and how long you slept, if you go to bed now.</summary>
    public static (int Wake, int Slept) Night(int bedMinute)
    {
        int wake = Math.Clamp(bedMinute + Need - Clock.MinutesPerDay, 0, LatestWake);
        return (wake, Clock.MinutesPerDay - bedMinute + wake);
    }

    /// <summary>Go to sleep now: settle the debt for tonight and return the minute you wake tomorrow.</summary>
    internal static int Sleep(GameState s, bool collapsed)
    {
        var (wake, slept) = Night(s.Minute);
        int owed = Need - slept + (collapsed ? CollapsePenalty : 0);
        s.SleepDebt = Math.Max(0, s.SleepDebt + Math.Max(owed, -MaxRepayPerNight));
        s.LastSleep = slept;
        return wake;
    }

    /// <summary>Debt carried in, plus tonight's: every minute awake past bedtime counts.</summary>
    public static int Effective(GameState s) => s.SleepDebt + Math.Max(0, s.Minute - Bedtime);

    public static Tiredness Level(GameState s) => Effective(s) switch
    {
        < 90 => Tiredness.Rested,
        < 240 => Tiredness.Tired,
        < 420 => Tiredness.Exhausted,
        _ => Tiredness.Wrecked,
    };

    /// <summary>How much longer work takes, per mille (1000 = as fast as you can).</summary>
    public static int WorkTimePermille(GameState s) => Level(s) switch
    {
        Tiredness.Rested => 1000, Tiredness.Tired => 1150, Tiredness.Exhausted => 1350, _ => 1600,
    };

    /// <summary>The chance a step done properly goes in wrong anyway, per mille. You won't know until it surfaces.</summary>
    public static int SlipPermille(GameState s) => Level(s) switch
    {
        Tiredness.Rested => 0, Tiredness.Tired => 30, Tiredness.Exhausted => 120, _ => 250,
    };

    public static bool PastBedtime(GameState s) => s.Minute >= Bedtime;

    /// <summary>A word for the phone, or null when you're fine.</summary>
    public static string? Describe(GameState s) => Level(s) switch
    {
        Tiredness.Rested => PastBedtime(s) ? "past bedtime" : null,
        Tiredness.Tired => "tired",
        Tiredness.Exhausted => "exhausted: slow, and slipping",
        _ => "dead on your feet",
    };
}
