using Telo.Sim.Minigames.OddJob;
using Telo.Sim.Town;
using Telo.Sim.Work;

namespace Telo.Sim.Tests;

public class PacingTests
{
    [Fact]
    public void GodKeysWalkTheWholeGame_StageByStageThenActByAct()
    {
        var sim = TestContent.NewGame();
        var gen = new Telo.Sim.Planet.PlanetGen(1983);
        var seen = new List<OnboardingStep>();
        for (int i = 0; i < 60 && sim.State.ActIndex < sim.Content.Acts.Count - 1; i++)
        {
            seen.Add(Onboarding.Current(sim.State, sim.Content));
            Assert.False(string.IsNullOrWhiteSpace(sim.GodNextStage(gen)));
        }
        Assert.Equal(new[] { OnboardingStep.Enquiry, OnboardingStep.Install, OnboardingStep.Frame, OnboardingStep.OddJob, OnboardingStep.Knock }, seen.Take(5));
        Assert.True(sim.State.Staff > 0);
        Assert.Contains(sim.State.Customers, c => c.PremiseId == "prem-adeyemi-farm");
        Assert.Equal(sim.Content.Acts.Count - 1, sim.State.ActIndex);
        sim.AdvanceDays(40); // and the world still runs afterwards
    }

    [Fact]
    public void ALineGoingLivePaysAConnectionFeeToday_AndQuietDaysCanBeSleptThrough()
    {
        var sim = TestContent.NewGame();
        var cust = sim.TakeEnquiry()!;
        Town.TownRules.FitSocket(sim.State, cust.Id);
        long before = sim.State.Cash;
        var frame = sim.NewFrameJob(sim.FrameWorkOrder());
        foreach (var l in frame.Lines) frame.Jumper(l.Index, l.TargetPort);
        sim.ApplyFrameOutcome(frame.Evaluate());
        Assert.Equal(before + World.Simulation.ConnectionFee(cust), sim.State.Cash);

        int day = sim.State.Day;
        int slept = sim.RestUntilNeeded();
        Assert.InRange(slept, 1, 30);
        Assert.Equal(day + slept, sim.State.Day);
    }

    [Fact]
    public void OddJobsAreDoneByHand_AndAThumpCostsGoodwill()
    {
        var sim = TestContent.NewGame();
        var job = sim.OfferJob()!;
        var task = sim.NewOddJobTask(job.Id)!;
        Assert.False(task.Do(Tools.None));               // needs the toolbox to open it up
        while (!task.Done) Assert.True(task.Do());
        Assert.True(task.ElapsedMinutes >= 20 && !task.Bodged && task.Steps.Count == 4);
        Assert.NotNull(TownRules.CompleteJob(sim.State, sim.Content, job.Id));
        int good = TownRules.HouseholdOf(sim.State, job.LotId).Goodwill;
        Assert.True(good > 0);

        var second = sim.OfferJob()!;
        var thump = sim.NewOddJobTask(second.Id)!;
        Assert.True(thump.Bodge() && thump.Done);
        long cash = sim.State.Cash;
        TownRules.CompleteJob(sim.State, sim.Content, second.Id, thump.Bodged);
        Assert.Equal(cash + second.Pay, sim.State.Cash);
        Assert.True(TownRules.HouseholdOf(sim.State, second.LotId).Goodwill < 0 || second.LotId == job.LotId);
    }
}
