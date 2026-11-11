using Telo.Sim.Content;
using Telo.Sim.Core;

namespace Telo.Sim.World;

/// <summary>Acts change the business model, the binding constraint and the signature verb. They advance on milestones + the announcement queue.</summary>
public static class ActRules
{
    public static long Measure(GameState s, ConditionKind kind) => kind switch
    {
        ConditionKind.Subscribers => s.LiveSubscribers,
        ConditionKind.Cash => s.Cash,
        ConditionKind.Staff => s.Staff,
        ConditionKind.MonthlyRevenue => s.RevenueLastMonth,
        ConditionKind.LedgerServed => s.LedgerServed,
        ConditionKind.RecalledEntries => s.Entries.Values.Count(e => e is EntryStatus.Recalled or EntryStatus.Drifting),
        _ => 0,
    };

    public static bool Met(GameState s, Condition cond) => Measure(s, cond.Kind) >= cond.Value;

    public static bool TryAdvance(GameState s, ContentDb c)
    {
        if (s.ActIndex + 1 >= c.Acts.Count) return false;
        var cur = c.Acts[s.ActIndex];
        var next = c.Acts[s.ActIndex + 1];
        if (!cur.ExitMilestone.All(m => Met(s, m))) return false;
        if (next.GateAnnouncement is { } g && !s.FiredAnnouncements.Contains(g)) return false;
        s.ActIndex++;
        if (cur.ExitText.Length > 0) s.Emit(EventKind.Milestone, cur.ExitText, cur.Id);
        s.Emit(EventKind.ActChanged, $"Act {next.Index}: {next.Name}. {next.BusinessModel}. What holds you back now: {next.BindingConstraint}.", next.Id);
        return true;
    }
}
