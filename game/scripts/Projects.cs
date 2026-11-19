using Godot;
using Telo.Game.World;
using Telo.Sim.Build;
using Telo.Sim.Town;

namespace Telo.Game;

/// <summary>Projects in the world: each site's view, wired to the rules (Build/ProjectRules). The sim decides; this only shows and forwards.</summary>
public partial class GameRoot
{
    private readonly Dictionary<string, ProjectSiteView> _projects = new();

    private void BuildProjects()
    {
        foreach (var def in _content.Projects)
        {
            if (_planet.SiteById(def.Site) is not { } site) continue;
            var view = new ProjectSiteView(def) { Transform = TownBuilder.Place(_planet, site, (float)def.East, (float)def.South) };
            _town.Root.AddChild(view);
            _projects[def.Id] = view;
            var s = Sim.State;
            view.Spot.Prompt = () => ProjectRules.Step(s, def) is { } st
                ? ProjectRules.Blocked(s, _content, def, KitOnSite()) is { } why ? $"{def.Name}: {why}"
                : $"{st.Verb} ({ProjectRules.ProgressOf(s, def.Id).Click + 1} of {st.Clicks}){(ProjectRules.Started(s, def.Id) ? "" : $", ${def.Cost} to start")}"
                : $"{def.Name}: built";
            view.Spot.CanUse = () => ProjectRules.Blocked(s, _content, def, KitOnSite()) is null;
            // the step's time, while there is something you could do (missing kit or cash still shows the points, with the reason)
            view.Spot.HoldSeconds = () => ProjectRules.Step(s, def) is { } st && ProjectRules.Locked(s, _content, def) is null
                && ProjectRules.ProgressOf(s, def.Id).WaitUntil <= s.Day ? Hold(st.Minutes) : 0f;
            view.Spot.OnUse = () =>
            {
                if (!ProjectRules.Do(s, _content, def, KitOnSite())) return;
                _pip.Once("project");
                SyncProjects();
                RefreshIdleScreens();
            };
            // no bar: each click of a step is worked at points on the model (its wp-s<step>-… empties), EA5
            WorkPoints.Attach(view.Spot, view.Places);
        }
        SyncProjects();
    }

    private void SyncProjects()
    {
        var s = Sim.State;
        foreach (var (id, view) in _projects)
        {
            var def = view.Def;
            bool built = ProjectRules.Done(s, id);
            bool open = built || ProjectRules.Started(s, id) || ProjectRules.Available(s, _content, def);
            int clicks = def.Steps.Sum(x => x.Clicks);
            var prog = ProjectRules.ProgressOf(s, id);
            int done = built ? clicks : def.Steps.Take(prog.Step).Sum(x => x.Clicks) + prog.Click;
            bool waiting = !built && prog.WaitUntil > s.Day;
            string line = built ? $"{def.Name} (built)"
                : waiting ? $"{def.Name}\n{def.Steps.ElementAtOrDefault(prog.Step)?.WaitText ?? "waiting"}"
                : $"{def.Name}\n{ProjectRules.Step(s, def)?.Verb}";
            // the signboard: what it waits on and until when, else the step you are on
            string board = built ? $"{def.Name}\nBUILT"
                : waiting ? $"{def.Name}\n{ProjectRules.Blocked(s, _content, def, Tools.All)}"
                : ProjectRules.Started(s, id) ? $"{def.Name}\nStep {prog.Step + 1} of {def.Steps.Count}: {ProjectRules.Step(s, def)?.Verb}"
                : $"{def.Name}\n${def.Cost} to start";
            view.Sync(open, built ? def.Steps.Count : prog.Step, clicks == 0 ? 0 : (float)done / clicks, built, waiting, line, board);
        }
    }
}
