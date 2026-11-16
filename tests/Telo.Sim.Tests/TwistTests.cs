using Telo.Sim.Minigames.Install;
using Xunit;

namespace Telo.Sim.Tests;

public class TwistTests
{
    [Fact]
    public void Same_seed_same_twist_a_twist_adds_one_action_and_bodging_past_it_leaves_its_trouble()
    {
        // the same seed gives the same twist (and the same hidden fault as with twists off)
        for (ulong seed = 1; seed < 60; seed++)
        {
            var a = InstallJob.Generate(seed, "c", "Ada", twists: true);
            var b = InstallJob.Generate(seed, "c", "Ada", twists: true);
            Assert.Equal(a.Twist?.Id, b.Twist?.Id);
        }
        // some houses have one, most don't; a new player's first install never does
        var jobs = Enumerable.Range(1, 300).Select(i => InstallJob.Generate((ulong)i, "c", "Ada", twists: true)).ToList();
        Assert.InRange(jobs.Count(j => j.Twist is not null), 60, 140);
        Assert.All(Enumerable.Range(1, 50), i => Assert.Null(InstallJob.Generate((ulong)i, "c", "Ada").Twist));

        // exactly one more action, on its stage, done first
        var t = jobs.First(j => j.Twist is not null);
        var stage = t.Twist!.Stage;
        Assert.Equal(InstallJob.Def(stage).Clicks + 1, t.Clicks(stage));
        Assert.Equal(InstallJob.Steps.Where(d => d.Step != InstallStep.Test).Sum(d => d.Clicks) + 1,
                     InstallJob.Steps.Where(d => d.Step != InstallStep.Test).Sum(d => t.Clicks(d.Step)));

        // done properly: the twist is dealt with and leaves nothing behind
        var careful = InstallJob.Generate(Seed(t), "c", "Ada", faultPermille: 0, twists: true);
        foreach (var d in InstallJob.Steps.Where(d => d.Step != InstallStep.Test))
            for (int k = 0; k < careful.Clicks(d.Step); k++) Assert.True(careful.Do(d.Step));
        Assert.Empty(careful.Evaluate().Defects);

        // bodge the twist's stage instead: its own trouble goes into the wall too
        var rushed = InstallJob.Generate(Seed(t), "c", "Ada", faultPermille: 0, twists: true);
        foreach (var d in InstallJob.Steps.Where(d => d.Step != InstallStep.Test))
        {
            if (d.Step == stage && d.CanRush) { Assert.True(rushed.Rush(d.Step)); continue; }
            for (int k = 0; k < rushed.Clicks(d.Step); k++) rushed.Do(d.Step);
        }
        Assert.Contains(t.Twist.Defect, rushed.Evaluate().Defects);

        ulong Seed(InstallJob j) => (ulong)(jobs.IndexOf(j) + 1);
    }
}
