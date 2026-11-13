using Telo.Sim.Net;
using Telo.Sim.Save;
using Telo.Sim.Town;
using Telo.Sim.Work;
using Telo.Sim.World;

namespace Telo.Sim.Tests;

/// <summary>Phase C: the three jobs at the sysop's terminal, and handing them to a co-sysop.</summary>
public class BoardServiceTests
{
    private static Simulation Hired()
    {
        var sim = TestContent.NewGame();
        for (int i = 0; i < 6; i++) sim.GodNextStage(null);   // five first-hour steps, then the hire
        Assert.True(sim.State.Staff > 0 && BoardService.TerminalOpen(sim.State));
        return sim;
    }

    [Fact]
    public void NewCallersMustBeLetIn_OrTheyCannotRingAndLosePatience()
    {
        var sim = Hired();
        var s = sim.State;
        Assert.Empty(BoardService.Waiting(s));                      // everyone from before the terminal is already in
        Customer? cust = null;
        for (int i = 0; i < 10 && cust is null; i++) cust = sim.TakeEnquiry();
        Assert.NotNull(cust);
        s.Modems = 5;
        Assert.True(StaffRules.Assign(s, sim.Content, $"install:{cust!.Id}", "install"));
        for (int i = 0; i < 4 && !cust.Installed; i++) sim.AdvanceDay();   // the technician connects them on their next working day
        Assert.True(cust!.Installed && !cust.HasAccount);
        Assert.Contains(Attention.Needs(s, sim.Content), n => n.Key == "callers");
        int before = cust.Satisfaction;
        sim.AdvanceDay();
        Assert.True(cust.Satisfaction < before);
        Assert.True(BoardService.LetIn(s, cust.Id));
        Assert.Empty(BoardService.Waiting(s));
    }

    [Fact]
    public void TheFileAreaDrawsPeopleIn_AndWhatIsOnOpenDisplayGetsNoticed()
    {
        var sim = Hired();
        var s = sim.State;
        sim.AdvanceDays(21);
        Assert.NotEmpty(BoardService.NewFiles(s));
        Assert.Equal(0, BoardService.Appeal(s));
        s.BoardFiles.Clear();
        for (int i = 0; i < 6; i++) s.BoardFiles.Add(new BoardFile { Id = $"f{i}", Title = $"t{i}", Kind = FileKind.Dubious, Status = FileStatus.New, Day = s.Day });
        foreach (var f in s.BoardFiles.Take(3)) Assert.True(BoardService.Decide(s, f.Id, FileStatus.Hidden));
        int hidden = BoardService.Appeal(s);
        foreach (var f in s.BoardFiles.Skip(3)) Assert.True(BoardService.Decide(s, f.Id, FileStatus.Kept));
        Assert.True(BoardService.Appeal(s) > hidden * 2);
        sim.AdvanceDays(120);
        Assert.Contains(s.Log, e => e.Text.Contains("solicitor"));    // a letter; later an invoice. Money moves; nothing ends.
    }

    [Fact]
    public void NightMailStartsInActOne_CostsByTheMinute_AndAFasterModemHalvesIt()
    {
        var sim = Hired();
        var s = sim.State;
        Assert.Equal(0, BoardService.RunMail(s));                  // no mail networks yet
        sim.GodNextAct();
        int slow = BoardService.MailMinutes(s);
        Assert.Equal(slow, BoardService.RunMail(s));
        Assert.Equal(0, BoardService.RunMail(s));                  // once a night
        Assert.Equal(slow * BoardService.CentsPerMinute, s.MailBillCents);
        s.Entries[BoardService.FastModemEntry] = EntryStatus.Recalled;
        Assert.True(BoardService.MailMinutes(s) <= (slow + 1) / 2);
        sim.AdvanceDay();
        Assert.False(Guidance.IsQuiet(s, sim.Content));            // a nightly chore by hand: no sleeping through the week
        sim.AdvanceDays(3);
        Assert.Contains(s.Log, e => e.Text.Contains("No mail"));
    }

    [Fact]
    public void TobiTakesTheBoardOver_AndKeepsEverything()
    {
        var sim = Hired();
        var s = sim.State; var c = sim.Content;
        sim.GodNextAct();
        while (s.LiveSubscribers < 6) sim.GodNextStage(null);
        s.Cash = 500;
        Assert.Equal(UpgradeResult.Ok, WorkRules.Upgrade(s, c, BoardService.WorkId));
        sim.AdvanceDays(30);
        Assert.Empty(BoardService.Waiting(s));
        Assert.Equal(s.Day, s.MailLastRunDay);
        Assert.Empty(BoardService.NewFiles(s));
        Assert.NotEmpty(s.BoardFiles);
        var reloaded = SaveCodec.FromJson(SaveCodec.ToJson(s));
        Assert.Equal(s.BoardFiles.Count, reloaded.BoardFiles.Count);
    }
}
