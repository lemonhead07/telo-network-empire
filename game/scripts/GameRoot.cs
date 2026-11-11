using Godot;
using Telo.Game.Player;
using Telo.Game.UI;
using Telo.Game.World;
using Telo.Sim.Content;
using Telo.Sim.Minigames.Frame;
using Telo.Sim.Planet;
using Telo.Sim.Save;
using Telo.Sim.Work;
using Telo.Sim.World;

namespace Telo.Game;

/// <summary>Composition root: loads content, owns the Simulation, builds the room, wires views. No game rules here.</summary>
public partial class GameRoot : Node3D
{
    /// <summary>Real seconds per in-game day (a full 24 h sun cycle). The bed skips to morning.</summary>
    public const float DayLengthSeconds = 480f;
    /// <summary>One hand-tuned world for now, so the planet is the same every time and content can be placed on it.</summary>
    public const ulong DefaultSeed = 1983;
    private static string? _pendingLoad;
    public const string SavePath = "user://save.json";

    public Simulation Sim { get; private set; } = null!;
    private FirstPersonController _player = null!;
    private Hud _hud = null!;
    private FrameView _frame = null!;
    private AlmanacView _almanac = null!;
    private NotebookView _notebook = null!;
    private float _dayClock = DayLengthSeconds * 0.1f;
    private ContentDb _content = null!;
    private PlanetGen _planet = null!;
    private PlanetSky _sky = null!;
    private Town _town = null!;
    private Van _van = null!;

    private float Hour => (6f + _dayClock / DayLengthSeconds * 24f) % 24f;

    private bool AnyModalOpen => _frame.Visible || _almanac.Visible || _notebook.Visible;

    public override void _Ready()
    {
        InputSetup.Register();
        _content = LoadContent();
        var args = OS.GetCmdlineUserArgs();
        ulong seed = args.FirstOrDefault(a => a.StartsWith("--seed=")) is { } sa ? ulong.Parse(sa["--seed=".Length..]) : DefaultSeed;
        Sim = _pendingLoad is { } json ? new Simulation(_content, SaveCodec.FromJson(json)) : Simulation.NewGame(_content, seed);
        _pendingLoad = null;

        // The world: a small planet, built from the sim's pure terrain function.
        _planet = new PlanetGen(Sim.State.Seed);
        float radius = (float)_planet.RadiusM;
        PlanetBuilder.Build(this, _planet);
        _sky = new PlanetSky(); AddChild(_sky);
        _sky.Setup(radius, PlanetBuilder.ToGodot(_planet.Town.Dir));
        _town = TownBuilder.Build(this, _planet);
        BuildProps(_town.Hq);

        _player = new FirstPersonController { SeaRadius = radius, IsFrozen = () => AnyModalOpen };
        AddChild(_player);
        _player.GlobalTransform = _town.PlayerSpawn;

        _van = new Van { SeaRadius = radius };
        AddChild(_van);
        _van.GlobalTransform = _town.VanSpawn;
        _van.Door.Prompt = () => "Drive the van";
        _van.Door.OnUse = EnterVan;

        var ui = new CanvasLayer();
        AddChild(ui);
        _hud = new Hud(); ui.AddChild(_hud);
        _frame = new FrameView { Sim = Sim }; ui.AddChild(_frame);
        _almanac = new AlmanacView { Sim = Sim }; ui.AddChild(_almanac);
        _notebook = new NotebookView { Sim = Sim }; ui.AddChild(_notebook);

        if (args.Contains("--smoke")) _ = RunSmoke();
        if (args.FirstOrDefault(a => a.StartsWith("--shot=")) is { } shot) _ = TakeScreenshot(shot["--shot=".Length..]);
    }

    private static ContentDb LoadContent() => ContentDb.FromJson(ContentDb.FileNames.ToDictionary(
        f => f, f => Godot.FileAccess.GetFileAsString($"res://content/{f}") is { Length: > 0 } t ? t
            : throw new InvalidOperationException($"Missing res://content/{f}")));

    private void BuildProps(Node3D room)
    {
        float back = -RoomBuilder.D / 2 + 0.1f;

        // The frame: wooden backboard with rows of terminal blocks.
        RoomBuilder.Prop(room, "Frame", new Vector3(-1.4f, 1.35f, back), new Vector3(1.5f, 1.2f, 0.08f), RoomBuilder.Board, FramePrompt, UseFrame);
        for (int row = 0; row < 6; row++)
            RoomBuilder.Box(room, new Vector3(-1.4f, 0.9f + row * 0.18f, back + 0.07f), new Vector3(1.2f, 0.07f, 0.05f), RoomBuilder.Beige);
        RoomBuilder.Label(room, "FRAME", new Vector3(-1.4f, 2.05f, back + 0.06f), 0.12f);

        // Desk with answering machine, PIP's handset, and the notebook.
        RoomBuilder.Box(room, new Vector3(1.3f, 0.37f, back + 0.45f), new Vector3(1.7f, 0.74f, 0.8f), RoomBuilder.Wood, solid: true);
        RoomBuilder.Prop(room, "AnsweringMachine", new Vector3(0.75f, 0.8f, back + 0.4f), new Vector3(0.28f, 0.1f, 0.2f), new Color(0.08f, 0.08f, 0.08f),
            () => Sim.State.MessagesWaiting > 0 ? $"Play messages ({Sim.State.MessagesWaiting} waiting)" : "No new messages", PlayMessages);
        RoomBuilder.Prop(room, "Handset", new Vector3(1.35f, 0.77f, back + 0.45f), new Vector3(0.08f, 0.04f, 0.16f), new Color(0.15f, 0.16f, 0.2f),
            () => "Open PIP's Almanac", OpenAlmanac);
        RoomBuilder.Prop(room, "Notebook", new Vector3(1.8f, 0.76f, back + 0.5f), new Vector3(0.22f, 0.03f, 0.3f), new Color(0.5f, 0.12f, 0.1f),
            () => "Open the company notebook", OpenNotebook);

        // Camp bed. Sleeping is how days pass quickly.
        RoomBuilder.Prop(room, "Bed", new Vector3(-2.2f, 0.22f, 1.4f), new Vector3(0.9f, 0.44f, 1.9f), RoomBuilder.Fabric,
            () => "Sleep until morning", () => { Sim.AdvanceDay(); _dayClock = DayLengthSeconds * 0.05f; });
    }

    // ---- interactions ------------------------------------------------------------------------
    private string FramePrompt()
    {
        if (Sim.State.PendingInstalls == 0) return "The frame (no installs waiting)";
        if (WorkRules.Current(Sim.State, Sim.Content, "install").Mode != WorkMode.Manual) return "Hand today's job sheet to your technician";
        return Sim.FrameWorkOrder().Count < 3 ? "Need 3 lines on the work order to make the trip worthwhile" : $"Work the frame ({Sim.State.PendingInstalls} to install)";
    }

    private void UseFrame()
    {
        if (Sim.State.PendingInstalls == 0) return;
        var order = Sim.FrameWorkOrder();
        if (order.Count < 3) return;
        var job = Sim.NewFrameJob(order);
        if (Sim.TryDelegateFrame(job) is { } done) { Sim.ApplyFrameOutcome(done); return; }
        _frame.Start(job);
        _frame.Open(WorkRules.InterfaceFor(Sim.State, Sim.Content, "install"));
    }

    private void PlayMessages()
    {
        if (Sim.State.MessagesWaiting == 0) return;
        var c = Sim.TakeEnquiry();
        Sim.State.Emit(Telo.Sim.Core.EventKind.Info, $"Message: {c.Name} ({c.Archetype}) wants a line at ${c.MonthlyFee}/month. Added to the work order.", c.Id);
    }

    private void EnterVan()
    {
        _player.SetActive(false);
        _van.SetDriven(true);
    }

    private void ExitVan()
    {
        _van.SetDriven(false);
        _player.SetActive(true);
        _player.PlaceAt(_van.GlobalPosition + _van.GlobalBasis.X * -2.4f + _van.Up * 0.6f, _van.Forward);
    }

    private void OpenAlmanac() => _almanac.Open(Telo.Sim.Content.InterfaceTier.Terminal); // PIP is from 2035: always a screen
    private void OpenNotebook() => _notebook.Open(WorkRules.InterfaceFor(Sim.State, Sim.Content, "billing"));

    // ---- loop --------------------------------------------------------------------------------
    public override void _Process(double delta)
    {
        if (!AnyModalOpen)
        {
            _dayClock += (float)delta;
            if (_dayClock >= DayLengthSeconds) { _dayClock -= DayLengthSeconds; Sim.AdvanceDay(); }
        }
        _sky.SetHour(Hour);
        _hud.SetCrosshair(!AnyModalOpen && !_van.Driven);

        SphereBody who = _van.Driven ? _van : _player;
        var biome = _planet.BiomeAt(PlanetBuilder.ToSim(who.GlobalPosition));
        string where = $"{biome}  alt {who.Altitude:0} m" + (_van.Driven ? $"  {Mathf.Abs(_van.Speed) * 3.6f:0} km/h" : _player.Flying ? "  SURVEY FLIGHT" : "");
        string? prompt = AnyModalOpen ? null : _van.Driven ? "[E] Get out   [W/S] drive   [A/D] steer" : _player.Focus is { } f ? $"[E] {f.Prompt()}" : null;
        _hud.Refresh(Sim, prompt, Hour, where);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (AnyModalOpen) return;
        if (_van.Driven && e.IsActionPressed("interact")) { ExitVan(); GetViewport().SetInputAsHandled(); }
        else if (e.IsActionPressed("almanac")) OpenAlmanac();
        else if (e.IsActionPressed("notebook")) OpenNotebook();
        else if (e.IsActionPressed("save")) Save();
        else if (e.IsActionPressed("load")) Load();
        else if (e.IsActionPressed("cancel")) Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void Save()
    {
        using var f = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
        f.StoreString(SaveCodec.ToJson(Sim.State));
        Sim.State.Emit(Telo.Sim.Core.EventKind.Info, "Saved.");
    }

    public void Load()
    {
        if (!Godot.FileAccess.FileExists(SavePath)) return;
        _pendingLoad = Godot.FileAccess.GetFileAsString(SavePath);
        GetTree().ReloadCurrentScene(); // the planet is rebuilt from the save's seed
    }

    // ---- automation hooks for CI / screenshots -----------------------------------------------
    private async Task RunSmoke()
    {
        // --- world: the player stands on the room floor, the van drives down Main Street ---
        async Task Physics(int frames) { for (int i = 0; i < frames; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
        await Physics(90);
        float floor = (float)_planet.Town.HeightM + 0.1f;
        bool standing = _player.IsOnFloor() && Mathf.Abs(_player.Altitude - floor) < 0.3f;
        var vanStart = _van.GlobalPosition;
        EnterVan();
        Input.ActionPress("move_forward");
        await Physics(360);
        Input.ActionRelease("move_forward");
        float drove = (_van.GlobalPosition - vanStart).Length();
        float vanAlt = _van.Altitude - (float)_planet.Town.HeightM;
        ExitVan();
        await Physics(60);
        bool world = standing && drove > 50 && Mathf.Abs(vanAlt) < 3 && _player.IsOnFloor();
        GD.Print($"WORLD {(world ? "OK" : "FAIL")}: standing={standing} playerAlt={_player.Altitude:0.00} floor={floor:0.00} vanDrove={drove:0}m vanAlt={vanAlt:0.0}");

        for (int i = 0; i < 3; i++) PlayMessages();
        var job = Sim.NewFrameJob(Sim.FrameWorkOrder());
        _frame.Start(job); _frame.Open(InterfaceTier.Paper);
        foreach (var l in job.Lines.Where(l => !l.IsPartyLine))
        {
            int t = job.VisibleTarget(l.Index) ?? job.Probe(l.Index);
            job.Jumper(l.Index, job.TestPort(t) ? t : job.SparePorts[0]);
        }
        _frame.Rebuild();
        var outcome = job.Evaluate();
        Sim.ApplyFrameOutcome(outcome);
        _frame.Close();
        _almanac.Open(InterfaceTier.Terminal); _almanac.Close();
        _notebook.Open(InterfaceTier.Paper); _notebook.Close();
        Sim.AdvanceDays(700);
        var json = SaveCodec.ToJson(Sim.State);
        var reloaded = new Simulation(_content, SaveCodec.FromJson(json));
        bool ok = world && outcome.Perfect && reloaded.State.Day == Sim.State.Day && Sim.State.ActIndex >= 1;
        GD.Print($"SMOKE {(ok ? "OK" : "FAIL")}: {Sim.State.Date} act={Sim.State.ActIndex} subs={Sim.State.LiveSubscribers} cash={Sim.State.Cash} archive={Sim.State.Archive}");
        GetTree().Quit(ok ? 0 : 1);
    }

    private async Task TakeScreenshot(string path)
    {
        Input.MouseMode = Input.MouseModeEnum.Visible; // stray mouse motion must not move the camera
        _dayClock = DayLengthSeconds * 0.2f;            // mid-morning light
        var cam = new Camera3D { Near = 0.1f, Far = 60000, Fov = 65 };
        AddChild(cam);
        cam.Current = true;
        async Task Shot(string suffix, Vector3 from, Vector3 lookAt, Vector3? camUp = null)
        {
            cam.LookAtFromPosition(from, lookAt, camUp ?? from.Normalized());
            for (int i = 0; i < 12; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetViewport().GetTexture().GetImage().SavePng(path.Replace(".png", suffix + ".png"));
        }
        for (int i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var hq = _town.Centre; var up = hq.Normalized();
        await Shot("-town", TownBuilder.Place(_planet, 22, 40, 0, 7).Origin, hq + up * 2);
        await Shot("-street", TownBuilder.Place(_planet, -70, 17, 0, 2.2f).Origin, TownBuilder.Place(_planet, 60, 16, 0, 2).Origin);
        await Shot("-aerial", TownBuilder.Place(_planet, 250, 500, 0, 420).Origin, hq);
        await Shot("-orbit", (up + _town.Hq.GlobalBasis.X * 0.5f + _town.Hq.GlobalBasis.Z * 0.35f).Normalized() * (float)_planet.RadiusM * 3.3f, Vector3.Zero, Vector3.Up);
        _dayClock = DayLengthSeconds * 0.62f;           // dusk
        await Shot("-dusk", TownBuilder.Place(_planet, -30, 60, 0, 12).Origin, hq + up * 6);
        cam.Current = false; _player.Camera.Current = true;
        _player.Camera.Rotation = new Vector3(-0.1f, 0, 0);
        for (int i = 0; i < 12; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetViewport().GetTexture().GetImage().SavePng(path.Replace(".png", "-room.png"));
        GD.Print($"SHOTS OK fps={Engine.GetFramesPerSecond()}");
        GetTree().Quit();
    }
}
