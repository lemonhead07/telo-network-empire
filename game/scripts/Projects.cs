using Godot;
using Telo.Game.World;
using Telo.Sim.Build;

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
            view.Spot.Prompt = () => ProjectRules.Step(s, def) is not null
                ? ProjectRules.Blocked(s, _content, def, KitOnSite()) is { } why ? $"{def.Name}: {why}" : ProjectRules.NextVerb(s, def)!
                : $"{def.Name}: built";
            view.Spot.CanUse = () => ProjectRules.Blocked(s, _content, def, KitOnSite()) is null;
            view.Spot.HoldSeconds = () => ProjectRules.Step(s, def) is { } st && ProjectRules.Blocked(s, _content, def, KitOnSite()) is null
                ? Hold(ProjectRules.Started(s, def.Id) ? st.Minutes : YardRules.UnloadMinutes) : 0f;
            view.Spot.OnUse = () =>
            {
                bool crate = !ProjectRules.Started(s, def.Id) && def.Parts == Telo.Sim.Content.PartsBy.Van;
                if (!ProjectRules.Do(s, _content, def, KitOnSite())) return;
                // the crate is unloaded at the site: it leaves your hands (the next crate, if you bought another, is on the van)
                if (crate && _cargo.Items.FirstOrDefault(i => i.Tool == Telo.Sim.Town.Tools.Parts && i.Where != Player.Carryable.Place.Van) is { } used) _cargo.Stow(used);
                _cargo.Refresh();
                _pip.Once("project");
                SyncProjects();
                RefreshIdleScreens();
            };
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
            bool started = ProjectRules.Started(s, id);
            string line = built ? $"{def.Name} (built)"
                : !started ? $"{def.Name}\n{YardRules.NotOnSite(s, _content, def, Telo.Sim.Town.Tools.Parts) ?? ProjectRules.StartVerb(def)}"
                : prog.WaitUntil > s.Day ? $"{def.Name}\n{def.Steps.ElementAtOrDefault(prog.Step)?.WaitText ?? "waiting"}"
                : $"{def.Name}\n{ProjectRules.Step(s, def)?.Verb}";
            // crates stand at the site once the parts are there: unloaded, or left by the Yard's lorry
            view.Sync(open, clicks == 0 ? 0 : (float)done / clicks, built, line, started || YardRules.LorryArrived(s, id));
        }
    }
}
