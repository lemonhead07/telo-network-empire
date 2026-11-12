using Telo.Sim.Minigames.Install;

namespace Telo.Sim.Tests;

public class InstallTests
{
    private static InstallJob Make(ulong seed, int faultPermille = 350) => InstallJob.Generate(seed, "c1", "Ada", faultPermille);

    /// <summary>The intended way: every stage by hand, in order, then test and redo whatever the test finds.</summary>
    private static void DoProperly(InstallJob j)
    {
        while (j.NextStep is { } step) Assert.True(j.Do(step));
        while (!j.Test().DialTone) while (j.NextStep is { } step) Assert.True(j.Do(step));
    }

    [Fact]
    public void SameSeedSameJob_AndProperWorkIsPerfect()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var a = Make(seed); var b = Make(seed);
            DoProperly(a); DoProperly(b);
            Assert.Equal(a.ElapsedMinutes, b.ElapsedMinutes);
            var o = a.Evaluate();
            Assert.True(o.Perfect && o.Tested && o.OvertimeMinutes == 0);
        }
    }

    [Fact]
    public void StagesGoInOrder()
    {
        var j = Make(3);
        Assert.False(j.Do(InstallStep.Socket));
        Assert.False(j.Test().DialTone);
        Assert.False(j.Evaluate().Complete);
        Assert.True(j.Do(InstallStep.DropWire));
    }

    [Fact]
    public void BodgingIsFastAndLeavesALatentDefectTheTestCannotSee()
    {
        var j = Make(5, faultPermille: 0);
        Assert.True(j.Rush(InstallStep.DropWire));
        while (j.NextStep is { } step) j.Do(step);
        Assert.True(j.Test().DialTone);
        var o = j.Evaluate();
        Assert.True(o.Complete);
        Assert.Single(o.Defects);
    }

    [Fact]
    public void SkippingTheTestLeavesTheHiddenFault_AndTheSoftClockNeverFails()
    {
        var j = Make(9, faultPermille: 1000);
        while (j.NextStep is { } step) j.Do(step);
        var skipped = j.Evaluate();
        Assert.True(skipped.Complete && !skipped.Tested);
        Assert.Single(skipped.Defects);

        Assert.False(j.Test().DialTone);
        while (j.NextStep is { } step) j.Do(step);
        for (int i = 0; i < 40; i++) Assert.True(j.Test().DialTone); // dither with the handset all afternoon
        var late = j.Evaluate();
        Assert.True(late.Complete && late.Perfect && late.OvertimeMinutes > 0);
    }

    [Fact]
    public void SigningOffUsesAModemAndPlantsDefects()
    {
        var sim = TestContent.NewGame();
        var cust = sim.TakeEnquiry()!;
        var job = sim.NewInstallJob(cust.Id)!;
        job.Rush(InstallStep.DropWire);
        while (job.NextStep is { } step) job.Do(step);
        int modems = sim.State.Modems, defects = sim.State.Defects.Count;
        Assert.True(sim.ApplyInstallOutcome(job.Evaluate()));
        Assert.True(cust.SocketFitted);
        Assert.Equal(modems - 1, sim.State.Modems);
        Assert.True(sim.State.Defects.Count > defects);
        Assert.Null(sim.NewInstallJob(cust.Id));
    }
}
