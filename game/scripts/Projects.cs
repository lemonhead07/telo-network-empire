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
            // on Halden Flats the data centre itself shows the work (Halden.cs), so no grey frame there
            bool dc = def.Site == "halden";
            var view = new ProjectSiteView(def, frame: !dc) { Transform = TownBuilder.Place(_planet, site, (float)def.East, (float)def.South, dc ? DataCentre.Yaw : 0) };
            _town.Root.AddChild(view);
            _projects[def.Id] = view;
            var s = Sim.State;
            view.Spot.Prompt = () => ProjectRules.Step(s, def) is not null
                ? ProjectRules.Blocked(s, _content, def, KitOnSite()) is { } why ? $"{def.Name}: {why}" : ProjectRules.NextVerb(s, def)!
                : $"{def.Name}: built";
            view.Spot.CanUse = () => ProjectRules.Blocked(s, _content, def, KitOnSite()) is null;
            // the step's time while there is work here you could do: the points stay out (with the reason) when only kit is
            // missing; none while the project is locked, the parts aren't bought or delivered, or the step waits on someone
            view.Spot.HoldSeconds = () => ProjectRules.Step(s, def) is not { } st || ProjectRules.Locked(s, _content, def) is not null ? 0f
                : !ProjectRules.Started(s, def.Id) ? (YardRules.NotOnSite(s, _content, def, Telo.Sim.Town.Tools.Parts) is null ? Hold(YardRules.UnloadMinutes) : 0f)
                : ProjectRules.ProgressOf(s, def.Id).WaitUntil > s.Day ? 0f : Hold(st.Minutes);
            // unloading the parts starts the job (a crate set down, or the paperwork for a project with no parts); then each step sounds like what it says
            view.Spot.HoldSound = () => ProjectRules.Started(s, def.Id) && ProjectRules.Step(s, def) is { } st ? SoundCue.ForStep(st.Verb).Hold : "work.tick";
            view.Spot.DoneSound = () => !ProjectRules.Started(s, def.Id) ? (def.Parts == Telo.Sim.Content.PartsBy.None ? "paper.page" : "kit.crate")
                : ProjectRules.Step(s, def) is { } st ? SoundCue.ForStep(st.Verb).Done : "work.done";
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
            // no bar (HO1): each click of a step is worked at points on the thing (the model's wp-s<stage>-… empties)
            WorkPoints.Attach(view.Spot, view.Places);
        }
        BuildHalden();
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
            bool waiting = started && !built && prog.WaitUntil > s.Day;
            // the signboard: what it waits on and until when, else the step you are on
            string board = built ? $"{def.Name}\nBUILT"
                : waiting ? $"{def.Name}\n{ProjectRules.Blocked(s, _content, def, Telo.Sim.Town.Tools.All)}"
                : started ? $"{def.Name}\nStep {prog.Step + 1} of {def.Steps.Count}: {ProjectRules.Step(s, def)?.Verb}"
                : $"{def.Name}\n{YardRules.NotOnSite(s, _content, def, Telo.Sim.Town.Tools.Parts) ?? ProjectRules.StartVerb(def)}";
            // crates stand at the site once the parts are there: unloaded, or left by the Yard's lorry
            view.Sync(open, built ? def.Steps.Count : prog.Step, started, built, waiting, clicks == 0 ? 0 : (float)done / clicks, line, board,
                started || YardRules.LorryArrived(s, id));
        }
        SyncHalden();
    }
}
