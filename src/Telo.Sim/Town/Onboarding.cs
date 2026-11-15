using Telo.Sim.Content;
using Telo.Sim.World;

namespace Telo.Sim.Town;

/// <summary>What a new player has met so far. Each step introduces exactly one thing; the next is held back until this one has been done once.</summary>
public enum OnboardingStep { Enquiry, Install, Frame, OddJob, Knock, Free }

/// <summary>
/// The first hour, one idea at a time: a phone message, then an install, then the frame, then odd jobs, then door-knocking,
/// and only then everything at once. Derived from what has actually happened in the save, never scripted and never stored,
/// so it cannot fall out of step and an experienced save is simply "Free".
/// </summary>
public static class Onboarding
{
    public static OnboardingStep Current(GameState s, ContentDb c)
    {
        if (c.Lots.Count == 0 || c.Plot.Count == 0) return OnboardingStep.Free;   // no town to learn in, or no plot
        return Plot.Current(s, c).Step;
    }

    public static bool Reached(GameState s, ContentDb c, OnboardingStep step) => Current(s, c) >= step;

    /// <summary>Odd jobs on offer at once: none until they have been introduced, one while learning, three after.</summary>
    public static int JobLimit(GameState s, ContentDb c) => Current(s, c) switch
    {
        < OnboardingStep.OddJob => 0,
        < OnboardingStep.Free => 1,
        _ => 3,
    };

    /// <summary>The desk (PIP's Almanac, the company notebook) opens once the basic round is learned.</summary>
    public static bool DeskOpen(GameState s, ContentDb c) => Reached(s, c, OnboardingStep.Free);

    /// <summary>The road out of town is "being resurfaced" until there is a reason to go: staff to mind the shop, or a line already begun.</summary>
    public static bool RoadOpen(GameState s, ContentDb c) => c.Lots.Count == 0 || s.Staff > 0 || s.Lines.Values.Any(l => l.Planted > 0);

    public static bool CanKnock(GameState s, ContentDb c) => Reached(s, c, OnboardingStep.Knock);
    /// <summary>Word of mouth only starts ringing the phone once the player knows the whole loop.</summary>
    public static bool PhoneRings(GameState s, ContentDb c) => Reached(s, c, OnboardingStep.Free);
}

/// <summary>
/// The plot (game/content/plot.json): one line of chapters, no overlap. The current chapter is derived from what has happened in the save,
/// never stored, so it cannot fall out of step. The first-hour steps above are read from it.
/// </summary>
public static class Plot
{
    public static ChapterDef Current(GameState s, ContentDb c) =>
        c.Plot.FirstOrDefault(ch => !Done(s, ch)) ?? c.Plot[^1];

    public static bool Done(GameState s, ChapterDef ch) => ch.DoneWhen.Any(cond => ActRules.Met(s, cond));

    public static int Index(GameState s, ContentDb c) => c.Plot.Count == 0 ? 0 : c.Plot.ToList().IndexOf(Current(s, c));
}
