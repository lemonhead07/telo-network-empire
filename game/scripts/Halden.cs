using Godot;
using Telo.Game.UI;
using Telo.Game.World;
using Telo.Sim.Act;
using Telo.Sim.Build;
using Telo.Sim.Content;

namespace Telo.Game;

/// <summary>
/// The data centre on Halden Flats (DC2) and the end of early access (DC9): the building grows with the Act 5 projects
/// (World/DataCentre), the ladder takes you to the roof, and once the board is lit there is a card on the night-shift desk.
/// The sim decides what is built; this only shows it.
/// </summary>
public partial class GameRoot
{
    private DataCentre? _dc;
    private CardView? _cardView;
    private WorldScreen? _cardScreen;
    private bool _dcWasLit;

    private void BuildHalden()
    {
        if (_planet.SiteById("halden") is not { } flats) return;
        _dc = new DataCentre { Transform = TownBuilder.Place(_planet, flats, 0, 0, DataCentre.Yaw) };
        _town.Root.AddChild(_dc);
        _dc.LadderUp.Prompt = () => "Climb the ladder to the roof";
        _dc.LadderUp.HoldSeconds = () => Hold(2);
        _dc.LadderUp.OnUse = () => OnRoof(true);
        _dc.LadderDown.Prompt = () => "Climb back down the ladder";
        _dc.LadderDown.HoldSeconds = () => Hold(2);
        _dc.LadderDown.OnUse = () => OnRoof(false);

        _cardView = new CardView();
        _cardScreen = WorldScreen.Create(_cardView, 0.42f, glows: false);
        _dc.CardAnchor.AddChild(_cardScreen);
        _cardView.Show(InterfaceTier.Paper);
        _cardView.Opened += () => LeanInto(_cardScreen);
        _cardView.Closed += LeanBack;
        _dc.Card.Prompt = () => "Read the card on the desk";
        _dc.Card.OnUse = () => _cardView.Open(InterfaceTier.Paper);   // reading it holds you still, as the other screens do (AnyScreenOpen)
        _dcWasLit = ProjectRules.Done(Sim.State, "proj-dc-lit");
        // the routes home glow from the roof at night, once the board is lit: to each homing project's far end
        _dc.BuildRoutes(_planet, _projects.Where(p => p.Key.StartsWith("proj-home-")).Select(p => p.Value.GlobalPosition));
    }

    private void SyncHalden()
    {
        if (_dc is null) return;
        _dc.Sync(Sim.State);
        bool lit = ProjectRules.Done(Sim.State, "proj-dc-lit");
        if (lit && !_dcWasLit) _pip?.Once("dclit");
        _dcWasLit = lit;
    }

    /// <summary>Up the ladder onto the roof, or back down to its foot.</summary>
    private void OnRoof(bool up)
    {
        if (_dc is null) return;
        var at = up ? new Vector3(14.6f, DataCentre.Roof + 0.1f, 6f) : new Vector3(17.4f, 0.2f, 6f);
        var face = up ? new Vector3(-1, 0, 0.3f) : new Vector3(1, 0, 0);
        _player.PlaceAt(_dc.ToGlobal(at), _dc.GlobalBasis * face);
        if (up) _pip.Once("roof");
    }

    /// <summary>Where the van stops for a project inside the data centre: its car park, not against the wall.</summary>
    private Vector3? HaldenPark(string place) =>
        _dc is not null && Places.IsProject(place, out var id) && _content.Project(id) is { Site: "halden" } ? _dc.ToGlobal(DataCentre.ParkLocal) : null;

    /// <summary>
    /// --shots=halden (with --from=act5): the site at each stage as the projects are built, inside each room, then the lit
    /// board at night, the view from the roof and the card. Building is by decree (as F4 would), for the pictures only.
    /// </summary>
    private async Task HaldenShots(string path, Func<string, Vector3, Vector3, Vector3?, Task> shot)
    {
        if (_dc is null) return;
        var s = Sim.State;
        Vector3 L(float x, float y, float z) => _dc.ToGlobal(new Vector3(x, y, z));
        async Task Outside(string name) { SyncProjects(); await shot($"-halden-{name}", L(-27f, 12f, 36f), L(0, 2f, 0), null); }
        void Build(string id) { if (_content.Project(id) is { } p && !ProjectRules.Done(s, id)) ProjectRules.GodBuild(s, _content, p); }
        void UnderWay(string id, int step) { s.Projects[id] = new Telo.Sim.World.ProjectProgress { Step = step }; }
        var town = _dc.ToLocal(_town.Centre);
        GD.Print($"HALDEN town is at local x={town.X:0} z={town.Z:0}");

        await Outside("0-plot");
        UnderWay("proj-dc-slab", 1); await Outside("1-pegs");
        UnderWay("proj-dc-slab", 2); await Outside("2-pour");
        Build("proj-dc-slab"); await Outside("3-slab");
        UnderWay("proj-dc-shell", 0); await Outside("4-frame");
        UnderWay("proj-dc-shell", 2); await Outside("5-frame");
        Build("proj-dc-shell"); await Outside("6-hall");
        await shot("-halden-6-planning", L(-1f, 1.7f, 10.5f), L(-12f, 1.2f, 5f), null);
        Build("proj-dc-power"); SyncProjects();
        await shot("-halden-7-plant", L(8.2f, 1.7f, 2.8f), L(13f, 1.0f, -8f), null);
        Build("proj-dc-cooling"); Build("proj-dc-meetme"); SyncProjects();
        await shot("-halden-8-meetme", L(-8.2f, 1.7f, 2.8f), L(-14f, 1.0f, -7f), null);
        Build("proj-dc-core"); Build("proj-peering"); SyncProjects();
        await shot("-halden-9-hall", L(5.8f, 1.7f, 3.0f), L(-1f, 1.0f, -7f), null);
        foreach (var id in new[] { "proj-home-valley", "proj-home-eastgate", "proj-migration", "proj-home-millbrook", "proj-home-farland" }) Build(id);
        SyncProjects();
        await shot("-halden-10-racks", L(-4.5f, 1.7f, -4f), L(3f, 1.0f, -3.5f), null);
        Build("proj-dc-lit"); SyncProjects();
        await shot("-halden-11-lit-hall", L(5.8f, 1.7f, 3.0f), L(-1f, 1.0f, -7f), null);
        await shot("-halden-12-card", L(-11f, 1.6f, 8.6f), L(-11f, 0.85f, 7.0f), null);
        // reading it, as the player does: walk up, E, lean in
        if (GetViewport().GetCamera3D() is { } shotCam && _cardView is not null)
        {
            shotCam.Current = false; _player.Camera.Current = true;
            _player.PlaceAt(L(-11f, 0.1f, 8.6f), _dc.GlobalBasis * new Vector3(0, 0, -1));
            _player.Camera.Rotation = new Vector3(-0.35f, 0, 0);
            await Frames(6);
            _dc.Card.OnUse!();
            await ToSignal(GetTree().CreateTimer(0.9), SceneTreeTimer.SignalName.Timeout);
            GetViewport().GetTexture().GetImage().SavePng(path.Replace(".png", "-halden-12-card-read.png"));
            _cardView.Close();
            await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
            _player.Camera.Current = false; shotCam.Current = true;
            _player.PlaceAt(_dc.ToGlobal(DataCentre.ParkLocal + new Vector3(3f, 0.7f, 2f)), _dc.GlobalBasis * Vector3.Back);   // out of the pictures
        }
        // night: the building lit from outside, then the view from its roof
        float was = _dayClock; bool hours = _ignoreHours;
        _ignoreHours = false;
        _dayClock = DayLengthSeconds * (22.5f - 6f) * 60f / Telo.Sim.Town.SleepRules.Bedtime; _slowTick = 0;
        await Frames(6);
        await Outside("13-night");
        var roof = _dc.ToGlobal(new Vector3(13.5f, DataCentre.Roof + 1.7f, 9.5f));
        await shot("-halden-14-roof", roof, roof + (_town.Centre - _dc.GlobalPosition).Normalized() * 60f - _dc.GlobalBasis.Y * 6f, null);
        if (_projects.TryGetValue("proj-home-eastgate", out var eg))
            await shot("-halden-15-roof-eastgate", roof, roof + (eg.GlobalPosition - roof).Normalized() * 60f - _dc.GlobalBasis.Y * 5f, null);
        // and the ladder works: up it, a second on the roof, still standing on the deck (not fallen through)
        OnRoof(true);
        for (int i = 0; i < 60; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        float y = _dc.ToLocal(_player.GlobalPosition).Y;
        GD.Print($"HALDEN ROOF standing={Mathf.Abs(y - DataCentre.Roof) < 0.4f} y={y:0.00}");
        OnRoof(false);
        _dayClock = was; _ignoreHours = hours;
    }
}
