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
        if (c.Lots.Count == 0) return OnboardingStep.Free;                       // no town to learn in
        if (s.Customers.Count == 0) return OnboardingStep.Enquiry;
        if (s.LiveSubscribers == 0)
            return s.Customers.Any(x => x.Live && x.SocketFitted) ? OnboardingStep.Frame : OnboardingStep.Install;
        if (s.LiveSubscribers >= 2 || s.Staff > 0) return OnboardingStep.Free;
        if (s.JobsDone == 0) return OnboardingStep.OddJob;
        bool knocked = s.Customers.Count >= 2 || s.Households.Values.Any(h => h.RetryDay > 0);
        return knocked ? OnboardingStep.Free : OnboardingStep.Knock;
    }

    public static bool Reached(GameState s, ContentDb c, OnboardingStep step) => Current(s, c) >= step;

    /// <summary>Odd jobs on offer at once: none until they have been introduced, one while learning, three after.</summary>
    public static int JobLimit(GameState s, ContentDb c) => Current(s, c) switch
    {
        < OnboardingStep.OddJob => 0,
        < OnboardingStep.Free => 1,
        _ => 3,
    };

    public static bool CanKnock(GameState s, ContentDb c) => Reached(s, c, OnboardingStep.Knock);
    /// <summary>Word of mouth only starts ringing the phone once the player knows the whole loop.</summary>
    public static bool PhoneRings(GameState s, ContentDb c) => Reached(s, c, OnboardingStep.Free);
}
