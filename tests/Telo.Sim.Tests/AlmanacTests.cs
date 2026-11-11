using Telo.Sim.Almanac;
using Telo.Sim.World;

namespace Telo.Sim.Tests;

public class AlmanacTests
{
    [Fact]
    public void Act0EntriesReadableAtStart_LaterOnesSealed()
    {
        var s = TestContent.NewGame().State;
        Assert.Equal(EntryStatus.Readable, s.Entries["alm-2400"]);
        Assert.Equal(EntryStatus.Sealed, s.Entries["alm-web"]);
        Assert.Equal(EntryStatus.Sealed, s.Entries["alm-exchange0"]);
    }

    [Fact]
    public void EarlyRecallCostsArchiveAndEcho()
    {
        var sim = TestContent.NewGame();
        sim.State.ArchiveMilli = 50_000;
        Assert.Equal(RecallResult.Ok, AlmanacRules.Recall(sim.State, sim.Content, "alm-2400"));
        Assert.Equal(42, sim.State.Archive);
        Assert.Equal(8, sim.State.Echo); // Mar 1984 from Jan 1983 → 2 years (rounded up) × 4
        Assert.Contains("alm-2400", sim.State.EarlyRecalls);
    }

    [Fact]
    public void OnTimeRecallCostsNoEcho()
    {
        var sim = TestContent.NewGame();
        sim.AdvanceDays(500); // past March 1984
        Assert.Contains("ann-2400", sim.State.FiredAnnouncements);
        sim.State.ArchiveMilli = 50_000;
        int echoBefore = sim.State.Echo;
        Assert.Equal(RecallResult.Ok, AlmanacRules.Recall(sim.State, sim.Content, "alm-2400"));
        Assert.Equal(echoBefore, sim.State.Echo);
    }

    [Fact]
    public void CannotRecallWithoutArchive()
    {
        var sim = TestContent.NewGame();
        Assert.Equal(RecallResult.NotEnoughArchive, AlmanacRules.Recall(sim.State, sim.Content, "alm-2400"));
    }

    [Fact]
    public void RequirementsUnsealEntries()
    {
        var sim = TestContent.NewGame();
        sim.State.ActIndex = 1;
        sim.State.ArchiveMilli = 100_000;
        AlmanacRules.RefreshStatuses(sim.State, sim.Content);
        Assert.Equal(EntryStatus.Sealed, sim.State.Entries["alm-hunt"]);
        AlmanacRules.Recall(sim.State, sim.Content, "alm-bbs");
        Assert.Equal(EntryStatus.Readable, sim.State.Entries["alm-hunt"]);
    }

    [Fact]
    public void CorrectionMakesNewestEarlyRecallDrift()
    {
        var sim = TestContent.NewGame();
        sim.State.ArchiveMilli = 100_000;
        AlmanacRules.Recall(sim.State, sim.Content, "alm-2400");
        sim.State.Echo = AlmanacRules.CorrectionThreshold;
        sim.AdvanceDay();
        Assert.Equal(EntryStatus.Drifting, sim.State.Entries["alm-2400"]);
        Assert.True(sim.State.Echo < AlmanacRules.CorrectionThreshold);
    }

    [Fact]
    public void CalendarLeverPullsAnnouncementForward()
    {
        var sim = TestContent.NewGame();
        var before = AlmanacRules.PublicDate(sim.State, sim.Content, "ann-2400");
        Assert.True(AlmanacRules.ShiftAnnouncement(sim.State, sim.Content, "ann-2400", -180, 100));
        Assert.Equal(before.Day - 180, AlmanacRules.PublicDate(sim.State, sim.Content, "ann-2400").Day);
    }

    [Theory]
    [InlineData(0, EchoBand.Quiet)] [InlineData(10, EchoBand.Noticed)] [InlineData(30, EchoBand.Watched)]
    [InlineData(60, EchoBand.Investigated)] [InlineData(80, EchoBand.Correction)]
    public void EchoBands(int echo, EchoBand band) => Assert.Equal(band, AlmanacRules.Band(echo));
}
