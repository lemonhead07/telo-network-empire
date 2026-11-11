namespace Telo.Sim.Content;

// Definitions are immutable records loaded from game/content/*.json.
// Instances (state) live in World/GameState and only reference definitions by stable string id.

public enum WorkMode { Manual, Delegated, Automated }

/// <summary>How the player *sees* a kind of work. The UI literally changes as this climbs.</summary>
public enum InterfaceTier { Paper, Terminal, Database, Dashboard }

public enum ConditionKind { Subscribers, Cash, Staff, MonthlyRevenue, LedgerServed, RecalledEntries }

public sealed record Condition(ConditionKind Kind, long Value);

public sealed record ActDef
{
    public required string Id { get; init; }
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required string Years { get; init; }
    public required string BusinessModel { get; init; }
    public required string BindingConstraint { get; init; }
    public required string SignatureMinigame { get; init; }
    /// <summary>The act cannot begin until this public announcement has fired. The queue is the gate, not the calendar.</summary>
    public string? GateAnnouncement { get; init; }
    /// <summary>Conditions that complete this act and let the next one begin.</summary>
    public List<Condition> ExitMilestone { get; init; } = new();
    public string ExitText { get; init; } = "";
}

public sealed record AnnouncementDef
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    /// <summary>Default public date, ISO yyyy-mm-dd. The player can push/pull it with calendar levers.</summary>
    public required string Date { get; init; }
    public string Headline { get; init; } = "";
}

public sealed record AlmanacEntryDef
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Blurb { get; init; } = "";
    /// <summary>The public announcement this knowledge corresponds to. Recalling before it fires is "early".</summary>
    public required string Announcement { get; init; }
    /// <summary>Entry stays sealed until the company has reached this act.</summary>
    public int Act { get; init; }
    public int ArchiveCost { get; init; }
    /// <summary>Echo gained per (started) year of earliness.</summary>
    public int EchoPerYearEarly { get; init; } = 5;
    public List<string> Requires { get; init; } = new();
}

public sealed record WorkStageDef
{
    public required WorkMode Mode { get; init; }
    public required InterfaceTier Interface { get; init; }
    /// <summary>Chance a task done in this stage is done right. Manual = the player's own skill (1000).</summary>
    public int AccuracyPermille { get; init; } = 1000;
    public long Cost { get; init; }
    public long MonthlyUpkeep { get; init; }
    public string? RequiresEntry { get; init; }
    public int RequiresAct { get; init; }
    public string Description { get; init; } = "";
    /// <summary>Every improvement creates a new problem. Shown to the player before they commit.</summary>
    public string NewProblem { get; init; } = "";
}

public sealed record WorkKindDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required List<WorkStageDef> Stages { get; init; }
}

public sealed record PremiseDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Reason { get; init; }
    public long CostToServe { get; init; }
    public int Act { get; init; }
    public string Story { get; init; } = "";
}

public enum LotKind { House, Shop, Farm }

/// <summary>A building someone lives or works in. Offsets are metres east/south of its site centre on the planet.</summary>
public sealed record LotDef
{
    public required string Id { get; init; }
    public LotKind Kind { get; init; }
    public required string Site { get; init; }
    public double East { get; init; }
    public double South { get; init; }
    public double Yaw { get; init; }
    public string Resident { get; init; } = "";
    public string Archetype { get; init; } = "family";
    public long Fee { get; init; } = 8;
    /// <summary>Set when this household is a Last Mile case: too far or too costly to sign up by knocking.</summary>
    public string? Premise { get; init; }
}
