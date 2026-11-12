using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.World;

namespace Telo.Sim.Work;

public enum UpgradeResult { Ok, AtFinalStage, NeedsAct, NeedsSubscribers, NeedsEntry, NotEnoughCash, UnknownWork }

/// <summary>
/// Earned automation — the engine of "the gameplay slowly changes as you play".
/// Every kind of work climbs Manual → Delegated → Automated, and its interface climbs Paper → Dashboard.
/// The player stops doing a verb with their hands only after paying for the people/software that replace them.
/// </summary>
public static class WorkRules
{
    public static WorkStageDef Current(GameState s, ContentDb c, string workId) =>
        c.Work[workId].Stages[Math.Min(s.WorkStage.GetValueOrDefault(workId), c.Work[workId].Stages.Count - 1)];

    public static WorkStageDef? Next(GameState s, ContentDb c, string workId)
    {
        var stages = c.Work[workId].Stages;
        int i = s.WorkStage.GetValueOrDefault(workId) + 1;
        return i < stages.Count ? stages[i] : null;
    }

    public static UpgradeResult CanUpgrade(GameState s, ContentDb c, string workId)
    {
        if (!c.Work.ContainsKey(workId)) return UpgradeResult.UnknownWork;
        var next = Next(s, c, workId);
        if (next is null) return UpgradeResult.AtFinalStage;
        if (s.ActIndex < next.RequiresAct) return UpgradeResult.NeedsAct;
        if (s.LiveSubscribers < next.RequiresSubscribers) return UpgradeResult.NeedsSubscribers;
        if (next.RequiresEntry is { } e && s.Entries.GetValueOrDefault(e) is not (EntryStatus.Recalled or EntryStatus.Drifting))
            return UpgradeResult.NeedsEntry;
        if (s.Cash < next.Cost) return UpgradeResult.NotEnoughCash;
        return UpgradeResult.Ok;
    }

    public static UpgradeResult Upgrade(GameState s, ContentDb c, string workId)
    {
        var r = CanUpgrade(s, c, workId);
        if (r != UpgradeResult.Ok) return r;
        var next = Next(s, c, workId)!;
        s.Cash -= next.Cost;
        s.WorkStage[workId] = s.WorkStage.GetValueOrDefault(workId) + 1;
        if (next.Mode == WorkMode.Delegated && s.WorkStage[workId] == 1) s.Staff++;
        s.Emit(EventKind.Work, $"{c.Work[workId].Name}: now {next.Mode.ToString().ToLowerInvariant()} on {next.Interface.ToString().ToLowerInvariant()}. New problem: {next.NewProblem}", workId);
        return UpgradeResult.Ok;
    }

    /// <summary>The interface tier the presentation layer should draw for a work kind.</summary>
    public static InterfaceTier InterfaceFor(GameState s, ContentDb c, string workId) => Current(s, c, workId).Interface;

    public static long MonthlyUpkeep(GameState s, ContentDb c) =>
        c.Work.Keys.Sum(id => Current(s, c, id).MonthlyUpkeep);

    /// <summary>
    /// Resolve a task done by someone other than the player. Wrong work is not shown now — it becomes a latent
    /// defect that surfaces weeks later. Returns true if it was done right.
    /// </summary>
    public static bool ResolveDelegated(GameState s, ContentDb c, string workId, DetRng rng, string? customerId, string what)
    {
        var stage = Current(s, c, workId);
        if (rng.ChancePermille(stage.AccuracyPermille)) return true;
        s.Defects.Add(new LatentDefect
        {
            Id = s.NewId("defect"),
            SurfaceDay = s.Day + rng.Range(7, 41),
            Source = workId,
            CustomerId = customerId,
            Text = what,
        });
        return false;
    }
}
