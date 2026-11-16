using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.Net;
using Telo.Sim.Town;
using Telo.Sim.World;

namespace Telo.Sim.Build;

/// <summary>
/// Projects (game/content/projects.json): things built in the world, step by step with kit from the van, that open the next part
/// of the game: a lane, a village cabinet, the modem bank, a leased line, a whole town. Every project is gated (its act, earlier
/// projects, an Almanac page, the depot, how big you are) so the game flows from one to the next, in list order.
/// NOTE: rebuilt in the cloud session on 2026-11-16 from its call sites and ProjectTests, because the original file was never
/// committed. If the original turns up, prefer it.
/// </summary>
public static class ProjectRules
{
    public static bool Done(GameState s, string id) => s.ProjectsDone.Contains(id);
    public static bool Started(GameState s, string id) => s.Projects.ContainsKey(id);
    public static ProjectProgress ProgressOf(GameState s, string id) => s.Projects.TryGetValue(id, out var p) ? p : new ProjectProgress();

    /// <summary>The step to do next, or null when the project is built.</summary>
    public static ProjectStepDef? Step(GameState s, ProjectDef p)
    {
        if (Done(s, p.Id) || p.Steps.Count == 0) return null;
        int i = ProgressOf(s, p.Id).Step;
        return i < p.Steps.Count ? p.Steps[i] : null;
    }

    /// <summary>Why this project can't be started yet, in words; null when it can (or it's already under way).</summary>
    public static string? Locked(GameState s, ContentDb c, ProjectDef p)
    {
        if (Done(s, p.Id)) return "already built";
        if (Started(s, p.Id)) return null;
        if (s.ActIndex < p.Act) return $"not until Act {p.Act}";
        foreach (var r in p.RequiresProjects)
            if (!Done(s, r)) return $"needs {c.Project(r)?.Name ?? r} first";
        if (p.RequiresEntry is { } e && s.Entries.GetValueOrDefault(e) is not (EntryStatus.Recalled or EntryStatus.Drifting))
            return $"needs the Almanac page \"{(c.Almanac.TryGetValue(e, out var a) ? a.Title : e)}\"";
        if (p.RequiresDepot && !s.HasDepot) return "needs the depot at 1 Main Street";
        foreach (var cond in p.Requires)
            if (!ActRules.Met(s, cond)) return $"needs {Words(cond.Kind)} {cond.Value} (you have {ActRules.Measure(s, cond.Kind)})";
        return null;
    }

    public static bool Available(GameState s, ContentDb c, ProjectDef p) => !Done(s, p.Id) && Locked(s, c, p) is null;

    /// <summary>The next thing to build: the first project, in order, that is under way or open.</summary>
    public static ProjectDef? Next(GameState s, ContentDb c) =>
        c.Projects.FirstOrDefault(p => !Done(s, p.Id) && (Started(s, p.Id) || Available(s, c, p)));

    /// <summary>The next project in its act that waits only on something you can do yourself (recall a page, lease the depot).</summary>
    public static ProjectDef? Waiting(GameState s, ContentDb c) =>
        c.Projects.FirstOrDefault(p => !Done(s, p.Id) && !Started(s, p.Id) && s.ActIndex >= p.Act
            && p.RequiresProjects.All(r => Done(s, r)) && p.Requires.All(x => ActRules.Met(s, x))
            && ((p.RequiresEntry is { } e && s.Entries.GetValueOrDefault(e) is not (EntryStatus.Recalled or EntryStatus.Drifting)) || (p.RequiresDepot && !s.HasDepot)));

    /// <summary>The Almanac page to recall next for a waiting project: its own page if readable, else the first readable page it depends on.</summary>
    public static AlmanacEntryDef? PageToRecall(GameState s, ContentDb c, ProjectDef p) =>
        p.RequiresEntry is { } e ? Readable(s, c, e, 0) : null;

    private static AlmanacEntryDef? Readable(GameState s, ContentDb c, string id, int depth)
    {
        if (depth > 12 || !c.Almanac.TryGetValue(id, out var entry)) return null;
        var status = s.Entries.GetValueOrDefault(id);
        if (status is EntryStatus.Recalled or EntryStatus.Drifting) return null;
        if (status == EntryStatus.Readable) return entry;
        foreach (var r in entry.Requires)
            if (Readable(s, c, r, depth + 1) is { } page) return page;
        return null;
    }

    /// <summary>What stops the next action on this project right now, with this kit to hand. Null when it can be done.</summary>
    public static string? Blocked(GameState s, ContentDb c, ProjectDef p, Tools onSite)
    {
        if (Locked(s, c, p) is { } locked) return locked;
        if (Step(s, p) is not { } st) return "already built";
        if (!Started(s, p.Id) && s.Cash < p.Cost) return $"${p.Cost} to start; you have ${s.Cash}";
        var prog = ProgressOf(s, p.Id);
        if (prog.WaitUntil > s.Day) return $"{(st.WaitText.Length > 0 ? st.WaitText : "waiting")} until {new GameDate(prog.WaitUntil)}";
        var missing = Needs(st) & ~onSite;
        return missing == Tools.None ? null : $"bring the {Act.Actions.Names(missing)} from the van";
    }

    /// <summary>One action on the project: pays to start it, does one click of the current step, moves on, and finishes it.</summary>
    public static bool Do(GameState s, ContentDb c, ProjectDef p, Tools onSite)
    {
        if (Blocked(s, c, p, onSite) is not null) return false;
        if (!Started(s, p.Id))
        {
            s.Cash -= p.Cost;
            var fresh = new ProjectProgress();
            s.Projects[p.Id] = fresh;
            if (p.Steps[0].WaitDays > 0) { fresh.WaitUntil = s.Day + p.Steps[0].WaitDays; return true; }   // the form is in; now the wait
        }
        var prog = s.Projects[p.Id];
        var st = p.Steps[prog.Step];
        if (++prog.Click < st.Clicks) return true;
        prog.Step++; prog.Click = 0;
        if (prog.Step >= p.Steps.Count) { Finish(s, c, p); return true; }
        if (p.Steps[prog.Step].WaitDays > 0) prog.WaitUntil = s.Day + p.Steps[prog.Step].WaitDays;
        return true;
    }

    /// <summary>The project is built: its reward applies. (Paying for it is <see cref="Do"/>'s job.)</summary>
    public static void Finish(GameState s, ContentDb c, ProjectDef p)
    {
        if (!s.ProjectsDone.Add(p.Id)) return;
        s.Projects.Remove(p.Id);
        if (p.Reward.LinesIn > 0) BoardRules.Home(s).LinesIn += p.Reward.LinesIn;
        if (p.Reward.Region > 0) s.RegionSigned.TryAdd(p.Id, 0);
        s.Emit(EventKind.Milestone, p.DoneText.Length > 0 ? p.DoneText : $"{p.Name}: built.", p.Id);
    }

    /// <summary>Each day, the towns your projects opened sign up, over about three weeks.</summary>
    public static void Day(GameState s, ContentDb c)
    {
        foreach (var p in c.Projects.Where(p => p.Reward.Region > 0 && Done(s, p.Id)))
        {
            int signed = s.RegionSigned.GetValueOrDefault(p.Id);
            if (signed >= p.Reward.Region) continue;
            s.RegionSigned[p.Id] = Math.Min(p.Reward.Region, signed + Math.Max(1, (p.Reward.Region + 20) / 21));
            if (s.RegionSigned[p.Id] >= p.Reward.Region && p.Reward.RegionName.Length > 0)
                s.Emit(EventKind.Milestone, $"{p.Reward.RegionName}: all {p.Reward.Region} signed up.", p.Id);
        }
    }

    /// <summary>What built projects bring in each month: a service on every line, flat income, and the towns they opened.</summary>
    public static long MonthlyRevenue(GameState s, ContentDb c) =>
        c.Projects.Where(p => Done(s, p.Id)).Sum(p =>
            p.Reward.PerSubscriber * s.LiveSubscribers + p.Reward.MonthlyRevenue + s.RegionSigned.GetValueOrDefault(p.Id) * p.Reward.RegionFee);

    /// <summary>What built projects cost each month, except bills another built project has ended (your own link replaces the lease).</summary>
    public static long MonthlyCost(GameState s, ContentDb c)
    {
        var cancelled = c.Projects.Where(p => Done(s, p.Id) && p.Reward.Cancels is not null).Select(p => p.Reward.Cancels!).ToHashSet();
        return c.Projects.Where(p => Done(s, p.Id) && !cancelled.Contains(p.Id)).Sum(p => p.Reward.MonthlyCost)
             + c.Projects.Where(p => Done(s, p.Id)).Sum(p => s.RegionSigned.GetValueOrDefault(p.Id) * p.Reward.RegionCost);
    }

    private static Tools Needs(ProjectStepDef st) =>
        st.Needs.Aggregate(Tools.None, (t, n) => t | (Enum.TryParse<Tools>(n, true, out var x) ? x : Tools.None));

    private static string Words(ConditionKind k) => k switch
    {
        ConditionKind.Subscribers => "subscribers",
        ConditionKind.LedgerServed => "Last Mile homes connected",
        ConditionKind.ProjectsDone => "projects built",
        ConditionKind.Staff => "staff",
        ConditionKind.Cash => "cash",
        _ => k.ToString().ToLowerInvariant(),
    };
}
