using Telo.Sim.Minigames.Install;
using Telo.Sim.Town;

namespace Telo.Sim.Tests;

public class OnboardingTests
{
    /// <summary>A new player meets one thing at a time, in order, and each step unlocks only by doing the one before.</summary>
    [Fact]
    public void TheFirstHourIntroducesOneThingAtATime()
    {
        var sim = TestContent.NewGame();
        var s = sim.State; var c = sim.Content;
        Assert.Equal(OnboardingStep.Enquiry, Onboarding.Current(s, c));
        Assert.Equal(1, s.MessagesWaiting);
        Assert.Empty(s.Jobs);
        Assert.Single(s.Log);                                    // one line from PIP, not a wall of notices
        sim.AdvanceDays(5);
        Assert.Empty(s.Jobs);                                    // nothing new arrives while they are still on step one
        Assert.Equal(1, s.MessagesWaiting);
        Assert.Equal(KnockResult.TooSoon, sim.Knock("lot-n1"));  // doors aren't for knocking yet

        var cust = sim.TakeEnquiry()!;
        Assert.Equal(OnboardingStep.Install, Onboarding.Current(s, c));
        var job = sim.NewInstallJob(cust.Id)!;
        while (job.NextStep is { } step) job.Do(step);
        sim.ApplyInstallOutcome(job.Evaluate());
        Assert.Equal(OnboardingStep.Frame, Onboarding.Current(s, c));

        var frame = sim.NewFrameJob(sim.FrameWorkOrder());
        foreach (var l in frame.Lines) frame.Jumper(l.Index, l.TargetPort);
        sim.ApplyFrameOutcome(frame.Evaluate());
        Assert.Equal(OnboardingStep.OddJob, Onboarding.Current(s, c));
        Assert.Single(s.Jobs);                                   // the first odd job turns up exactly now

        TownRules.CompleteJob(s, c, s.Jobs[0].Id);
        Assert.Equal(OnboardingStep.Knock, Onboarding.Current(s, c));
        for (int i = 0; i < 20 && Onboarding.Current(s, c) == OnboardingStep.Knock; i++)
            foreach (var lot in TownRules.Prospects(s, c).ToList()) sim.Knock(lot.Id);
        Assert.Equal(OnboardingStep.Free, Onboarding.Current(s, c));
        Assert.Equal(3, Onboarding.JobLimit(s, c));
    }
}
