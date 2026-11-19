using Godot;
using Telo.Game.World;
using Telo.Sim.Build;
using Telo.Sim.World;

namespace Telo.Game;

/// <summary>
/// A quick look at every project site part-built (EA5), without the full --shot run:
///   xvfb-run -a godot --path game -- --shot-projects=/abs/path/p.png   → p-&lt;project id&gt;.png for each, then quits
/// Each project is put halfway through its steps (a wait, if that step has one, shows on the signboard) and the first
/// work point of the step under way is done, so the picture shows built stages, the ghost of the next, and the points.
/// Developer tool only: it pokes the state the way the god keys do, and is never saved.
/// </summary>
public partial class GameRoot
{
    private async Task TakeProjectShots(string path)
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _ignoreHours = true; _kitEverywhere = true;
        _dayClock = DayLengthSeconds * 0.2f;   // mid-morning light
        var cam = new Camera3D { Near = 0.05f, Far = 60000, Fov = 60 };
        AddChild(cam);
        cam.Current = true;
        var s = Sim.State;
        string? only = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--only="))?["--only=".Length..];
        await Frames(20);
        foreach (var def in _content.Projects)
        {
            if (only is not null && !def.Id.Contains(only)) continue;
            if (!_projects.TryGetValue(def.Id, out var view)) continue;
            int step = def.Steps.Count / 2;
            var st = def.Steps[step];
            s.Projects[def.Id] = new ProjectProgress { Step = step, WaitUntil = st.WaitDays > 0 ? s.Day + st.WaitDays : 0 };
            SyncProjects();
            await Physics(3);
            if (view.Spot.Pieces is { Next: { } first } && first.CanUse()) first.OnUse?.Invoke();
            await Physics(2);
            var spot = view.Spot.Position;
            var up = view.GlobalBasis.Y;
            cam.LookAtFromPosition(view.ToGlobal(spot + new Vector3(-0.9f, 0.7f, 2.0f)), view.ToGlobal(new Vector3(0, 1.0f, spot.Z - 1.6f)), up);
            await Frames(12);
            GetViewport().GetTexture().GetImage().SavePng(path.Replace(".png", $"-{def.Id}.png"));
            // close up on the point to work next, from where a player would stand (eye height, in front)
            if (view.Spot.Pieces is { Next: { } next })
            {
                var at = next.GlobalPosition;
                var eye = view.ToGlobal(view.ToLocal(at) with { Y = 1.6f } + new Vector3(-0.35f, 0, 1.1f));
                cam.LookAtFromPosition(eye, at, up);
                await Frames(8);
                GetViewport().GetTexture().GetImage().SavePng(path.Replace(".png", $"-{def.Id}-hands.png"));
            }
            GD.Print($"PROJECT SHOT {def.Id}: step {step + 1} of {def.Steps.Count}, {view.Spot.Pieces?.Points.Count ?? 0} points");
            s.Projects.Remove(def.Id);
            SyncProjects();
        }
        GD.Print("PROJECT SHOTS OK");
        GetTree().Quit();
    }
}
