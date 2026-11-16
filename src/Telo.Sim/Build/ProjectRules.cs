using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.Net;
using Telo.Sim.Town;
using Telo.Sim.World;

namespace Telo.Sim.Build;

/// <summary>
/// Projects (projects.json): things built in the world, step by step with kit from the van, that open the next part of the
/// game. Each is gated (its act, earlier projects, an Almanac page, the depot, how big you are). Starting one pays its cost;
/// a step can make you wait (an engineer, concrete, a ship); finishing one applies its reward. Regions it opens sign over
/// about three weeks and are billed as a block.
/// </summary>
public static class ProjectRules
{
    /// <summary>How many days a region takes to sign up in full.</summary>
    public const int RegionSignDays = 21;

    public static bool Done(GameState s, string id) => s.ProjectsDone.Contains(id);
    public static bool Started(GameState s, string id) => s.Projects.ContainsKey(id);

    public static ProjectProgress ProgressOf(GameState s, string id) =>
        s.Projects.TryGetValue(id, out var p) ? p : new ProjectProgress();

    /// <summary>The step to do next, or null when built.</summary>
    public static ProjectStepDef? Step(GameState s, ProjectDef p) =>
        Done(s, p.Id) ? null : p.Steps.ElementAtOrDefault(ProgressOf(s, p.Id).Step);

    /// <summary>Why this project isn't open to you yet (null: it is, or it's under way or built).</summary>
    public static string? Locked(GameState s, ContentDb c, ProjectDef p)
    {
        if (Done(s, p.Id) || Started(s, p.Id)) return null;
        if (s.ActIndex < p.Act) return $"not until {(p.Act < c.Acts.Count ? c.Acts[p.Act].Name : $"act {p.Act}")}";
        if (p.RequiresProjects.FirstOrDefault(r => !Done(s, r)) is { } need) return $"first build {c.Project(need)?.Name ?? need}";
        if (p.RequiresEntry is { } e && s.Entries.GetValueOrDefault(e) is not (EntryStatus.Recalled or EntryStatus.Drifting))
            return $"needs the Almanac page \"{(c.Almanac.TryGetValue(e, out var pg) ? pg.Title : e)}\"";
        if (p.RequiresDepot && !s.HasDepot) return "needs the depot";
        if (p.Requires.FirstOrDefault(r => !ActRules.Met(s, r)) is { } cond)
            return $"needs {cond.Value} {cond.Kind.ToString().ToLowerInvariant()} (you have {ActRules.Measure(s, cond.Kind)})";
        return null;
    }

    /// <summary>Open to you: under way, or unlocked and not yet built.</summary>
    public static bool Available(GameState s, ContentDb c, ProjectDef p) => !Done(s, p.Id) && Locked(s, c, p) is null;

    /// <summary>The project to work on now: one under way first, else the first open one, in the file's order.</summary>
    public static ProjectDef? Next(GameState s, ContentDb c) =>
        c.Projects.FirstOrDefault(p => Started(s, p.Id) && !Done(s, p.Id)) ?? c.Projects.FirstOrDefault(p => Available(s, c, p));

    /// <summary>The first unbuilt project in this act or earlier that is held up only by something you can do: a page to recall, the depot to lease.</summary>
    public static ProjectDef? Waiting(GameState s, ContentDb c) =>
        c.Projects.FirstOrDefault(p => !Done(s, p.Id) && !Started(s, p.Id) && p.Act <= s.ActIndex && p.RequiresProjects.All(r => Done(s, r))
            && ((p.RequiresEntry is { } e && s.Entries.GetValueOrDefault(e) is not (EntryStatus.Recalled or EntryStatus.Drifting)) || (p.RequiresDepot && !s.HasDepot)));

    /// <summary>The Almanac page to recall next for this project: its own page if it can be read now, else the first readable page it builds on.</summary>
    public static AlmanacEntryDef? PageToRecall(GameState s, ContentDb c, ProjectDef p) => p.RequiresEntry is { } e ? Readable(s, c, e, 0) : null;

    private static AlmanacEntryDef? Readable(GameState s, ContentDb c, string id, int depth)
    {
        if (depth > 8 || !c.Almanac.TryGetValue(id, out var pg)) return null;
        var st = s.Entries.GetValueOrDefault(id);
        if (st is EntryStatus.Recalled or EntryStatus.Drifting) return null;
        if (st == EntryStatus.Readable) return pg;
        return pg.Requires.Select(r => Readable(s, c, r, depth + 1)).FirstOrDefault(x => x is not null);
    }

    public static Tools NeedsOf(ProjectStepDef st) => st.Needs.Aggregate(Tools.None, (t, n) => t | Enum.Parse<Tools>(n, true));

    /// <summary>Why the next step can't be done here and now (null: it can).</summary>
    public static string? Blocked(GameState s, ContentDb c, ProjectDef p, Tools onSite)
    {
        if (Done(s, p.Id)) return "built";
        if (Locked(s, c, p) is { } why) return why;
        if (Step(s, p) is not { } st) return "built";
        var prog = ProgressOf(s, p.Id);
        if (prog.WaitUntil > s.Day) return $"{(st.WaitText.Length > 0 ? st.WaitText : "waiting")} until {new GameDate(prog.WaitUntil)}";
        if (!Started(s, p.Id) && s.Cash < p.Cost) return $"it costs ${p.Cost} to start";
        var missing = NeedsOf(st) & ~onSite;
        return missing == Tools.None ? null : $"you need the {Act.Actions.Names(missing)} here";
    }

    /// <summary>One action on the current step. The first one pays for the project; the last one finishes it.</summary>
    public static bool Do(GameState s, ContentDb c, ProjectDef p, Tools onSite)
    {
        if (Blocked(s, c, p, onSite) is not null) return false;
        if (!Started(s, p.Id))
        {
            s.Cash -= p.Cost;
            s.Projects[p.Id] = new ProjectProgress();
            s.Emit(EventKind.Milestone, $"Project started: {p.Name} (${p.Cost}).", p.Id);
        }
        var prog = s.Projects[p.Id];
        var st = p.Steps[prog.Step];
        if (++prog.Click < st.Clicks) return true;
        prog.Click = 0;
        prog.Step++;
        if (prog.Step >= p.Steps.Count) { Finish(s, c, p); return true; }
        if (p.Steps[prog.Step].WaitDays > 0) prog.WaitUntil = s.Day + p.Steps[prog.Step].WaitDays;
        return true;
    }

    /// <summary>Built: the reward applies. (Also used by dev shortcuts to build by decree; the cost is theirs to take.)</summary>
    public static void Finish(GameState s, ContentDb c, ProjectDef p)
    {
        if (!s.ProjectsDone.Add(p.Id)) return;
        s.Projects.Remove(p.Id);
        if (p.Reward.LinesIn > 0) BoardRules.Home(s).LinesIn += p.Reward.LinesIn;
        if (p.Reward.Region > 0 && !s.RegionSigned.ContainsKey(p.Id)) s.RegionSigned[p.Id] = 0;
        s.Emit(EventKind.Milestone, p.DoneText.Length > 0 ? p.DoneText : $"{p.Name}: built.", p.Id);
    }

    /// <summary>Every morning: the towns your projects opened sign up, a share a day, until all of them are on.</summary>
    public static void Day(GameState s, ContentDb c)
    {
        foreach (var p in c.Projects)
        {
            if (p.Reward.Region <= 0 || !s.RegionSigned.TryGetValue(p.Id, out int signed) || signed >= p.Reward.Region) continue;
            int add = Math.Max(1, (p.Reward.Region + RegionSignDays - 1) / RegionSignDays);
            s.RegionSigned[p.Id] = Math.Min(p.Reward.Region, signed + add);
            if (s.RegionSigned[p.Id] == p.Reward.Region)
                s.Emit(EventKind.Milestone, $"All of {p.Reward.RegionName} is on your network: {p.Reward.Region} subscribers.", p.Id);
        }
    }

    private static IEnumerable<ProjectDef> Built(GameState s, ContentDb c) => c.Projects.Where(p => Done(s, p.Id));

    /// <summary>What the projects bring in each month: services on every local line, income of their own, and the towns' blocks.</summary>
    public static long MonthlyRevenue(GameState s, ContentDb c) =>
        Built(s, c).Sum(p => p.Reward.PerSubscriber * s.LiveSubscribers + p.Reward.MonthlyRevenue
                             + (long)s.RegionSigned.GetValueOrDefault(p.Id) * p.Reward.RegionFee);

    /// <summary>What the projects cost each month, except bills a later project has ended.</summary>
    public static long MonthlyCost(GameState s, ContentDb c)
    {
        var cancelled = Built(s, c).Select(p => p.Reward.Cancels).Where(x => x is not null).ToHashSet();
        return Built(s, c).Where(p => !cancelled.Contains(p.Id)).Sum(p => p.Reward.MonthlyCost);
    }
}
