using Telo.Sim.Town;
using Telo.Sim.World;
using Xunit;

namespace Telo.Sim.Tests;

public class SleepTests
{
    private static Simulation NewSim() => TestContent.NewGame();

    [Fact]
    public void BedByTen_WakesRestedAtSix_LateNightsBuildDebt_AndTwoInTheMorningPutsYouToBed()
    {
        var sim = NewSim(); var s = sim.State;

        s.Minute = SleepRules.Bedtime;                 // 22:00
        sim.Sleep();
        Assert.Equal(0, s.Minute);                     // 06:00
        Assert.Equal(0, s.SleepDebt);

        s.Minute = SleepRules.Bedtime + 210;           // in bed at half one
        sim.Sleep();
        Assert.Equal(SleepRules.LatestWake, s.Minute); // the day starts at nine at the latest...
        Assert.Equal(30, s.SleepDebt);                 // ...so that night was half an hour short
    }

    [Fact]
    public void ShortNightsAddUp_AndTirednessSlowsAndSpoilsWork()
    {
        var sim = NewSim(); var s = sim.State;
        int day = s.Day;
        // stay up till two, three nights running: you drop where you stand each time
        for (int n = 0; n < 3; n++)
        {
            s.Minute = SleepRules.Bedtime;
            sim.AdvanceMinutes(5 * 60);                // try to work on past two
            Assert.True(s.Minute <= SleepRules.LatestWake, "fell asleep and woke next morning");
        }
        Assert.Equal(day + 3, s.Day);
        Assert.True(s.SleepDebt >= 4 * 60);
        Assert.True(SleepRules.Level(s) >= Tiredness.Exhausted);
        Assert.True(SleepRules.WorkTimePermille(s) > 1000 && SleepRules.SlipPermille(s) > 0);

        // early nights pay it back, two hours a night
        for (int n = 0; n < 4; n++) { s.Minute = 14 * 60; sim.Sleep(); }   // in bed by eight
        Assert.Equal(Tiredness.Rested, SleepRules.Level(s));
    }
}
