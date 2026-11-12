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
    /// <summary>False until someone physically wires them in at the frame. Nothing works just because it was sold.</summary>
    public bool Installed { get; set; } = true;
    /// <summary>Set when this customer came from the Promise Ledger — a named person, not a number.</summary>
    public string? PremiseId { get; set; }
    /// <summary>The building they live in, if any.</summary>
    public string? LotId { get; set; }
    /// <summary>The wall socket and modem are in at their house. Needed before the frame work means anything.</summary>
    public bool SocketFitted { get; set; } = true;
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
    public const int CurrentVersion = 3;

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
