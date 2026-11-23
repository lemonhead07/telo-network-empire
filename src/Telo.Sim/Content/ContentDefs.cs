namespace Telo.Sim.Content;

// Definitions are immutable records loaded from game/content/*.json.
// Instances (state) live in World/GameState and only reference definitions by stable string id.

public enum WorkMode { Manual, Delegated, Automated }

/// <summary>How the player *sees* a kind of work. The UI literally changes as this climbs.</summary>
public enum InterfaceTier { Paper, Terminal, Database, Dashboard }

public enum ConditionKind { Subscribers, Cash, Staff, MonthlyRevenue, LedgerServed, RecalledEntries, Customers, SocketsFitted, JobsDone, Knocked, ServiceNines, ProjectsDone, Built }

/// <summary>A threshold on something measured (<see cref="Kind"/> at least <see cref="Value"/>), or, for <see cref="ConditionKind.Built"/>, one named project built (<see cref="Id"/>).</summary>
public sealed record Condition(ConditionKind Kind, long Value)
{
    /// <summary>The project a <see cref="ConditionKind.Built"/> condition asks for (DC1: Act 5 ends when the board is lit).</summary>
    public string? Id { get; init; }
}

/// <summary>One part of an assembly, outermost first: what it is, what holds it, and whether it can be the fault.</summary>
public sealed record PartDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Fastener { get; init; } = "none";
    public int Count { get; init; } = 1;
    public bool CanFail { get; init; }
}

/// <summary>
/// A thing in the world as parts and fasteners (game/content/assemblies.json, spine step 6). A fault is one bad part; a repair
/// undoes what is in the way, finds it, replaces it and does it all up again. A new appliance is data, not code.
/// </summary>
public sealed record AssemblyDef
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";
    /// <summary>The odd job (by title) this assembly is the work of.</summary>
    public string Job { get; init; } = "";
    /// <summary>"repair" (find and replace a bad part) or "install" (build it).</summary>
    public string Mode { get; init; } = "repair";
    /// <summary>Kit beyond the toolbox, by name (ladder, cableDrum).</summary>
    public List<string> Needs { get; init; } = new();
    public List<PartDef> Parts { get; init; } = new();
}

/// <summary>
/// One chapter of the plot (game/content/plot.json). Chapters run in one line, never overlapping: the current chapter is the first
/// whose <see cref="DoneWhen"/> is not yet met (any one condition finishes it). Each teaches one thing; the last has no end.
/// </summary>
public sealed record ChapterDef
{
    public required string Id { get; init; }
    public string Title { get; init; } = "";
    /// <summary>Which first-hour step this chapter is (everything after the first hour is "free").</summary>
    public Town.OnboardingStep Step { get; init; } = Town.OnboardingStep.Free;
    /// <summary>The action (by id prefix) this chapter teaches. The bot and the phone use it.</summary>
    public string Teaches { get; init; } = "";
    /// <summary>The morning text that opens the chapter (on the phone).</summary>
    public string Text { get; init; } = "";
    public List<Condition> DoneWhen { get; init; } = new();
}

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

/// <summary>The person a work stage brings in, if it is a hire.</summary>
public sealed record HireDef
{
    public required string Name { get; init; }
    /// <summary>Kinds of ticket they can be handed. Empty for people whose work isn't on tickets (the co-sysop).</summary>
    public List<Work.TicketKind> Does { get; init; } = new();
    public int JobsPerDay { get; init; } = 2;
    /// <summary>Paid per job until there is a depot.</summary>
    public long PieceRate { get; init; }
    public long WeeklyWage { get; init; }
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
    /// <summary>Live, paying subscribers needed before this stage makes sense (you can't hire on nothing).</summary>
    public int RequiresSubscribers { get; init; }
    public string Description { get; init; } = "";
    /// <summary>Every improvement creates a new problem. Shown to the player before they commit.</summary>
    public string NewProblem { get; init; } = "";
    public HireDef? Hire { get; init; }
}

public sealed record WorkKindDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required List<WorkStageDef> Stages { get; init; }
}

/// <summary>A line the player builds by hand to reach a premise: poles along a planet road.</summary>
public sealed record PoleLineDef
{
    public required string Road { get; init; }
    public double SpacingM { get; init; } = 60;
    public long PoleCost { get; init; } = 15;
    /// <summary>Paid when the line is finished: the county's rural lines grant.</summary>
    public long Grant { get; init; }
}

public sealed record PremiseDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Reason { get; init; }
    public long CostToServe { get; init; }
    public int Act { get; init; }
    public string Story { get; init; } = "";
    /// <summary>Set when reaching them means building a pole line yourself rather than paying a fee.</summary>
    public PoleLineDef? PoleLine { get; init; }
}

public sealed record ProductStepDef
{
    public required string Verb { get; init; }
    public int Clicks { get; init; } = 1;
    public int Minutes { get; init; } = 4;
    public List<Town.Tools> Needs { get; init; } = new();
}

/// <summary>Something a subscriber can order besides the line: an extension, an answering machine, a faster modem.</summary>
public sealed record ProductDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>What the customer says when they ring to ask for it.</summary>
    public string Pitch { get; init; } = "";
    /// <summary>What they pay you, once.</summary>
    public long Price { get; init; }
    /// <summary>What Sol charges you for the hardware.</summary>
    public long Cost { get; init; }
    public long MonthlyExtra { get; init; }
    public int RequiresAct { get; init; }
    public string? RequiresEntry { get; init; }
    /// <summary>Which kinds of household want it. Empty means anyone.</summary>
    public List<string> Archetypes { get; init; } = new();
    public required List<ProductStepDef> Steps { get; init; }
    /// <summary>How it changes the length of their evening calls to the Board (1000 = no change; a fast modem halves it).</summary>
    public int CallLengthPermille { get; init; } = 1000;
    public string BodgeDefect { get; init; } = "something fitted in a hurry";
}

public enum LotKind { House, Shop, Farm, Depot, Yard }

/// <summary>How a project's parts reach its site (EA2b, D43): a crate on your van, the Yard's lorry, or no parts at all (a form, a handshake).</summary>
public enum PartsBy { Van, Lorry, None }

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
    /// <summary>Paying subscribers you need before this household has heard of you (0: from the start). How new streets and villages open up.</summary>
    public int OpensAt { get; init; }
    /// <summary>A project that has to be built before this household can be connected (the poles up their lane, the village cabinet).</summary>
    public string? NeedsProject { get; init; }
    /// <summary>
    /// A business or public building rather than a home (the pub, the post office, the school): somebody is always minding it,
    /// so nobody pops out from it, and neighbours pop in while it's open (Town/PeopleRules).
    /// </summary>
    public bool Business { get; init; }
}

/// <summary>One step of a project: an action held for a while, a few times over, with the kit it needs here.</summary>
public sealed record ProjectStepDef
{
    public required string Verb { get; init; }
    public int Clicks { get; init; } = 1;
    public int Minutes { get; init; } = 15;
    public List<string> Needs { get; init; } = new();
    /// <summary>Days to wait before this step can start (NezTel's engineer is booked; the concrete has to set).</summary>
    public int WaitDays { get; init; }
    /// <summary>What you're waiting for, in words.</summary>
    public string WaitText { get; init; } = "";
}

/// <summary>What finishing a project changes.</summary>
public sealed record ProjectRewardDef
{
    /// <summary>Extra lines in on the Board.</summary>
    public int LinesIn { get; init; }
    /// <summary>Extra switchboard panels it allows (eight subscribers each): a village comes in on its own panel.</summary>
    public int Panels { get; init; }
    /// <summary>Added to every live subscriber's monthly bill (a new service on their line).</summary>
    public long PerSubscriber { get; init; }
    public long MonthlyRevenue { get; init; }
    /// <summary>A monthly bill that comes with it (rent on a line, power).</summary>
    public long MonthlyCost { get; init; }
    /// <summary>Another project whose monthly cost this one ends (your own link replaces the leased one).</summary>
    public string? Cancels { get; init; }
    /// <summary>A whole town or region it opens: this many subscribers sign over the following weeks, counted and billed as a block.</summary>
    public int Region { get; init; }
    public string RegionName { get; init; } = "";
    /// <summary>What each of them pays a month.</summary>
    public long RegionFee { get; init; } = 15;
    /// <summary>What each of them costs a month to keep: support, power, leased capacity (EA1: towns pay a margin, not their whole fee).</summary>
    public long RegionCost { get; init; }
}

/// <summary>
/// A project (projects.json): something built in the world, step by step, that opens the next part of the game. Every
/// project is gated (act, earlier projects, an Almanac page, how big you are) so the game flows from one to the next.
/// </summary>
public sealed record ProjectDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int Act { get; init; }
    public required string Site { get; init; }
    /// <summary>The prop id of what stands there once it is built (props.json, e.g. "site.street-cabinet"). Presentation only.</summary>
    public string? Model { get; init; }
    public double East { get; init; }
    public double South { get; init; }
    /// <summary>What its parts cost at the Yard (or, for a project with no parts, what it costs to start).</summary>
    public long Cost { get; init; }
    /// <summary>How the parts get there (EA2b): on your van (the default), on the Yard's lorry, or none to buy.</summary>
    public PartsBy Parts { get; init; } = PartsBy.Van;
    public List<string> RequiresProjects { get; init; } = new();
    public string? RequiresEntry { get; init; }
    public bool RequiresDepot { get; init; }
    public List<Condition> Requires { get; init; } = new();
    public List<ProjectStepDef> Steps { get; init; } = new();
    public ProjectRewardDef Reward { get; init; } = new();
    /// <summary>The one-line why, on the phone when it becomes available.</summary>
    public string Story { get; init; } = "";
    public string DoneText { get; init; } = "";
}
