using Telo.Sim.Core;

namespace Telo.Sim.World;

public enum EntryStatus { Sealed, Readable, Recalled, Drifting }
public enum PromiseStatus { Unserved, Promised, Served, Broken }

public sealed class Customer
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Archetype { get; set; }
    public long MonthlyFee { get; set; }
    /// <summary>0..100.</summary>
    public int Satisfaction { get; set; } = 70;
    public int JoinedDay { get; set; }
    public bool Live { get; set; } = true;
    /// <summary>False until someone physically jumpers them in at the switchboard. Nothing works just because it was sold.</summary>
    public bool Installed { get; set; } = true;
    /// <summary>Set when this customer came from the Promise Ledger — a named person, not a number.</summary>
    public string? PremiseId { get; set; }
    /// <summary>The building they live in, if any.</summary>
    public string? LotId { get; set; }
    /// <summary>The wall socket and modem are in at their house. Needed before the switchboard work means anything.</summary>
    public bool SocketFitted { get; set; } = true;
    /// <summary>Which exchange their line runs to (v4).</summary>
    public string ExchangeId { get; set; } = "ex-town";
    /// <summary>Catalogue products fitted here (v4).</summary>
    public List<string> Products { get; set; } = new();
    /// <summary>They have been let in to the Board (v5). Without an account they can't ring in.</summary>
    public bool HasAccount { get; set; } = true;
}

public enum HouseholdStatus { Unknown, Declined, Signed }

public sealed class Household
{
    public HouseholdStatus Status { get; set; }
    /// <summary>Earliest day it is polite to knock again.</summary>
    public int RetryDay { get; set; }
    /// <summary>Favours done here make them likelier to say yes (permille bonus).</summary>
    public int Goodwill { get; set; }
}

public sealed class OddJob
{
    public required string Id { get; set; }
    public required string LotId { get; set; }
    public required string Title { get; set; }
    public long Pay { get; set; }
    public int Hours { get; set; }
    public int ExpiresDay { get; set; }
}

public sealed class LatentDefect
{
    public required string Id { get; set; }
    public int SurfaceDay { get; set; }
    public required string Source { get; set; }
    public string? CustomerId { get; set; }
    public string Text { get; set; } = "";
}

/// <summary>A defect that has surfaced: the customer has a bad line until someone goes and fixes it.</summary>
public sealed class Fault
{
    public required string Id { get; set; }
    public required string CustomerId { get; set; }
    public string Text { get; set; } = "";
    public string Source { get; set; } = "";
    public int OpenedDay { get; set; }
}

/// <summary>How far a hand-built pole line has got.</summary>
public sealed class LineProgress
{
    public int Planted { get; set; }
    public int Strung { get; set; }
}

/// <summary>A place lines run to: your frame and the Board behind it. Later, cabinets and other towns.</summary>
public sealed class Exchange
{
    public required string Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>How many callers the Board can answer at once.</summary>
    public int LinesIn { get; set; } = 2;
}

/// <summary>How one evening went at an exchange.</summary>
public sealed class EveningReport
{
    public int Day { get; set; }
    public int LinesIn { get; set; }
    public int Attempts { get; set; }
    /// <summary>Engaged tones heard.</summary>
    public int Engaged { get; set; }
    /// <summary>Customers who never got through.</summary>
    public List<string> GaveUp { get; set; } = new();
    public int Peak { get; set; }
    public int BusiestSlot { get; set; }
    /// <summary>Lines in use in each half-hour from 18:00.</summary>
    public int[] InUse { get; set; } = Array.Empty<int>();
}

/// <summary>A house install part-way through, so a save in the middle of a job comes back where it was.</summary>
public sealed class InstallProgress
{
    public int[] Progress { get; set; } = Array.Empty<int>();
    public bool[] Rushed { get; set; } = Array.Empty<bool>();
    public int ElapsedMinutes { get; set; }
    public bool Tested { get; set; }
    public bool FaultFixed { get; set; }
    public int KnownFault { get; set; } = -1;
}

/// <summary>Something a subscriber has rung up to order and is waiting for.</summary>
public sealed class Order
{
    public required string Id { get; set; }
    public required string CustomerId { get; set; }
    public required string ProductId { get; set; }
    public int PlacedDay { get; set; }
}

public enum FileKind { Useful, Fun, Dubious }
public enum FileStatus { New, Kept, Hidden, Binned }

/// <summary>Something a caller has uploaded to the Board's file area.</summary>
public sealed class BoardFile
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public FileKind Kind { get; set; }
    public FileStatus Status { get; set; }
    public int Day { get; set; }
}

/// <summary>Someone who works for you: a named person with a narrow job.</summary>
public sealed class StaffMember
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public List<Work.TicketKind> Does { get; set; } = new();
    public int JobsPerDay { get; set; } = 2;
    public int AccuracyPermille { get; set; } = 850;
    public long PieceRate { get; set; }
    public long WeeklyWage { get; set; }
    public int JobsToday { get; set; }
    public int JobsThisWeek { get; set; }
}

/// <summary>One subscriber's terminal on the switchboard.</summary>
public sealed class BoardPosition
{
    public required string CustomerId { get; set; }
    /// <summary>The port their number lives on.</summary>
    public int AssignedPort { get; set; }
    /// <summary>False while the name tag is too smudged to read.</summary>
    public bool TagRead { get; set; } = true;
    /// <summary>The port their jumper currently goes to, or -1 for none.</summary>
    public int Jumper { get; set; } = -1;
    public bool HotWired { get; set; }
    /// <summary>A jumper somebody left loose. Visible, and fixable before it becomes a fault.</summary>
    public bool Loose { get; set; }
    public bool EverLive { get; set; }
}

/// <summary>The switchboard on your wall: who is on which terminal, which ports are bad, how big it is.</summary>
public sealed class BoardState
{
    public int Stage { get; set; }
    public List<BoardPosition> Positions { get; set; } = new();
    public List<int> FaultyPorts { get; set; } = new();
    public List<int> TestedPorts { get; set; } = new();
}

public sealed class PromiseRecord
{
    public PromiseStatus Status { get; set; }
    public int DueDay { get; set; }
}

/// <summary>
/// The whole mutable world. Plain data only, so it round-trips through JSON (see Save/SaveCodec).
/// Derived values (bands, readable entries) are recomputed by the rules, never trusted from disk.
/// </summary>
public sealed class GameState
{
    public const int CurrentVersion = 7;

    public int Version { get; set; } = CurrentVersion;
    public ulong Seed { get; set; }
    public ulong RngState { get; set; }
    public int Day { get; set; }
    public int ActIndex { get; set; }

    // Money is whole period dollars.
    public long Cash { get; set; }
    public long RevenueThisMonth { get; set; }
    public long RevenueLastMonth { get; set; }
    public int Staff { get; set; }
    /// <summary>Enquiries waiting on the answering machine. Word of mouth grows this.</summary>
    public int MessagesWaiting { get; set; }
    /// <summary>Modems in stock. One goes into every new customer's house.</summary>
    public int Modems { get; set; } = 1;
    public Dictionary<string, Household> Households { get; set; } = new();
    public List<OddJob> Jobs { get; set; } = new();
    public int JobsDone { get; set; }

    // Almanac spine
    public long ArchiveMilli { get; set; }
    public int Echo { get; set; }
    public Dictionary<string, EntryStatus> Entries { get; set; } = new();
    /// <summary>Entries recalled before their public announcement, in order (newest last). Correction drifts the newest.</summary>
    public List<string> EarlyRecalls { get; set; } = new();
    public HashSet<string> FiredAnnouncements { get; set; } = new();
    public Dictionary<string, int> AnnouncementShift { get; set; } = new();

    // Earned automation: work kind id -> stage index
    public Dictionary<string, int> WorkStage { get; set; } = new();

    public List<Customer> Customers { get; set; } = new();
    public Dictionary<string, PromiseRecord> Ledger { get; set; } = new();
    public List<LatentDefect> Defects { get; set; } = new();
    /// <summary>Surfaced faults waiting for a repair visit (v2).</summary>
    public List<Fault> Faults { get; set; } = new();
    /// <summary>Customers connected by staff without you (v2).</summary>
    public int TechInstalls { get; set; }
    public int FaultsFixed { get; set; }
    /// <summary>Pole lines under construction, by premise id (v3).</summary>
    public Dictionary<string, LineProgress> Lines { get; set; } = new();

    // v4: the network carries something
    public List<Exchange> Exchanges { get; set; } = new() { new Exchange { Id = "ex-town", Name = "Main Street" } };
    /// <summary>Line cards bought but not yet fitted.</summary>
    public int LineCards { get; set; }
    public EveningReport? LastEvening { get; set; }
    public int EngagedThisMonth { get; set; }
    /// <summary>House installs under way, by customer id.</summary>
    public Dictionary<string, InstallProgress> Installs { get; set; } = new();
    /// <summary>Days ahead of the rest of the world each act was begun, by act id. Rivals start from the public date.</summary>
    public Dictionary<string, int> HeadStartDays { get; set; } = new();
    /// <summary>Catalogue orders waiting to be fitted.</summary>
    public List<Order> Orders { get; set; } = new();
    public int OrdersDone { get; set; }

    // v5: running the Board
    public List<BoardFile> BoardFiles { get; set; } = new();
    /// <summary>How much notice the file area has drawn from people with solicitors.</summary>
    public int BoardHeat { get; set; }
    public bool BoardWarned { get; set; }
    public int MailLastRunDay { get; set; } = -1;
    public int MailMissed { get; set; }
    public long MailBillCents { get; set; }

    // v6: the clock, people, and paper
    /// <summary>Minutes since six this morning (the sim's day runs 06:00 to 06:00).</summary>
    public int Minute { get; set; } = 150;
    public List<StaffMember> StaffMembers { get; set; } = new();
    /// <summary>Job tickets handed to staff: ticket key to staff id.</summary>
    public Dictionary<string, string> Assigned { get; set; } = new();
    /// <summary>1 Main Street is leased: staff collect their own tickets and are paid by the week.</summary>
    public bool HasDepot { get; set; }

    /// <summary>The switchboard (v7).</summary>
    public BoardState Switch { get; set; } = new();
    public List<GameEvent> Log { get; set; } = new();
    public int NextId { get; set; } = 1;

    public GameDate Date => new(Day);
    public int Archive => (int)(ArchiveMilli / 1000);
    public int LiveSubscribers => Customers.Count(c => c.Live && c.Installed);
    public int PendingInstalls => Customers.Count(c => c.Live && !c.Installed);
    public int ReadyForFrame => Customers.Count(c => c.Live && !c.Installed && c.SocketFitted);
    public int LedgerServed => Ledger.Values.Count(p => p.Status == PromiseStatus.Served);

    public string NewId(string prefix) => $"{prefix}-{NextId++}";

    public const int MaxLog = 500;

    public void Emit(EventKind kind, string text, string? refId = null)
    {
        Log.Add(new GameEvent(Day, kind, text, refId));
        if (Log.Count > MaxLog) Log.RemoveRange(0, Log.Count - MaxLog);
    }
}
