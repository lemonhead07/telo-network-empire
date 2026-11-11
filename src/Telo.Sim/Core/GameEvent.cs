namespace Telo.Sim.Core;

public enum EventKind { Info, Announcement, Milestone, ActChanged, Echo, Defect, Money, Ledger, Work }

/// <summary>Something the presentation layer may show (fax, pager, PIP line, newspaper). Milestones and errors only.</summary>
public sealed record GameEvent(int Day, EventKind Kind, string Text, string? RefId = null);
