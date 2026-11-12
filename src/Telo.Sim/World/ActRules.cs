using Telo.Sim.Content;
using Telo.Sim.Almanac;
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

    /// <summary>
    /// An act's gate opens when the world announces its technology, or sooner if the player has recalled that technology
    /// from the Almanac. Recalling early costs Archive and Echo, and buys a head start over everyone else.
    /// </summary>
    public static bool GateOpen(GameState s, ContentDb c, string announcementId) =>
        s.FiredAnnouncements.Contains(announcementId) || GateEntry(s, c, announcementId) is { } e && s.Entries.GetValueOrDefault(e.Id) is EntryStatus.Recalled or EntryStatus.Drifting;

    /// <summary>The Almanac page that would open this gate early, if there is one.</summary>
    public static AlmanacEntryDef? GateEntry(GameState s, ContentDb c, string announcementId) =>
        c.Almanac.Values.Where(e => e.Announcement == announcementId).OrderBy(e => e.ArchiveCost).FirstOrDefault();

    /// <summary>The page to recall to begin the next act now: only when this act's work is done and the world hasn't caught up.</summary>
    public static AlmanacEntryDef? EarlyStartEntry(GameState s, ContentDb c)
    {
        if (s.ActIndex + 1 >= c.Acts.Count || !c.Acts[s.ActIndex].ExitMilestone.All(m => Met(s, m))) return null;
        if (c.Acts[s.ActIndex + 1].GateAnnouncement is not { } g || GateOpen(s, c, g)) return null;
        return GateEntry(s, c, g) is { } e && s.Entries.GetValueOrDefault(e.Id) == EntryStatus.Readable ? e : null;
    }

    private static string Ahead(int days) => days >= 60 ? $"{days / 30} months" : $"{days} days";

    public static bool TryAdvance(GameState s, ContentDb c)
    {
        if (s.ActIndex + 1 >= c.Acts.Count) return false;
        var cur = c.Acts[s.ActIndex];
        var next = c.Acts[s.ActIndex + 1];
        if (!cur.ExitMilestone.All(m => Met(s, m))) return false;
        if (next.GateAnnouncement is { } g && !GateOpen(s, c, g)) return false;
        s.ActIndex++;
        if (next.GateAnnouncement is { } early && !s.FiredAnnouncements.Contains(early))
        {
            int days = AlmanacRules.PublicDate(s, c, early).Day - s.Day;
            if (days > 0)
            {
                s.HeadStartDays[next.Id] = days;
                s.Emit(EventKind.Milestone, $"You are {Ahead(days)} ahead of the rest of the world. Anyone who comes to compete with you starts from the public date.", next.Id);
            }
        }
        if (cur.ExitText.Length > 0) s.Emit(EventKind.Milestone, cur.ExitText, cur.Id);
        s.Emit(EventKind.ActChanged, $"Act {next.Index}: {next.Name}. {next.BusinessModel}. What holds you back now: {next.BindingConstraint}.", next.Id);
        return true;
    }
}
