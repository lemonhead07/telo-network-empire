using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.Town;
using Telo.Sim.Work;
using Telo.Sim.World;

namespace Telo.Sim.Net;

/// <summary>
/// Running the Board: the three jobs at the sysop's terminal. Let new callers in, tend the file area, run the night
/// mail call. All three are done by hand first and handed to a co-sysop later (work kind "board").
/// The file area is the canon's Borrowed Library: it is about what a community shares and what you choose to keep
/// on the shelf, never about how anything is copied.
/// </summary>
public static class BoardService
{
    public const string WorkId = "board";
    public const int LetInMinutes = 2, FileMinutes = 1, MailSetupMinutes = 5;
    public const int ShelfSize = 12, AppealCap = 40, NewFilesGoStaleAfterDays = 14;
    public const int WarningHeat = 10, FineHeat = 20;
    public const long Fine = 150;
    public const int CentsPerMinute = 40;
    public const string FastModemEntry = "alm-2400";

    private static readonly (string Title, FileKind Kind)[] Uploads =
    {
        ("Recipe index (v2, now with puddings)", FileKind.Useful), ("Tide tables for the bay", FileKind.Useful),
        ("Bus timetable, typed out by hand", FileKind.Useful), ("Household accounts template", FileKind.Useful),
        ("The night sky this month", FileKind.Useful), ("Knitting pattern converter", FileKind.Useful),
        ("Seed-sowing calendar", FileKind.Useful), ("Parish newsletter, back issues", FileKind.Useful),
        ("Text adventure: The Missing Marrow", FileKind.Fun), ("Limerick collection (clean)", FileKind.Fun),
        ("ASCII art: a horse, allegedly", FileKind.Fun), ("Parish quiz, 200 questions", FileKind.Fun),
        ("A chess problem a week", FileKind.Fun), ("Text adventure: Last Bus to Easthaven", FileKind.Fun),
        ("School maths program (the school's own copy)", FileKind.Dubious), ("\"The spreadsheet\". The expensive one", FileKind.Dubious),
        ("An arcade game that is definitely not that arcade game", FileKind.Dubious), ("Word processor, serial number scratched out", FileKind.Dubious),
        ("A pop album. As a file. Somehow", FileKind.Dubious),
    };

    /// <summary>The sysop's terminal arrives with your first hire, or with Act 1, whichever is first. Before that the home-made board lets anyone in.</summary>
    public static bool TerminalOpen(GameState s) => s.Staff > 0 || s.ActIndex >= 1;
    /// <summary>Boards only start swapping mail overnight once the hobbyist mail networks exist: Act 1.</summary>
    public static bool MailOpen(GameState s) => s.ActIndex >= 1;
    public static bool Delegated(GameState s, ContentDb c) => c.Work.ContainsKey(WorkId) && WorkRules.Current(s, c, WorkId).Mode != WorkMode.Manual;

    // ---- callers ---------------------------------------------------------------------------------
    public static List<Customer> Waiting(GameState s) => s.Customers.Where(x => x.Live && x.Installed && !x.HasAccount).ToList();

    public static bool LetIn(GameState s, string customerId)
    {
        if (s.Customers.FirstOrDefault(x => x.Id == customerId && !x.HasAccount) is not { } cust) return false;
        cust.HasAccount = true;
        return true;
    }

    // ---- the file area ---------------------------------------------------------------------------
    public static List<BoardFile> NewFiles(GameState s) => s.BoardFiles.Where(f => f.Status == FileStatus.New).ToList();
    /// <summary>What is on the shelf: the most recent things kept or kept under the counter. Old files stop drawing anyone.</summary>
    public static List<BoardFile> Shelf(GameState s) => s.BoardFiles.Where(f => f.Status is FileStatus.Kept or FileStatus.Hidden).TakeLast(ShelfSize).ToList();

    public static bool Decide(GameState s, string fileId, FileStatus status)
    {
        if (s.BoardFiles.FirstOrDefault(f => f.Id == fileId && f.Status == FileStatus.New) is not { } file || status == FileStatus.New) return false;
        file.Status = status;
        return true;
    }

    /// <summary>How much the shelf draws people in. More appeal means more word of mouth, and more people ringing in of an evening.</summary>
    public static int Appeal(GameState s) => Math.Min(AppealCap, Shelf(s).Sum(f => (f.Kind, f.Status) switch
    {
        (FileKind.Useful, FileStatus.Kept) => 2, (FileKind.Fun, FileStatus.Kept) => 3, (FileKind.Dubious, FileStatus.Kept) => 5,
        (FileKind.Dubious, FileStatus.Hidden) => 2, (_, FileStatus.Hidden) => 1, _ => 0,
    }));

    /// <summary>Once a week: a few uploads arrive, stale ones scroll off, and what is on open display gets noticed.</summary>
    public static void Week(GameState s, ContentDb c, DetRng rng)
    {
        if (!TerminalOpen(s)) return;
        foreach (var old in s.BoardFiles.Where(f => f.Status == FileStatus.New && s.Day - f.Day > NewFilesGoStaleAfterDays)) old.Status = FileStatus.Binned;
        int n = s.LiveSubscribers >= 8 ? 3 : 2;
        for (int i = 0; i < n; i++)
        {
            var (title, kind) = Uploads[rng.Next(Uploads.Length)];
            if (s.BoardFiles.Any(f => f.Title == title && f.Status != FileStatus.Binned)) continue;
            s.BoardFiles.Add(new BoardFile { Id = s.NewId("file"), Title = title, Kind = kind, Day = s.Day, Status = Delegated(s, c) ? FileStatus.Kept : FileStatus.New });
        }
        if (s.BoardFiles.Count > 200) s.BoardFiles.RemoveRange(0, s.BoardFiles.Count - 200);

        int onDisplay = Shelf(s).Count(f => f is { Kind: FileKind.Dubious, Status: FileStatus.Kept });
        s.BoardHeat = Math.Max(0, s.BoardHeat + onDisplay * 2 - 1);
        if (s.BoardHeat >= FineHeat)
        {
            s.BoardHeat -= 10; s.Cash -= Fine;
            s.Emit(EventKind.Defect, $"A second letter about the file area. This one has an invoice in it: ${Fine}. The Board has never been more popular.");
        }
        else if (s.BoardHeat >= WarningHeat && !s.BoardWarned)
        {
            s.BoardWarned = true;
            s.Emit(EventKind.Defect, "A letter arrives from a software publisher's solicitor about the file area. It uses the word \"forthwith\".");
        }
        if (s.BoardHeat < WarningHeat / 2) s.BoardWarned = false;
    }

    // ---- the night mail call ---------------------------------------------------------------------
    public static bool MailRunToday(GameState s) => s.MailLastRunDay == s.Day;

    /// <summary>Long-distance minutes tonight's swap will take. A faster modem halves it.</summary>
    public static int MailMinutes(GameState s)
    {
        int minutes = 2 + s.Customers.Count(x => x.Live && x.Installed && x.HasAccount) / 2;
        return s.Entries.GetValueOrDefault(FastModemEntry) is EntryStatus.Recalled or EntryStatus.Drifting ? (minutes + 1) / 2 : minutes;
    }

    public static int RunMail(GameState s)
    {
        if (!MailOpen(s) || MailRunToday(s)) return 0;
        int minutes = MailMinutes(s);
        s.MailLastRunDay = s.Day;
        s.MailBillCents += minutes * CentsPerMinute;
        s.MailMissed = 0;
        return minutes;
    }

    /// <summary>The co-sysop's day: everyone waiting is let in and the mail goes out. (They keep every file, too: see Week.)</summary>
    public static void CoSysopDay(GameState s, ContentDb c)
    {
        if (!Delegated(s, c)) return;
        foreach (var cust in Waiting(s)) cust.HasAccount = true;
        RunMail(s);
    }

    /// <summary>People grumble about a locked door or late mail, but nobody cancels over it alone.</summary>
    public const int GrumbleFloor = 45;

    /// <summary>Close out the day that just ended: people locked out or without mail lose patience (down to a grumble, no further).</summary>
    public static void EndOfDay(GameState s, int dayEnded)
    {
        static void Grumble(Customer c) { if (c.Satisfaction > GrumbleFloor) c.Satisfaction--; }
        foreach (var cust in Waiting(s)) Grumble(cust);
        if (!MailOpen(s)) return;
        if (s.MailLastRunDay == dayEnded) return;
        s.MailMissed++;
        if (s.MailMissed < 2) return;
        if (s.MailMissed % 2 == 0) foreach (var cust in s.Customers.Where(x => x.Live && x.Installed)) Grumble(cust);
        if (s.MailMissed == 2) s.Emit(EventKind.Defect, "No mail has come through the Board for two nights. People have noticed.");
    }

    public static long TakeMonthlyBill(GameState s)
    {
        long dollars = (s.MailBillCents + 99) / 100;
        s.MailBillCents = 0;
        return dollars;
    }
}
