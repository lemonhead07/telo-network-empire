using Godot;
using Telo.Game.Player;
using Telo.Game.UI;
using Telo.Game.World;
using Telo.Sim.Build;
using Telo.Sim.Content;
using Telo.Sim.Minigames.Install;
using Telo.Sim.Minigames.OddJob;
using Telo.Sim.Net;
using Telo.Sim.Planet;
using Telo.Sim.Save;
using Telo.Sim.Town;
using Telo.Sim.Work;
using Telo.Sim.World;

namespace Telo.Game;

/// <summary>Composition root: loads content, owns the Simulation, builds the world, wires interactions. No game rules here.</summary>
public partial class GameRoot : Node3D
{
    /// <summary>Real seconds per in-game day (a full 24 h sun cycle). The bed skips to morning.</summary>
    public const float DayLengthSeconds = 480f;
    /// <summary>One hand-tuned world for now, so the planet is the same every time and content can be placed on it.</summary>
    public const ulong DefaultSeed = 1983;
    public const string SavePath = "user://save.json";
    private static string? _pendingLoad;

    public Simulation Sim { get; private set; } = null!;
    private FirstPersonController _player = null!;
    private Hud _hud = null!;
    private FrameView _frame = null!;
    private AlmanacView _almanac = null!;
    private NotebookView _notebook = null!;
    private BoardView _board = null!;
    private WorldScreen _boardScreen = null!;
    private Node3D _terminal = null!;
    private Interactable _terminalUse = null!;
    private WorldScreen _frameScreen = null!, _pipScreen = null!, _notebookScreen = null!;
    private float _minuteFrac;
    /// <summary>Seconds into the day (which starts at six), as the sky and lamps want it. The sim owns the clock; this is a view of it.</summary>
    private float _dayClock
    {
        get => (Sim.State.Minute + _minuteFrac) / Clock.MinutesPerDay * DayLengthSeconds;
        set { float m = Mathf.Clamp(value / DayLengthSeconds * Clock.MinutesPerDay, 0, Clock.MinutesPerDay - 1); Sim.State.Minute = (int)m; _minuteFrac = m - (int)m; }
    }
    /// <summary>Real seconds of holding E for a job step of this many game minutes: exactly as long as the clock says, within reason.</summary>
    private static float Hold(float minutes) => Mathf.Clamp(minutes * DayLengthSeconds / Clock.MinutesPerDay, 0.8f, 6f);
    private TicketRail _rail = null!;
    private WallCalendar _calendar = null!;
    private readonly HashSet<string> _clipboard = new();
    private readonly Dictionary<string, StaffFigure> _figures = new();
    /// <summary>Automated checks ignore who is home and whether the shop is open.</summary>
    private bool _ignoreHours;
    private float _slowTick;
    private ContentDb _content = null!;
    private PlanetGen _planet = null!;
    private PlanetSky _sky = null!;
    private Town _town = null!;
    private Van _van = null!;
    private Cargo _cargo = null!;
    private PipTips _pip = null!;
    private OnboardingStep _lastStep;
    private Node3D? _roadBarrier;
    private LineRack _rack = null!;
    private HashSet<string>? _paged;
    /// <summary>Odd jobs being done by hand indoors, by lot: the task and the thing on the rug.</summary>
    private readonly Dictionary<string, (OddJobTask Task, Interactable Bench, Func<bool> Wanted)> _oddJobs = new();
    private readonly List<Action> _lineRefresh = new();
    private readonly List<(PremiseDef Premise, PoleLineView View)> _lines = new();
    /// <summary>Automated checks pretend all the kit is to hand.</summary>
    private bool _kitEverywhere;
    private Transform3D _cameraHome;
    private Tween? _lean;
    /// <summary>House installs under way, by lot. Not saved: a half-done job is simply started again.</summary>
    private readonly Dictionary<string, InstallJob> _installs = new();

    private float Hour => (6f + _dayClock / DayLengthSeconds * 24f) % 24f;
    private bool AnyScreenOpen => _frame.IsOpen || _almanac.IsOpen || _notebook.IsOpen || _board.IsOpen;

    public override void _Ready()
    {
        InputSetup.Register();
        InstallSite.HoldFor = minutes => Hold(minutes);
        _content = LoadContent();
        var args = OS.GetCmdlineUserArgs();
        ulong seed = args.FirstOrDefault(a => a.StartsWith("--seed=")) is { } sa ? ulong.Parse(sa["--seed=".Length..]) : DefaultSeed;
        Sim = _pendingLoad is { } json ? new Simulation(_content, SaveCodec.FromJson(json, _content)) : Simulation.NewGame(_content, seed);
        _pendingLoad = null;

        // The world: a small planet, built from the sim's pure terrain function.
        _planet = new PlanetGen(Sim.State.Seed);
        float radius = (float)_planet.RadiusM;
        PlanetBuilder.Build(this, _planet);
        _sky = new PlanetSky(); AddChild(_sky);
        _sky.Setup(radius, PlanetBuilder.ToGodot(_planet.Town.Dir));
        _town = TownBuilder.Build(this, _planet, _content);
        foreach (var lot in _town.Lots) WireLot(lot);
        _ = BuildRoadsWhenPhysicsIsReady();

        _frame = new FrameView { Sim = Sim };
        _almanac = new AlmanacView { Sim = Sim };
        _notebook = new NotebookView { Sim = Sim };
        _board = new BoardView { Sim = Sim };
        BuildRoom(_town.Hq);
        foreach (var v in new ModalView[] { _frame, _almanac, _notebook, _board })
        {
            v.Opened += () => LeanInto(ScreenOf(v));
            v.Closed += LeanBack;
        }

        _player = new FirstPersonController { SeaRadius = radius, IsFrozen = () => AnyScreenOpen };
        AddChild(_player);
        _player.GlobalTransform = _town.PlayerSpawn;

        _van = new Van { SeaRadius = radius, IsPaved = p => _planet.IsPaved(PlanetBuilder.ToSim(p)) };
        AddChild(_van);
        _van.GlobalTransform = _town.VanSpawn;
        _cargo = new Cargo(_van, _player, this, () => Sim.State.Modems - _installs.Values.Count(j => j.IsDone(InstallStep.Modem)), () => Sim.State.LineCards);
        _van.Door.Prompt = () => _cargo.Held is { } h ? $"Put the {h.Label} back on the van" : "Drive the van";
        _van.Door.OnUse = () => { if (_cargo.Held is not null) _cargo.StowHeld(); else EnterVan(); };

        // The only overlay left: a small status strip. (Target: wall calendar, cash tin, pager.)
        var ui = new CanvasLayer();
        AddChild(ui);
        _hud = new Hud(); ui.AddChild(_hud);

        _pip = new PipTips(_hud);
        _lastStep = Onboarding.Current(Sim.State, _content);
        if (_lastStep == OnboardingStep.Enquiry) _pip.Once("hello");
        if (_lastStep == OnboardingStep.Free) _pip.Skip("customer", "van", "carry", "install", "step", "modem", "dial", "signed", "frame", "oddjob", "knock", "free");
        if (Sim.State.Staff > 0) _pip.Skip("canhire", "hired");
        _cargo.PickedUp += _ => _pip.Once("carry");
        // a save made in the middle of a house install comes back with the door open and the job where it was
        foreach (var id in Sim.State.Installs.Keys.ToList())
            if (Sim.State.Customers.FirstOrDefault(c => c.Id == id) is { LotId: not null } mid && _town.Lots.FirstOrDefault(l => l.Lot.Id == mid.LotId) is { Install: not null } midView)
                StartInstall(midView, mid);

        RefreshIdleScreens();
        if (args.Contains("--smoke")) _ = RunSmoke();
        if (args.FirstOrDefault(a => a.StartsWith("--shot=")) is { } shot) _ = TakeScreenshot(shot["--shot=".Length..]);
    }

    private static ContentDb LoadContent() => ContentDb.FromJson(ContentDb.FileNames.ToDictionary(
        f => f, f => Godot.FileAccess.GetFileAsString($"res://content/{f}") is { Length: > 0 } t ? t
            : throw new InvalidOperationException($"Missing res://content/{f}")));

    private async Task BuildRoadsWhenPhysicsIsReady()
    {
        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        TownBuilder.BuildRoads(_town.Root, _planet, GetWorld3D().DirectSpaceState);
        foreach (var p in _content.Premises.Values) BuildPoleLine(p);
        if (!Onboarding.RoadOpen(Sim.State, _content)) BuildRoadBarrier();
    }

    /// <summary>A council barrier across the road out of town, until there is a reason to leave. You can drive round it on the grass; it is a hint, not a wall.</summary>
    private void BuildRoadBarrier()
    {
        if (_planet.Roads.Count == 0) return;
        var pts = _planet.Roads[0].Points; float R = (float)_planet.RadiusM;
        int i = Enumerable.Range(0, pts.Count).FirstOrDefault(k => (pts[k] - _planet.Town.Dir).Length * _planet.RadiusM > _planet.Town.RadiusM * 0.7);
        if (i <= 0 || i >= pts.Count - 1) return;
        var dir = PlanetBuilder.ToGodot(pts[i]);
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(dir * (R + 400), dir * (R - 100), 1));
        var ground = hit.Count > 0 ? (Vector3)hit["position"] : dir * (R + (float)_planet.Height(pts[i]));
        var along = (PlanetBuilder.ToGodot(pts[i + 1]) - PlanetBuilder.ToGodot(pts[i - 1])).Normalized();
        var across = along.Cross(dir).Normalized();
        var body = new StaticBody3D { Name = "RoadBarrier", Transform = new Transform3D(new Basis(across, dir, across.Cross(dir)), ground) };
        var lp = new LowPoly();
        for (int k = -4; k <= 4; k++) lp.Box(new Vector3(k * 2f, 0.9f, 0), new Vector3(2f, 0.35f, 0.12f), k % 2 == 0 ? new Color(0.9f, 0.25f, 0.15f) : new Color(0.95f, 0.93f, 0.88f));
        foreach (float x in new[] { -8.5f, -3f, 3f, 8.5f }) lp.Box(new Vector3(x, 0.5f, 0), new Vector3(0.12f, 1f, 0.5f), new Color(0.3f, 0.3f, 0.32f));
        lp.Box(new Vector3(0, 1.75f, 0), new Vector3(3.4f, 1.1f, 0.08f), new Color(0.95f, 0.8f, 0.2f));
        body.AddChild(lp.Instance());
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(18f, 2.2f, 0.5f) }, Position = new Vector3(0, 1.1f, 0) });
        foreach (int side in new[] { -1, 1 })
            body.AddChild(new Label3D
            {
                Text = "ROAD CLOSED\nCouncil resurfacing\nCompletion: pending", Position = new Vector3(0, 1.75f, side * 0.06f), RotationDegrees = new Vector3(0, side < 0 ? 180 : 0, 0),
                FontSize = 48, PixelSize = 0.006f, Modulate = new Color(0.1f, 0.08f, 0.05f), OutlineSize = 0,
            });
        AddChild(body);
        _roadBarrier = body;
    }

    // ---- pole lines the player builds by hand --------------------------------------------------
    private void BuildPoleLine(PremiseDef p)
    {
        var sites = LineRules.PoleSites(_planet, p);
        if (sites.Count == 0) return;
        var end = _town.Lots.FirstOrDefault(l => l.Lot.Premise == p.Id) is { } lot ? lot.Body.ToGlobal(new Vector3(-3.5f, 3f, 3.5f)) : PlanetBuilder.ToGodot(sites[^1]) * (float)_planet.RadiusM;
        var view = new PoleLineView(_planet, sites, end, GetWorld3D().DirectSpaceState);
        AddChild(view);
        _lines.Add((p, view));
        var s = Sim.State;
        void Refresh()
        {
            var l = LineRules.ProgressOf(s, p.Id);
            bool mine = l.Planted > 0 || _cargo.Held is { Tool: Tools.Pole } || WorkRules.Current(s, _content, "install").Mode != WorkMode.Manual;
            view.Sync(l.Planted, l.Strung, mine);
        }
        _lineRefresh.Add(Refresh);

        view.PlantSpot.Prompt = () =>
        {
            int k = LineRules.ProgressOf(s, p.Id).Planted + 1;
            if (s.Cash < p.PoleLine!.PoleCost) return $"Pole {k} of {view.Count} goes here. A pole is ${p.PoleLine.PoleCost}; you have ${s.Cash}";
            var missing = LineRules.PlantNeeds & ~KitOnSite();
            return missing != Tools.None ? $"Pole {k} of {view.Count} goes here: bring {Cargo.Names(missing)} from the van"
                : $"Plant pole {k} of {view.Count} (${p.PoleLine.PoleCost})";
        };
        view.PlantSpot.CanUse = () => LineRules.CanPlant(s, _planet, p) && (LineRules.PlantNeeds & ~KitOnSite()) == Tools.None;
        view.PlantSpot.OnUse = () =>
        {
            if (!LineRules.Plant(s, _planet, p, KitOnSite())) return;
            if (_cargo.Items.FirstOrDefault(i => i.Tool == Tools.Pole && i.Where != Carryable.Place.Van) is { } used) _cargo.Stow(used); // the next pole is on the rack
            Refresh();
        };
        view.PlantSpot.HoldSeconds = () => Hold(LineRules.PlantMinutes);
        view.StringSpot.HoldSeconds = () => Hold(LineRules.StringMinutes);
        view.StringSpot.Prompt = () =>
        {
            var missing = LineRules.StringNeeds & ~KitOnSite();
            int k = LineRules.ProgressOf(s, p.Id).Strung + 1;
            return missing != Tools.None ? $"Span {k} of {view.Count} is strung from here: you need {Cargo.Names(missing)}"
                : k == view.Count ? $"String the last span, to {p.Name}" : $"String span {k} of {view.Count} to this pole";
        };
        view.StringSpot.CanUse = () => LineRules.CanString(s, _planet, p) && (LineRules.StringNeeds & ~KitOnSite()) == Tools.None;
        view.StringSpot.OnUse = () =>
        {
            if (!LineRules.String(s, _content, _planet, p, KitOnSite())) return;
            Refresh();
        };
        Refresh();
    }

    // ---- the room: every "menu" is an object -------------------------------------------------
    private void BuildRoom(Node3D room)
    {
        float back = -RoomBuilder.D / 2 + 0.1f;

        // The frame: a backboard on the wall. Its face is the work surface.
        RoomBuilder.Prop(room, "Frame", new Vector3(-1.4f, 1.35f, back), new Vector3(1.6f, 1.1f, 0.08f), RoomBuilder.Board, FramePrompt, UseFrame);
        _frameScreen = Screen(room, _frame, 1.44f, new Vector3(-1.4f, 1.35f, back + 0.05f), Vector3.Zero, glows: false);
        RoomBuilder.Label(room, "FRAME", new Vector3(-1.4f, 2.0f, back + 0.06f), 0.12f);

        // The Board's lines in: lamps, empty sockets for cards not yet bought, and the tally of engaged tones.
        _rack = new LineRack { Position = new Vector3(-0.22f, 1.4f, back + 0.04f) };
        room.AddChild(_rack);
        _rack.Slot.Prompt = () =>
        {
            var s = Sim.State; var ex = BoardRules.Home(s);
            if (s.LineCards > 0)
                return (BoardRules.FitNeeds & ~KitOnSite()) != Tools.None ? "A line card is waiting on the van: carry it in here" : "Slot in the line card";
            return s.LastEvening is { } eve
                ? $"The Board: {ex.LinesIn} line(s) in. Last night {eve.Attempts} calls, {eve.Engaged} engaged tones, busiest at {BoardRules.Clock(eve.BusiestSlot)}"
                : $"The Board: {ex.LinesIn} line(s) in";
        };
        _rack.Slot.CanUse = () => Sim.State.LineCards > 0 && (BoardRules.FitNeeds & ~KitOnSite()) == Tools.None;
        _rack.Slot.HoldSeconds = () => Hold(BoardRules.FitMinutes);
        _rack.Slot.OnUse = () =>
        {
            if (!BoardRules.FitCard(Sim.State, KitOnSite())) return;
            if (_cargo.Items.FirstOrDefault(i => i.Tool == Tools.LineCard && i.Where != Carryable.Place.Van) is { } used) _cargo.Stow(used);
            _cargo.Refresh();
            _pip.Once("cardfit");
        };

        // Desk with answering machine, PIP's handset on a stand, and the notebook lying open.
        RoomBuilder.Box(room, new Vector3(1.3f, 0.37f, back + 0.45f), new Vector3(1.7f, 0.74f, 0.8f), RoomBuilder.Wood, solid: true);
        RoomBuilder.Prop(room, "AnsweringMachine", new Vector3(0.72f, 0.8f, back + 0.4f), new Vector3(0.26f, 0.1f, 0.2f), new Color(0.08f, 0.08f, 0.08f),
            () => Sim.State.MessagesWaiting > 0 ? $"Play messages ({Sim.State.MessagesWaiting} waiting)" : "No new messages", PlayMessages);

        RoomBuilder.Prop(room, "Handset", new Vector3(1.3f, 0.86f, back + 0.36f), new Vector3(0.38f, 0.26f, 0.03f), new Color(0.1f, 0.11f, 0.14f),
            () => Onboarding.DeskOpen(Sim.State, _content) ? "Read PIP's Almanac" : "PIP's handset. It has more to show you once you know the round",
            () => { if (Onboarding.DeskOpen(Sim.State, _content)) _almanac.Open(InterfaceTier.Terminal); }).RotationDegrees = new Vector3(-18, 0, 0);
        _pipScreen = Screen(room, _almanac, 0.34f, new Vector3(1.3f, 0.865f, back + 0.385f), new Vector3(-18, 0, 0), glows: true);

        RoomBuilder.Prop(room, "Notebook", new Vector3(1.85f, 0.745f, back + 0.5f), new Vector3(0.52f, 0.02f, 0.34f), new Color(0.5f, 0.12f, 0.1f),
            () => Onboarding.DeskOpen(Sim.State, _content) ? "Read the company notebook" : "The company notebook. Nothing in it yet: there is no company",
            () => { if (Onboarding.DeskOpen(Sim.State, _content)) _notebook.Open(WorkRules.InterfaceFor(Sim.State, Sim.Content, "billing")); });
        _notebookScreen = Screen(room, _notebook, 0.48f, new Vector3(1.85f, 0.758f, back + 0.5f), new Vector3(-90, 0, 0), glows: false);

        // The Board's terminal, on a side table. It turns up with your first hire. Worked with the number keys.
        _terminal = new Node3D { Position = new Vector3(2.62f, 0, back + 0.42f), Visible = false };
        room.AddChild(_terminal);
        _terminal.AddChild(new LowPoly().Box(new Vector3(0, 0.34f, 0), new Vector3(0.7f, 0.68f, 0.62f), RoomBuilder.Wood * 0.9f)
            .Box(new Vector3(0, 0.9f, -0.08f), new Vector3(0.52f, 0.44f, 0.42f), new Color(0.72f, 0.7f, 0.62f))          // the tube
            .Box(new Vector3(0, 0.7f, 0.2f), new Vector3(0.46f, 0.04f, 0.17f), new Color(0.66f, 0.64f, 0.57f))           // keyboard
            .Box(new Vector3(0, 0.725f, 0.2f), new Vector3(0.4f, 0.012f, 0.12f), new Color(0.2f, 0.2f, 0.2f)).Instance());
        _boardScreen = Screen(_terminal, _board, 0.42f, new Vector3(0, 0.9f, 0.135f), Vector3.Zero, glows: true);
        _terminalUse = new Interactable { Position = new Vector3(0, 0.85f, 0.1f), CollisionLayer = 0, CollisionMask = 0 };
        _terminalUse.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.6f, 0.6f, 0.7f) } });
        _terminalUse.Prompt = () => "Sit down at the Board's terminal";
        _terminalUse.OnUse = () => { _board.Open(InterfaceTier.Terminal); if (BoardService.NewFiles(Sim.State).Count > 0) _pip.Once("files"); };
        _terminal.AddChild(_terminalUse);

        // The left wall: today's job tickets on a rail, and the calendar with pay day on it.
        _rail = new TicketRail { Position = new Vector3(-RoomBuilder.W / 2 + 0.08f, 1.45f, -0.95f), RotationDegrees = new Vector3(0, 90, 0) };
        room.AddChild(_rail);
        _rail.Prompt = TicketPrompt;
        _rail.CanUse = t => t.AssignedTo is null && Sim.State.StaffMembers.Any(m => m.Does.Contains(t.Kind));
        _rail.Use = t => { if (!_clipboard.Remove(t.Key)) { _clipboard.Add(t.Key); _pip.Once("ticket"); } SyncPaper(); };
        _calendar = new WallCalendar { Position = new Vector3(-RoomBuilder.W / 2 + 0.07f, 1.5f, 0.2f), RotationDegrees = new Vector3(0, 90, 0) };
        room.AddChild(_calendar);

        // Camp bed. Sleeping is how days pass quickly.
        RoomBuilder.Prop(room, "Bed", new Vector3(-2.2f, 0.22f, 1.4f), new Vector3(0.9f, 0.44f, 1.9f), RoomBuilder.Fabric,
            () => Guidance.IsQuiet(Sim.State, _content) ? "Sleep until something needs you (days may pass)" : "Sleep until morning",
            () =>
            {
                int days = 1;
                if (Guidance.IsQuiet(Sim.State, _content)) days = Sim.RestUntilNeeded(); else Sim.Sleep();
                _minuteFrac = 0;
                if (days > 1) _pip.Say($"{days} days went by. {Sim.State.Date}. Something needs you.");
                RefreshIdleScreens();
            });
    }

    private static WorldScreen Screen(Node3D room, ModalView view, float widthM, Vector3 pos, Vector3 rotDeg, bool glows)
    {
        var s = WorldScreen.Create(view, widthM, glows);
        s.Position = pos; s.RotationDegrees = rotDeg;
        room.AddChild(s);
        return s;
    }

    private WorldScreen ScreenOf(ModalView v) => v == _frame ? _frameScreen : v == _almanac ? _pipScreen : v == _board ? _boardScreen : _notebookScreen;

    private void LeanInto(WorldScreen screen)
    {
        var cam = _player.Camera;
        if (_lean is null || !_lean.IsRunning()) _cameraHome = cam.Transform;
        _lean?.Kill();
        var size = GetViewport().GetVisibleRect().Size;
        _lean = CreateTween();
        _lean.TweenProperty(cam, "global_transform", screen.LeanPose(cam.Fov, size.X / size.Y), 0.35).SetTrans(Tween.TransitionType.Sine);
    }

    private void LeanBack()
    {
        _lean?.Kill();
        _lean = CreateTween();
        _lean.TweenProperty(_player.Camera, "transform", _cameraHome, 0.25).SetTrans(Tween.TransitionType.Sine);
        RefreshIdleScreens();
    }

    /// <summary>Screens show live information even when nobody is using them.</summary>
    private void RefreshIdleScreens()
    {
        _frame.Show(WorkRules.InterfaceFor(Sim.State, Sim.Content, "install"));
        _almanac.Show(InterfaceTier.Terminal);
        _notebook.Show(WorkRules.InterfaceFor(Sim.State, Sim.Content, "billing"));
        _board.Show(InterfaceTier.Terminal);
    }

    // ---- interactions ------------------------------------------------------------------------
    private string FramePrompt()
    {
        if (Sim.State.ReadyForFrame == 0)
            return Sim.State.PendingInstalls > 0 ? "The frame (fit the sockets at their houses first)" : "The frame (no installs waiting)";
        return WorkRules.Current(Sim.State, Sim.Content, "install").Mode != WorkMode.Manual
            ? $"Wire {Sim.State.ReadyForFrame} line(s) yourself (otherwise your technician does one customer a day)"
            : $"Work the frame ({Sim.State.ReadyForFrame} to wire)";
    }

    private void UseFrame()
    {
        if (Sim.State.ReadyForFrame == 0) return;
        var job = Sim.NewFrameJob(Sim.FrameWorkOrder());
        _pip.Once("frame");
        _frame.Start(job);
        _frame.Open(WorkRules.InterfaceFor(Sim.State, Sim.Content, "install"));
    }

    private void PlayMessages()
    {
        if (Sim.State.MessagesWaiting == 0) return;
        if (Sim.TakeEnquiry() is not null) _pip.Once("customer");
    }

    /// <summary>What can be done at this building right now? Decided from sim state every time it is asked.</summary>
    private (string Prompt, Action? Use) LotAction(LotDef lot)
    {
        var s = Sim.State;
        if (lot.Kind == LotKind.Depot)
            return s.HasDepot ? ("1 Main Street: your depot. Staff collect their tickets here each morning and are paid by the week", null)
                : StaffRules.CanLeaseDepot(s) ? ($"Sign the lease on 1 Main Street: ${StaffRules.DepotDeposit} down, ${StaffRules.DepotRentPerMonth} a month. Your staff will work from here", () => { if (StaffRules.LeaseDepot(s)) _pip.Once("depot"); })
                : s.StaffMembers.Count == 0 ? ("TO LET: 1 Main Street. A yard and a workshop. (For when you have someone to put in it.)", null)
                : ($"TO LET: 1 Main Street. ${StaffRules.DepotDeposit} down; you have ${s.Cash}", null);
        // people keep hours: nobody comes to the door when nobody is in (a job already under way carries on)
        if (!_ignoreHours && !_installs.ContainsKey(lot.Id) && !_oddJobs.ContainsKey(lot.Id) && HomeRules.WhyNot(s, _content, lot) is { } away)
        {
            _pip.Once("hours");
            return ($"{lot.Resident}: {away}", null);
        }
        // a broken line comes before anything else at this door
        if (TownRules.FaultAt(s, lot.Id) is { } fault && TownRules.CustomerAt(s, lot.Id) is { } broken)
        {
            var missing = TownRules.RepairNeeds & ~KitOnSite();
            return missing == Tools.None
                ? ($"Fix {broken.Name}'s line: {fault.Text}", () => TownRules.Repair(s, fault.Id, KitOnSite()))
                : ($"{broken.Name} has a fault: {fault.Text}. You need {Cargo.Names(missing)} from the van", null);
        }
        if (OrderRules.OrderAt(s, lot.Id) is { } order && _content.Products.TryGetValue(order.ProductId, out var product)
            && _town.Lots.FirstOrDefault(l => l.Lot.Id == lot.Id) is { Install: not null } orderHome)
        {
            if (_oddJobs.TryGetValue(lot.Id, out var fitting))
                return ($"{fitting.Task.Title}: {(fitting.Task.Next is { } n ? n.Verb.ToLowerInvariant() : "show them how it works")} (indoors, on the rug)", null);
            return ($"{lot.Resident} ordered: {product.Name.ToLowerInvariant()} (+${product.Price}). They let you in", () => StartOrder(orderHome, order, product));
        }
        if (TownRules.JobAt(s, lot.Id) is { } job && _town.Lots.FirstOrDefault(l => l.Lot.Id == lot.Id) is { Install: not null } jobHome)
        {
            // houses: the job is done by hand indoors
            if (_oddJobs.TryGetValue(lot.Id, out var doing))
                return ($"{doing.Task.Title}: {(doing.Task.Next is { } nx ? nx.Verb.ToLowerInvariant() : "hand it back")} (indoors, on the rug)", null);
            return ($"{job.Title} for {lot.Resident} (+${job.Pay}): they let you in. Bring the toolbox", () => StartOddJob(jobHome, job));
        }
        if (TownRules.JobAt(s, lot.Id) is { } quick)
        {
            // the shop and the farm aren't enterable yet: the job is still done at the door
            return ($"{quick.Title} for {lot.Resident} (+${quick.Pay})", () => TownRules.CompleteJob(s, _content, quick.Id));
        }
        var cust = TownRules.CustomerAt(s, lot.Id);
        if (cust is { SocketFitted: false } && _town.Lots.FirstOrDefault(l => l.Lot.Id == lot.Id) is { Install: not null } view)
        {
            if (_installs.TryGetValue(lot.Id, out var running))
                return ($"{cust.Name}'s install: {(running.NextStep is { } n ? InstallJob.Def(n).Verb.ToLowerInvariant() + (InstallJob.Def(n).Outside ? " (outside wall, left of the window)" : " (indoors)") : "test the line, then sign the job sheet indoors")}", null);
            return s.Modems > 0
                ? (WorkRules.Current(s, _content, "install").Mode != WorkMode.Manual
                    ? $"{cust.Name} is booked in with your technician. Or do the install yourself now"
                    : $"Start {cust.Name}'s install: they let you in (about an hour; bring the kit from the van)", () => StartInstall(view, cust))
                : ($"{cust.Name} is waiting for their line. You have no modems (Sol's Electronics sells them)", null);
        }
        if (cust is { SocketFitted: false })
            return s.Modems > 0
                ? ($"Fit the wall socket for {cust.Name} (uses a modem)", () => TownRules.FitSocket(s, cust.Id))
                : ($"{cust.Name} is waiting for a socket. You have no modems (Sol's Electronics sells them)", null);
        if (cust is not null)
            return (cust.Installed ? $"{lot.Resident}: subscriber, satisfaction {cust.Satisfaction}%" : $"{lot.Resident}: socket fitted. Wire their line at the frame", null);

        if (lot.Premise is not null)
            return ($"{lot.Resident}: \"A line out here? I've asked twice.\" (Promise Ledger, in your notebook)", null);

        // a new player isn't asked to sell door to door until they have seen one customer through
        if (!Onboarding.CanKnock(s, _content)) return (lot.Kind == LotKind.House ? $"{lot.Resident}'s house" : lot.Resident, null);
        var h = TownRules.HouseholdOf(s, lot.Id);
        if (s.Day < h.RetryDay)
            return (h.Status == HouseholdStatus.Declined ? $"{lot.Resident} said no. Try again in {h.RetryDay - s.Day} days" : $"Nobody home at {lot.Resident}'s. Try tomorrow", null);
        return ($"Knock on {lot.Resident}'s door", () => Sim.Knock(lot.Id));
    }

    // ---- the install at a customer's house: the sim owns the job, the house shows it -------------
    private void StartInstall(LotView view, Customer cust)
    {
        if (Sim.NewInstallJob(cust.Id) is not { } job) return;
        _installs[view.Lot.Id] = job;
        OpenDoor(view, true);
        _pip.Once("install");
        view.Install!.Begin(job,
            (step, rush) =>
            {
                if (step == InstallStep.Test) { var r = job.Test(); view.Install.NoteTest(r); _pip.Once(r.DialTone ? "dial" : "nodial"); }
                else if (rush) { if (job.Rush(step, KitOnSite())) _pip.Once("bodge"); }
                else if (job.Do(step, KitOnSite())) _pip.Once(step == InstallStep.Modem ? "modem" : "step");
                Sim.RecordInstall(job);
                // the modem is out of its box and on their table: the empty box goes back on the van
                if (job.IsDone(InstallStep.Modem) && _cargo.Items.FirstOrDefault(i => i.Tool == Tools.ModemBox && i.Where != Carryable.Place.Van) is { } boxed) _cargo.Stow(boxed);
                _cargo.Refresh();
                view.Install.Sync();
            },
            () =>
            {
                if (!Sim.ApplyInstallOutcome(job.Evaluate())) return;
                _installs.Remove(view.Lot.Id);
                view.Install.ShowFitted();
                _cargo.Refresh();
                _pip.Once("signed");
            },
            KitOnSite, Cargo.Names);
    }

    /// <summary>Kit in the player's hands or put down within a few metres of them.</summary>
    private Tools KitOnSite() => _kitEverywhere ? Tools.All : _cargo.OnSite(_player.GlobalPosition);

    private static void OpenDoor(LotView view, bool open)
    {
        if (view.DoorOpen == open) return;
        view.DoorOpen = open;
        view.SetDoor?.Invoke(open);
    }

    /// <summary>Houses catch up with the sim: finished installs are visible, and doors close behind you once you've left.</summary>
    private void SyncHouses(Vector3 playerPos)
    {
        foreach (var v in _town.Lots)
        {
            if (v.Install is null) continue;
            if (!v.Install.Fitted && TownRules.CustomerAt(Sim.State, v.Lot.Id) is { SocketFitted: true })
            {
                _installs.Remove(v.Lot.Id); // the technician got there first
                v.Install.ShowFitted();
            }
            // Whatever else happens, you are never shut in: a door with you behind it opens.
            if (!v.DoorOpen && Indoors(v, playerPos, 0)) OpenDoor(v, true);
            // The door closes behind you: only once the job is signed off, you have stepped off the doorstep, and none of your kit is still indoors.
            if (_oddJobs.TryGetValue(v.Lot.Id, out var bench) && !bench.Wanted()) EndOddJob(v.Lot.Id); // they got someone else in
            if (v.DoorOpen && !_installs.ContainsKey(v.Lot.Id) && !_oddJobs.ContainsKey(v.Lot.Id) && !Indoors(v, playerPos, 1.1f)
                && !_cargo.Items.Any(i => i.Where == Carryable.Place.Ground && Indoors(v, i.GlobalPosition, 0))) OpenDoor(v, false);
        }
    }

    // ---- odd jobs by hand: the thing to fix sits on the rug in their front room ------------------
    private void StartOddJob(LotView view, OddJob job) =>
        StartBenchJob(view, Sim.NewOddJobTask(job.Id), new Color(0.22f, 0.2f, 0.18f), $"Hand it back to {view.Lot.Resident} (+${job.Pay})",
            bodged => TownRules.CompleteJob(Sim.State, _content, job.Id, bodged), () => TownRules.JobAt(Sim.State, view.Lot.Id)?.Id == job.Id, "oddstart");

    /// <summary>Something from the catalogue, fitted in the customer's front room.</summary>
    private void StartOrder(LotView view, Order order, ProductDef product) =>
        StartBenchJob(view, OrderRules.NewTask(_content, order), new Color(0.78f, 0.72f, 0.58f), $"Show {view.Lot.Resident} how it works (+${product.Price})",
            bodged => Sim.CompleteOrder(order.Id, bodged), () => Sim.State.Orders.Any(o => o.Id == order.Id), "order");

    /// <summary>A small job done by hand indoors: the thing sits on the rug, E works through its steps, R thumps it.</summary>
    private void StartBenchJob(LotView view, OddJobTask? task, Color colour, string handBack, Action<bool> complete, Func<bool> wanted, string pipKey)
    {
        if (task is null) return;
        var bench = new Interactable { Position = new Vector3(1.5f, TownBuilder.HouseFloor + 0.17f, -0.9f), CollisionLayer = 2, CollisionMask = 0 };
        bench.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.6f, 0.5f, 0.5f) } });
        bench.AddChild(new LowPoly().Box(Vector3.Zero, new Vector3(0.42f, 0.26f, 0.3f), colour)
            .Box(new Vector3(0, 0.14f, 0), new Vector3(0.3f, 0.03f, 0.2f), new Color(0.8f, 0.72f, 0.5f))
            .Box(new Vector3(0.12f, 0.05f, 0.155f), new Vector3(0.08f, 0.08f, 0.02f), new Color(0.85f, 0.2f, 0.15f)).Instance());
        bench.AddChild(new Label3D { Text = task.Title, Position = new Vector3(0, 0.36f, 0), FontSize = 40, PixelSize = 0.002f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Modulate = new Color(0.98f, 0.95f, 0.85f), OutlineSize = 10 });
        bench.Prompt = () =>
        {
            if (task.Done) return handBack;
            var d = task.Next!;
            var missing = task.Missing(KitOnSite());
            return missing != Tools.None ? $"{d.Verb}: you need {Cargo.Names(missing)} here (from the van)"
                : $"{d.Verb} ({task.NextProgress + 1} of {d.Clicks})";
        };
        bench.CanUse = () => task.Done || task.Missing(KitOnSite()) == Tools.None;
        bench.OnUse = () =>
        {
            if (!task.Done) { task.Do(KitOnSite()); return; }
            complete(task.Bodged);
            EndOddJob(view.Lot.Id);
        };
        bench.AltPrompt = () => task.Done ? null : "Give it a thump and call it done";
        bench.HoldSeconds = () => task.Done ? 0f : Hold(task.MinutesFor(task.Next!));
        bench.OnAlt = () => { if (task.Bodge()) _pip.Once("thump"); };
        view.Body.AddChild(bench);
        _oddJobs[view.Lot.Id] = (task, bench, wanted);
        OpenDoor(view, true);
        _pip.Once(pipKey);
    }

    private void EndOddJob(string lotId)
    {
        if (!_oddJobs.Remove(lotId, out var doing)) return;
        doing.Bench.QueueFree();
    }

    /// <summary>God keys: decree that something has happened, then let every view catch up.</summary>
    private void God(string what)
    {
        _pip.Say("By decree: " + what);
        foreach (var v in _town.Lots) if (_installs.Remove(v.Lot.Id)) v.Install?.ShowFitted();
        RefreshIdleScreens();
    }

    // ---- paper and people: tickets on the rail, handed to staff in person --------------------------
    private string TicketPrompt(Ticket t)
    {
        var s = Sim.State;
        string waited = t.WaitingDays > 0 ? $", waiting {t.WaitingDays} day{(t.WaitingDays == 1 ? "" : "s")}" : "";
        if (t.AssignedTo is { } id && StaffRules.Member(s, id) is { } who) return $"{t.Title}{waited}. {who.Name.Split(' ')[0]} has this ticket";
        if (_clipboard.Contains(t.Key)) return $"{t.Title}: on your clipboard. Hand it over in person, or put it back";
        return s.StaffMembers.FirstOrDefault(m => m.Does.Contains(t.Kind)) is { } could
            ? $"{t.Title}{waited}. Take this ticket to hand to {could.Name.Split(' ')[0]}"
            : $"{t.Title}{waited}. Yours to do";
    }

    private string TicketLine(Ticket t)
    {
        string who = t.AssignedTo is { } id && StaffRules.Member(Sim.State, id) is { } m ? $"with {m.Name.Split(' ')[0].ToUpperInvariant()}" : _clipboard.Contains(t.Key) ? "ON YOUR CLIPBOARD" : "";
        return $"{t.Kind.ToString().ToUpperInvariant()}  {(t.WaitingDays > 0 ? $"day {t.WaitingDays + 1}" : "new")}\n{t.Title}\n{who}";
    }

    /// <summary>The rail, the calendar and where each member of staff is standing all follow the sim.</summary>
    private void SyncPaper()
    {
        var s = Sim.State;
        var tickets = StaffRules.Tickets(s, _content);
        _clipboard.RemoveWhere(k => tickets.All(t => t.Key != k || t.AssignedTo is not null));
        _rail.Show(tickets, TicketLine);

        var today = new Telo.Sim.Core.GameDate(s.Day).ToDate();
        var lines = StaffRules.WeekAhead(s, _content).Select(d =>
        {
            var date = new Telo.Sim.Core.GameDate(d.Day).ToDate();
            return $"{(d.Day == s.Day ? "▶" : "  ")} {date:ddd d}{(d.Note.Length > 0 ? "   " + d.Note : "")}";
        });
        _calendar.Show($"{today:MMMM yyyy}".ToUpperInvariant() + $"        {Clock.Text(s)}\n\n" + string.Join("\n", lines));

        foreach (var m in s.StaffMembers.Where(m => m.Does.Count > 0))
        {
            if (!_figures.TryGetValue(m.Id, out var fig))
            {
                fig = new StaffFigure(m.Name, new Color(0.25f, 0.4f, 0.6f));
                AddChild(fig);
                _figures[m.Id] = fig;
                var member = m;
                fig.Hot.Prompt = () => StaffPrompt(member);
                fig.Hot.CanUse = () => TicketToGive(member) is not null;
                fig.Hot.OnUse = () =>
                {
                    if (TicketToGive(member) is not { } t || !StaffRules.Assign(Sim.State, _content, t.Key, member.Id)) return;
                    _clipboard.Remove(t.Key);
                    _pip.Once("given");
                    SyncPaper();
                };
            }
            bool here = Clock.WorkingHours(s) || _ignoreHours;
            fig.Visible = here;
            fig.Hot.CollisionLayer = here ? 2u : 0u;
            // at the job they are on; otherwise waiting where they collect tickets: your door, or the depot once there is one
            var at = tickets.FirstOrDefault(t => t.AssignedTo == m.Id && t.LotId is not null) is { } job && _town.Lots.FirstOrDefault(l => l.Lot.Id == job.LotId) is { } site
                ? site.Body.GlobalTransform * new Transform3D(Basis.Identity, new Vector3(1.9f, 0, 4.9f))
                : s.HasDepot && _town.Lots.FirstOrDefault(l => l.Lot.Kind == LotKind.Depot) is { } depot
                    ? depot.Body.GlobalTransform * new Transform3D(Basis.Identity, new Vector3(1.6f, 0, 4.6f))
                    : _town.Hq.GlobalTransform * new Transform3D(Basis.Identity, new Vector3(RoomBuilder.DoorX + 2.4f, -0.1f, RoomBuilder.D / 2 + 1.1f));
            fig.GlobalTransform = at;
        }
    }

    private Ticket? TicketToGive(StaffMember m)
    {
        var tickets = StaffRules.Tickets(Sim.State, _content);
        return _clipboard.Select(k => tickets.FirstOrDefault(t => t.Key == k)).FirstOrDefault(t => t is not null && StaffRules.CanTake(Sim.State, _content, m, t));
    }

    private string StaffPrompt(StaffMember m)
    {
        var s = Sim.State;
        string first = m.Name.Split(' ')[0];
        if (TicketToGive(m) is { } t) return $"Give {first} the ticket: {t.Title}";
        int queue = StaffRules.Queue(s, _content, m);
        string pay = s.HasDepot ? $"${m.WeeklyWage} a week" : $"${m.PieceRate} a job";
        if (_clipboard.Count > 0 && queue >= m.JobsPerDay) return $"{first} has a day's work in hand ({queue} tickets). Paid {pay} on Fridays";
        if (queue > 0) return $"{m.Name}: {queue} ticket{(queue == 1 ? "" : "s")} in hand, {m.JobsToday} done today. Paid {pay} on Fridays";
        return s.HasDepot ? $"{m.Name}. Collects tickets from the depot each morning. Paid {pay} on Fridays"
            : $"{m.Name}, waiting for a job ticket. Take one from the rail in your room. Paid {pay} on Fridays";
    }

    /// <summary>Is this world position inside the house (plus a margin, e.g. the doorstep)?</summary>
    private static bool Indoors(LotView v, Vector3 world, float margin)
    {
        var p = v.Body.ToLocal(world);
        return Mathf.Abs(p.X) < 4f + margin && p.Z > -3.5f - margin && p.Z < 3.5f + margin && p.Y < 6f;
    }

    /// <summary>The first hour unlocks one thing at a time; PIP says so at the moment it happens.</summary>
    private void WatchProgress()
    {
        var s = Sim.State;
        var step = Onboarding.Current(s, _content);
        if (step != _lastStep)
        {
            _lastStep = step;
            if (step == OnboardingStep.OddJob) _pip.Once("oddjob");
            if (step == OnboardingStep.Knock) _pip.Once("knock");
            if (step == OnboardingStep.Free) _pip.Once("free");
        }
        if (step >= OnboardingStep.Install && s.Modems == 0 && s.Customers.Any(c => c.Live && !c.SocketFitted)) _pip.Once("nomodem");
        if (s.Staff == 0 && WorkRules.CanUpgrade(s, _content, "install") == UpgradeResult.Ok) _pip.Once("canhire");
        if (s.StaffMembers.Any(m => m.Does.Count > 0)) _pip.Once("hired");
        if (StaffRules.CanLeaseDepot(s)) _pip.Once("tolet");
        if (s.StaffMembers.Count > 0 && Clock.Weekday(s.Day) == DayOfWeek.Friday) _pip.Once("payday");
        if (s.Faults.Count > 0) _pip.Once("fault");
        if (s.Lines.Values.Any(l => l.Planted > 0)) _pip.Once("pole");
        if (s.Orders.Count > 0) _pip.Once("catalogue");
        if (_lines.Any(l => LineRules.Complete(s, _planet, l.Premise))) _pip.Once("linedone");

        // the Board: lamps follow tonight as it happens; the tally is last night's
        var ex = BoardRules.Home(s);
        var tonight = BoardRules.Evening(s, s.Day, ex);
        int slot = Mathf.FloorToInt((Hour - BoardRules.FirstHour) * 2);
        string tally = s.LastEvening is { } eve
            ? $"ENGAGED TONES\nlast night: {eve.Engaged}\nthis month: {s.EngagedThisMonth}" + (eve.GaveUp.Count > 0 ? $"\ngave up: {string.Join(", ", eve.GaveUp.Select(id => s.Customers.FirstOrDefault(c => c.Id == id)?.Name).Where(n => n is not null).Take(3))}" : "")
            : "ENGAGED TONES\nnone yet";
        _rack.Show(ex.LinesIn, slot >= 0 && slot < BoardRules.Slots ? tonight.InUse[slot] : 0, tally);
        if (s.LastEvening is { GaveUp.Count: > 0 } && step >= OnboardingStep.Free)
        {
            _pip.Once("engaged");
            if (!BoardRules.CanAddLine(s) && s.LineCards == 0) _pip.Once("cap");
        }
        if (ActRules.EarlyStartEntry(s, _content) is { } page && s.Archive >= page.ArchiveCost) _pip.Once("almanac");
        if (s.HeadStartDays.Count > 0) _pip.Once("headstart");

        // the Board's terminal turns up with the first hire
        bool terminal = BoardService.TerminalOpen(s);
        _terminal.Visible = terminal;
        _terminalUse.CollisionLayer = terminal ? 1u : 0u;
        if (terminal) _pip.Once("terminal");
        if (BoardService.MailOpen(s) && !BoardService.Delegated(s, _content)) { _pip.Once("mail"); if (s.MailMissed >= 1) _pip.Once("nomail"); }
        if (s.BoardWarned) _pip.Once("solicitor");
        if (terminal && !BoardService.Delegated(s, _content) && WorkRules.CanUpgrade(s, _content, BoardService.WorkId) == UpgradeResult.Ok) _pip.Once("tobi");

        // the pager: beeps once for each new thing that needs you
        if (Attention.HasPager(s))
        {
            _pip.Once("pager");
            var now = Attention.Needs(s, _content);
            if (_paged is not null)
                foreach (var need in now.Where(n => !_paged.Contains(n.Key))) _pip.Say("Pager: " + need.Text);
            _paged = now.Select(n => n.Key).ToHashSet();
        }
        if (_roadBarrier is not null)
        {
            bool open = Onboarding.RoadOpen(s, _content);
            if (open) { _roadBarrier.QueueFree(); _roadBarrier = null; }
            else if ((_roadBarrier.GlobalPosition - (_van.Driven ? _van.GlobalPosition : _player.GlobalPosition)).Length() < 25) _pip.Once("closed");
        }
    }

    /// <summary>How long the thing this door currently offers takes to do by hand (zero for a knock or a chat).</summary>
    private float LotHold(LotDef lot)
    {
        var s = Sim.State;
        if (TownRules.FaultAt(s, lot.Id) is not null) return Hold(TownRules.RepairMinutes);
        if (TownRules.JobAt(s, lot.Id) is { } job && _town.Lots.FirstOrDefault(l => l.Lot.Id == lot.Id) is { Install: null }) return Hold(job.Hours * 60);
        return 0;
    }

    private void WireLot(LotView view)
    {
        view.Door.HoldSeconds = () => LotAction(view.Lot).Use is null ? 0f : LotHold(view.Lot);
        view.Door.Prompt = () => LotAction(view.Lot).Prompt;
        view.Door.CanUse = () => LotAction(view.Lot).Use is not null;
        view.Door.OnUse = () => LotAction(view.Lot).Use?.Invoke();
        if (view.Counter is { } counter)
        {
            bool Shut() => !_ignoreHours && HomeRules.ShopShut(Sim.State, _content, view.Lot);
            counter.Prompt = () => Shut() ? $"The counter is {HomeRules.WhyNot(Sim.State, _content, view.Lot)}" : Sim.State.Cash >= TownRules.ModemPrice
                ? $"Buy a modem at the counter: ${TownRules.ModemPrice} (you have {Sim.State.Modems})"
                : $"A modem is ${TownRules.ModemPrice}. You have ${Sim.State.Cash}";
            counter.CanUse = () => !Shut() && Sim.State.Cash >= TownRules.ModemPrice;
            counter.OnUse = () => { if (!Shut()) TownRules.BuyModem(Sim.State); };
            // a second thing on Sol's counter, once the Board has started filling up
            counter.AltPrompt = () => !Shut() && Onboarding.DeskOpen(Sim.State, _content) && Sim.State.LastEvening is { Engaged: > 0 } && BoardRules.CanAddLine(Sim.State) && Sim.State.Cash >= BoardRules.CardPrice
                ? $"Buy a line card for the Board: ${BoardRules.CardPrice}" : null;
            counter.OnAlt = () => { if (!Shut() && BoardRules.BuyCard(Sim.State)) { _cargo.Refresh(); _pip.Once("card"); } };
        }
    }

    private void EnterVan()
    {
        _player.SetActive(false);
        _van.SetDriven(true);
        _pip.Once("van");
    }

    private void ExitVan()
    {
        _van.SetDriven(false);
        _player.SetActive(true);
        _player.PlaceAt(_van.GlobalPosition + _van.GlobalBasis.X * -2.4f + _van.Up * 0.6f, _van.Forward);
    }

    // ---- loop --------------------------------------------------------------------------------
    public override void _Process(double delta)
    {
        if (!AnyScreenOpen)
        {
            // time passes only as it is lived: real seconds become game minutes, and nothing else moves the clock
            _minuteFrac += (float)delta * Clock.MinutesPerDay / DayLengthSeconds;
            int whole = (int)_minuteFrac;
            if (whole > 0) { _minuteFrac -= whole; Sim.AdvanceMinutes(whole); }
        }
        _sky.SetHour(Hour);
        _hud.SetCrosshair(!AnyScreenOpen && !_van.Driven);
        _hud.SetReading(AnyScreenOpen);

        var (next, targetLot) = Guidance.Next(Sim.State, _content);
        SphereBody who = _van.Driven ? _van : _player;

        _slowTick -= (float)delta;
        if (_slowTick <= 0)
        {
            _slowTick = 0.5f;
            foreach (var lot in _town.Lots)
            {
                bool job = TownRules.JobAt(Sim.State, lot.Lot.Id) is not null || TownRules.FaultAt(Sim.State, lot.Lot.Id) is not null;
                bool target = lot.Lot.Id == targetLot;
                lot.Marker.Visible = job || target;
                lot.Marker.Text = target ? "▼" : TownRules.FaultAt(Sim.State, lot.Lot.Id) is not null ? "!" : "$";
                lot.Marker.Modulate = target ? new Color(1f, 0.85f, 0.2f) : new Color(0.6f, 0.95f, 0.6f);
            }
            if (!AnyScreenOpen) RefreshIdleScreens();
            SyncHouses(_player.GlobalPosition);
            SyncPaper();
            // who is in: warm windows after dark, and the car on the drive
            foreach (var lot in _town.Lots)
            {
                if (lot.Lights is not null) lot.Lights.Visible = Clock.Dark(Sim.State) && HomeRules.IsHome(Sim.State, _content, lot.Lot);
                if (lot.Car is not null) lot.Car.Visible = !HomeRules.OutAtWork(Sim.State, _content, lot.Lot);
            }
            _cargo.Refresh();
            foreach (var refresh in _lineRefresh) refresh();
            WatchProgress();
        }

        if (targetLot is not null && _town.Lots.FirstOrDefault(l => l.Lot.Id == targetLot) is { } t)
            next += $"  ({(t.Position - who.GlobalPosition).Length():0} m)";

        var biome = _planet.BiomeAt(PlanetBuilder.ToSim(who.GlobalPosition));
        string where = $"{biome}  alt {who.Altitude:0} m" + (_van.Driven ? $"  {Mathf.Abs(_van.Speed) * 3.6f:0} km/h{(_van.OnRoad ? "" : " (off-road)")}" : _player.Flying ? "  SURVEY FLIGHT" : "");
        string? prompt = AnyScreenOpen ? null
            : _van.Driven ? "[E] Get out   [W/S] drive   [A/D] steer"
            : _player.Focus is { } f ? (!f.CanUse() ? "" : f.HoldSeconds() > 0 ? "[Hold E] " : "[E] ") + f.Prompt() + (f.AltPrompt() is { } alt ? $"   [R] {alt}" : "")
            : _cargo.Held is { } held ? $"Carrying the {held.Label}   [G] put it down" : null;
        _hud.Refresh(Sim, prompt, Hour, where, AnyScreenOpen ? "" : "▶ " + next);
        _hud.SetHold(_van.Driven ? 0 : _player.HoldProgress);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (AnyScreenOpen) return;
        if (_van.Driven && e.IsActionPressed("interact")) { ExitVan(); GetViewport().SetInputAsHandled(); }
        else if (e.IsActionPressed("drop") && !_van.Driven) _cargo.Drop();
        else if (e.IsActionPressed("god_help")) _pip.Say("God keys. F2: $500. F3: next day. F4: finish what the game is asking for. F6: jump to the next act.");
        else if (e.IsActionPressed("god_cash")) God(Sim.GodCash(500));
        else if (e.IsActionPressed("god_day")) { Sim.Sleep(); _minuteFrac = 0; God($"it is {Sim.State.Date}."); }
        else if (e.IsActionPressed("god_stage")) God(Sim.GodNextStage(_planet));
        else if (e.IsActionPressed("god_act")) God(Sim.GodNextAct());
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

    // ---- automation hooks for checks / screenshots -------------------------------------------
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }

    /// <summary>Sign up neighbours and fit their sockets, as a player would over a couple of days.</summary>
    private void QuickCustomers(int count)
    {
        Sim.State.Modems += count;
        for (int i = 0; i < count; i++) if (Sim.TakeEnquiry() is { } c) TownRules.FitSocket(Sim.State, c.Id);
    }

    private async Task RunSmoke()
    {
        // --- world: the player stands on the room floor, the van drives down Main Street ---
        async Task Physics(int frames) { for (int i = 0; i < frames; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
        _ignoreHours = true;   // these checks are about the work, not the time of day (HOURS below checks that)
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
        bool world = standing && drove > 50 && Mathf.Abs(vanAlt) < 3 && _player.IsOnFloor() && _planet.Roads.Count > 0;
        GD.Print($"WORLD {(world ? "OK" : "FAIL")}: standing={standing} vanDrove={drove:0}m vanAlt={vanAlt:0.0} roads={_planet.Roads.Count} lots={_town.Lots.Count}");

        // --- town: every door offers something; an odd job pays ---
        bool doors = _town.Lots.All(l => !string.IsNullOrWhiteSpace(l.Door.Prompt()));
        long cash = Sim.State.Cash;
        bool quietStart = Sim.State.Jobs.Count == 0 && Sim.State.MessagesWaiting == 1 && Sim.State.Log.Count == 1 && _town.Lots.All(l => !l.Door.CanUse());
        Sim.OfferJob();
        var jobLot = _town.Lots.First(l => TownRules.JobAt(Sim.State, l.Lot.Id) is not null);
        jobLot.Door.OnUse!();
        bool handsOn = true;
        if (_oddJobs.TryGetValue(jobLot.Lot.Id, out var oj))
        {
            bool needsToolbox = !oj.Bench.CanUse();
            _kitEverywhere = true;
            for (int i = 0; i < 12 && _oddJobs.ContainsKey(jobLot.Lot.Id); i++) oj.Bench.OnUse!();
            _kitEverywhere = false;
            handsOn = needsToolbox && jobLot.DoorOpen && !_oddJobs.ContainsKey(jobLot.Lot.Id);
            GD.Print($"ODDJOB {(handsOn ? "OK" : "FAIL")}: needsToolbox={needsToolbox} doorOpened={jobLot.DoorOpen}");
        }
        bool paid = quietStart && handsOn && Sim.State.Cash > cash;

        // --- house install: sign someone up, get let in, walk through the door, do every step by hand ---
        Customer? first = null;
        for (int i = 0; i < 10 && (first is null || _town.Lots.First(l => l.Lot.Id == first.LotId).Install is null); i++) { Sim.State.MessagesWaiting = 1; first = Sim.TakeEnquiry(); } // until it is someone in a house
        var home = _town.Lots.First(l => l.Lot.Id == first!.LotId);
        bool house = false;
        if (home.Install is { } site)
        {
            home.Door.OnUse!();
            _player.PlaceAt(home.Body.ToGlobal(new Vector3(0, 0.4f, 5.6f)), -home.Body.GlobalBasis.Z);
            await Physics(20);
            Input.ActionPress("move_forward");
            await Physics(110);
            Input.ActionRelease("move_forward");
            var inside = home.Body.ToLocal(_player.GlobalPosition);
            bool walkedIn = inside.Z < 3.0f && Mathf.Abs(inside.X) < 3.5f && _player.IsOnFloor();
            // carry the kit: pick each thing off the van, put it down in the house; nothing works until it is there
            site.WorkOrder.First().OnUse!();
            bool blocked = _installs[home.Lot.Id].Progress(InstallStep.DropWire) == 0;
            var ladder = _cargo.Items.First(i => i.Tool == Tools.Ladder);
            ladder.OnUse!();
            bool slowed = _cargo.Held == ladder && _player.Pace < 1f;
            foreach (var item in _cargo.Items) { if (_cargo.Held is null) item.OnUse!(); _cargo.Drop(); }
            bool carried = blocked && slowed && _cargo.Held is null && KitOnSite() == Tools.All;
            GD.Print($"CARRY {(carried ? "OK" : "FAIL")}: blocked={blocked} slowed={slowed} onSite={KitOnSite()}");
            foreach (var spot in site.WorkOrder.ToList()) spot.OnUse!();
            if (_installs.ContainsKey(home.Lot.Id)) foreach (var spot in site.WorkOrder.ToList()) spot.OnUse!(); // the test found a fault: redo and sign off
            await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
            bool stayedOpen = home.DoorOpen;                       // job signed, but you and your kit are still inside
            foreach (var item in _cargo.Items) _cargo.Stow(item);
            _player.GlobalTransform = _town.PlayerSpawn;
            await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
            bool closedBehind = !home.DoorOpen;
            _player.GlobalPosition = home.Body.ToGlobal(new Vector3(0, 0.4f, 0));   // back inside a closed, finished house
            await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
            bool neverTrapped = home.DoorOpen;
            _player.GlobalTransform = _town.PlayerSpawn;
            await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
            closedBehind &= neverTrapped && !home.DoorOpen;
            GD.Print($"DOOR {(stayedOpen && closedBehind ? "OK" : "FAIL")}: stayedOpenWhileInside={stayedOpen} closedAfterLeaving={closedBehind}");
            house = stayedOpen && closedBehind && carried && walkedIn && first!.SocketFitted && site.Fitted && Sim.State.Defects.All(d => d.Source != "install");
            GD.Print($"HOUSE {(house ? "OK" : "FAIL")}: walkedIn={walkedIn} at {inside} fitted={first.SocketFitted}");
            _player.GlobalTransform = _town.PlayerSpawn;
            await Physics(30);
        }

        // --- install: sockets, then the frame on its in-world screen ---
        QuickCustomers(3);
        UseFrame();
        var job = _frame.Job!;

        // --- a real mouse click on the in-world frame board must reach the right button ---
        await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
        var btn = _frame.FirstLineButton!;
        var at = _frameScreen.DisplayPointFor(btn.GetGlobalRect().GetCenter(), _player.Camera);
        foreach (bool down in new[] { true, false })
        {
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = down, Position = at, GlobalPosition = at }, true);
            await Frames(3);
        }
        bool clicked = _frame.SelectedLine == 0;
        GD.Print($"SCREEN {(clicked ? "OK" : "FAIL")}: clicked display point {at} -> selected line {_frame.SelectedLine?.ToString() ?? "none"}");

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
        // --- the first automation stage: hire the technician, they connect someone without you, a fault needs a repair visit ---
        bool roadShut = _roadBarrier is not null;
        Sim.State.Cash += 1000; Sim.State.Modems += 3;
        bool hired = WorkRules.Upgrade(Sim.State, _content, "install") == UpgradeResult.Ok;
        Customer? next = null;
        for (int i = 0; i < 6 && next is null; i++) { Sim.State.MessagesWaiting = 1; next = Sim.TakeEnquiry(); }
        // paper and people: nothing happens until the ticket is taken off the rail and put in Jory's hand
        Sim.AdvanceDays(2);
        bool waited = next is { Installed: false };
        SyncPaper();
        var slip = StaffRules.Tickets(Sim.State, _content).First(t => t.Key == $"install:{next!.Id}");
        bool onRail = _rail.CanUse(slip) && _figures.ContainsKey("install") && !_figures["install"].Hot.CanUse();
        _rail.Use(slip);
        _figures["install"].Hot.OnUse!();
        bool handed = Sim.State.Assigned.ContainsKey(slip.Key) && _clipboard.Count == 0;
        for (int i = 0; i < 4 && !next.Installed; i++) Sim.AdvanceDay();
        bool techDid = waited && onRail && handed && next is { Installed: true } && Sim.State.TechInstalls > 0;
        GD.Print($"TICKETS {(techDid ? "OK" : "FAIL")}: waitedWithoutTicket={waited} onRail={onRail} handedOver={handed} done={next.Installed}");

        // people keep hours: on a weekday morning the family's car is gone and nobody answers; in the evening the windows are lit
        _ignoreHours = false;
        while (Clock.Weekday(Sim.State.Day) != DayOfWeek.Tuesday) Sim.AdvanceDay();
        var famLot = _town.Lots.First(l => l.Lot.Archetype == "family" && l.Car is not null);
        Sim.State.Minute = 5 * 60; _slowTick = 0; await Frames(3);
        bool outByDay = !famLot.Car!.Visible && !famLot.Door.CanUse() && famLot.Door.Prompt().Contains("car");
        Sim.State.Minute = 13 * 60 + 30; _slowTick = 0; await Frames(3);
        bool inByNight = famLot.Car.Visible && famLot.Lights!.Visible;
        _ignoreHours = true;
        bool hours = outByDay && inByNight;
        GD.Print($"HOURS {(hours ? "OK" : "FAIL")}: outAtEleven={outByDay} homeAndLitAtHalfSeven={inByNight}");

        // the depot: lease 1 Main Street, and from the next working morning tickets are collected without you
        Sim.State.Cash += 500;
        var depotLot = _town.Lots.First(l => l.Lot.Kind == LotKind.Depot);
        depotLot.Door.OnUse!();
        Customer? later = null;
        for (int i = 0; i < 8 && later is null; i++) { Sim.State.MessagesWaiting = 1; later = Sim.TakeEnquiry(); }
        for (int i = 0; i < 5 && later is { Installed: false }; i++) Sim.AdvanceDay();
        bool depot = Sim.State.HasDepot && later is { Installed: true };
        GD.Print($"DEPOT {(depot ? "OK" : "FAIL")}: leased={Sim.State.HasDepot} collectedAndDone={later?.Installed}");
        techDid &= hours && depot;
        Sim.State.Defects.Add(new LatentDefect { Id = "defect-smoke", SurfaceDay = Sim.State.Day + 1, Source = "technician", CustomerId = next!.Id, Text = "a loose terminal" });
        Sim.AdvanceDay();
        var faultLot = _town.Lots.First(l => l.Lot.Id == next.LotId);
        bool needsKit = TownRules.FaultAt(Sim.State, faultLot.Lot.Id) is not null && !faultLot.Door.CanUse();
        _kitEverywhere = true;
        faultLot.Door.OnUse!();
        _kitEverywhere = false;
        await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
        bool roadGate = roadShut && _roadBarrier is null;
        bool auto = hired && techDid && needsKit && roadGate && Sim.State.Faults.Count == 0;
        GD.Print($"AUTOMATION {(auto ? "OK" : "FAIL")}: hired={hired} techDid={techDid} faultNeedsKit={needsKit} roadGate={roadGate} faultsLeft={Sim.State.Faults.Count}");
        // --- new infrastructure: build the whole pole line to the farm, pole by span ---
        Sim.State.Cash += 2000;
        bool lineBuilt = _lines.Count > 0;
        foreach (var (premise, view) in _lines)
        {
            bool needsPole = !view.PlantSpot.CanUse();
            _kitEverywhere = true;
            for (int i = 0; i < 200 && (view.PlantSpot.CanUse() || view.StringSpot.CanUse()); i++)
                if (view.PlantSpot.CanUse()) view.PlantSpot.OnUse!(); else view.StringSpot.OnUse!();
            _kitEverywhere = false;
            bool served = Sim.State.Customers.Any(c => c.PremiseId == premise.Id);
            lineBuilt &= needsPole && served;
            GD.Print($"LINE {(needsPole && served ? "OK" : "FAIL")}: {premise.Name} poles={view.Count} needsPole={needsPole} served={served}");
        }
        // --- catalogue: a subscriber orders something; it is fitted by hand and their bill goes up ---
        bool catalogue = false;
        if (Sim.OfferOrder() is { } ord && Sim.State.Customers.First(c => c.Id == ord.CustomerId) is { } buyer && _town.Lots.First(l => l.Lot.Id == buyer.LotId) is { } shopper)
        {
            long feeBefore = buyer.MonthlyFee, cashBefore = Sim.State.Cash;
            shopper.Door.OnUse!();
            _kitEverywhere = true;
            for (int i = 0; i < 14 && _oddJobs.TryGetValue(shopper.Lot.Id, out var fit); i++) fit.Bench.OnUse!();
            _kitEverywhere = false;
            var bought = _content.Products[ord.ProductId];
            catalogue = buyer.Products.Contains(bought.Id) && buyer.MonthlyFee == feeBefore + bought.MonthlyExtra && Sim.State.Cash == cashBefore + bought.Price - bought.Cost;
            GD.Print($"CATALOGUE {(catalogue ? "OK" : "FAIL")}: {buyer.Name} bought {bought.Name}, fee {feeBefore}->{buyer.MonthlyFee}");
        }
        // --- carry: the whole town rings the Board on two lines; a line card is bought, carried and fitted ---
        Sim.GodNextStage(_planet);                                   // connect everyone
        foreach (var w in BoardService.Waiting(Sim.State)) w.HasAccount = true;
        Sim.AdvanceDay();
        for (int i = 0; i < 14 && (Sim.State.LastEvening?.Engaged ?? 0) == 0; i++) Sim.AdvanceDay();   // some evenings are quiet; a fortnight never is
        int engagedBefore = Sim.State.LastEvening?.Engaged ?? 0;
        Sim.State.Cash += 500;
        var solCounter = _town.Lots.First(l => l.Counter is not null).Counter!;
        bool offered = solCounter.AltPrompt() is not null;
        solCounter.OnAlt!();
        bool needsCard = Sim.State.LineCards == 1 && !_rack.Slot.CanUse();
        _kitEverywhere = true; _rack.Slot.OnUse!(); _kitEverywhere = false;
        bool board = engagedBefore > 0 && offered && needsCard && BoardRules.Home(Sim.State).LinesIn == 3 && Sim.State.LineCards == 0;
        GD.Print($"BOARD {(board ? "OK" : "FAIL")}: engagedOnTwoLines={engagedBefore} cardOffered={offered} needsCarrying={needsCard} linesNow={BoardRules.Home(Sim.State).LinesIn}");
        // --- the Board's terminal: let a new caller in with the number keys; in act one, run the night mail ---
        bool terminalShown = _terminal.Visible;
        foreach (var w in BoardService.Waiting(Sim.State)) w.HasAccount = true;
        Sim.State.Customers.First(c => c.Live && c.Installed).HasAccount = false;
        _board.Open(InterfaceTier.Terminal);
        _board.Press(1); _board.Press(1);
        bool letIn = BoardService.Waiting(Sim.State).Count == 0;
        Sim.GodNextAct();
        _board.Rebuild();
        _board.Press(3);
        bool mailed = BoardService.MailRunToday(Sim.State) && Sim.State.MailBillCents > 0;
        _board.Press(0);
        bool sysop = terminalShown && letIn && mailed && !_board.IsOpen;
        GD.Print($"TERMINAL {(sysop ? "OK" : "FAIL")}: shown={terminalShown} letIn={letIn} mailed={mailed} closed={!_board.IsOpen}");
        Sim.AdvanceDays(700);
        var reloaded = new Simulation(_content, SaveCodec.FromJson(SaveCodec.ToJson(Sim.State)));
        bool ok = world && doors && paid && house && auto && lineBuilt && catalogue && board && sysop && clicked && outcome.Perfect && reloaded.State.Day == Sim.State.Day && Sim.State.ActIndex >= 1;
        GD.Print($"SMOKE {(ok ? "OK" : "FAIL")}: doors={doors} paid={paid} perfect={outcome.Perfect} {Sim.State.Date} act={Sim.State.ActIndex} subs={Sim.State.LiveSubscribers} cash={Sim.State.Cash}");
        GetTree().Quit(ok ? 0 : 1);
    }

    /// <summary>
    /// A MOCK-UP for the owner, not part of the game: what the frame could look like as a real switchboard with wires and lamps
    /// (docs/DESK_AND_DAY_DESIGN.md). Built only for this screenshot, then thrown away.
    /// </summary>
    private async Task ConceptSwitchboard(Camera3D cam, string path)
    {
        _board.Close();
        await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
        var mock = new Node3D { Position = new Vector3(-1.0f, 1.35f, -RoomBuilder.D / 2 + 0.55f) };
        _town.Hq.AddChild(mock);
        var lp = new LowPoly();
        var ply = new Color(0.72f, 0.58f, 0.4f); var brass = new Color(0.8f, 0.66f, 0.3f); var paper = new Color(0.95f, 0.93f, 0.85f);
        lp.Box(Vector3.Zero, new Vector3(1.9f, 1.3f, 0.04f), ply).Box(new Vector3(0, -0.56f, 0.09f), new Vector3(1.9f, 0.04f, 0.2f), ply * 0.85f);   // board and shelf
        void Text3(string t, Vector3 at, float size, Color col) => mock.AddChild(new Label3D { Text = t, Position = at, FontSize = 48, PixelSize = size / 48f, Modulate = col, OutlineSize = 0 });
        Text3("SUBSCRIBERS", new Vector3(-0.62f, 0.53f, 0.03f), 0.05f, new Color(0.15f, 0.1f, 0.05f));
        Text3("BOARD PORTS", new Vector3(0.5f, 0.53f, 0.03f), 0.05f, new Color(0.15f, 0.1f, 0.05f));
        Text3("LINES IN", new Vector3(0.0f, 0.6f, 0.03f), 0.04f, new Color(0.15f, 0.1f, 0.05f));
        string[] names = { "D. Brennan", "C. Achebe", "Lindqvist", "T. Okafor", "H. Moss", "▒▒▒▒▒▒", "P. Duarte", "▒▒▒▒" };
        var wireCols = new[] { new Color(0.8f, 0.15f, 0.12f), new Color(0.15f, 0.3f, 0.8f), new Color(0.9f, 0.8f, 0.2f), new Color(0.15f, 0.6f, 0.25f), new Color(0.9f, 0.9f, 0.88f) };
        int[] goesTo = { 2, 0, 4, 1, -1, -1, 6, -1 };   // which port each subscriber is jumpered to (-1: not yet)
        var lit = new HashSet<int> { 0, 2, 6 };
        for (int i = 0; i < 8; i++)
        {
            float y = 0.42f - i * 0.115f;
            lp.Box(new Vector3(-0.92f + 0.17f, y, 0.025f), new Vector3(0.34f, 0.075f, 0.01f), paper);                // name tag
            Text3(names[i], new Vector3(-0.75f, y, 0.035f), 0.042f, new Color(0.12f, 0.1f, 0.3f));
            lp.Box(new Vector3(-0.5f, y, 0.035f), new Vector3(0.05f, 0.05f, 0.03f), brass);                          // subscriber terminal
            lp.Box(new Vector3(0.4f, y, 0.035f), new Vector3(0.05f, 0.05f, 0.03f), brass);                           // port jack
            Text3($"{i + 1}", new Vector3(0.5f, y, 0.03f), 0.045f, new Color(0.15f, 0.1f, 0.05f));
            mock.AddChild(new MeshInstance3D
            {
                Position = new Vector3(0.62f, y, 0.04f), Mesh = new BoxMesh { Size = new Vector3(0.05f, 0.05f, 0.03f) },
                MaterialOverride = lit.Contains(i) ? new StandardMaterial3D { AlbedoColor = new Color(1f, 0.75f, 0.2f), EmissionEnabled = true, Emission = new Color(1f, 0.7f, 0.15f), EmissionEnergyMultiplier = 2 }
                    : new StandardMaterial3D { AlbedoColor = i == 3 ? new Color(0.5f, 0.08f, 0.06f) : new Color(0.1f, 0.25f, 0.12f) },
            });
        }
        // jumper wires: each hangs in a shallow curve between its two terminals
        void Jumper(Vector3 from, Vector3 to, Color col)
        {
            var prev = from;
            for (int k = 1; k <= 8; k++)
            {
                float t = k / 8f, sag = Mathf.Sin(t * Mathf.Pi);
                var p = from.Lerp(to, t) + new Vector3(0, -0.07f * sag, 0.08f * sag);
                var d = p - prev; var z = d.Normalized(); var x = Vector3.Up.Cross(z).Normalized();
                var seg = new LowPoly().Box(Vector3.Zero, new Vector3(0.012f, 0.012f, d.Length() + 0.004f), col).Instance();
                seg.Transform = new Transform3D(new Basis(x, z.Cross(x), z), (p + prev) / 2);
                mock.AddChild(seg);
                prev = p;
            }
        }
        for (int i = 0; i < 8; i++)
            if (goesTo[i] >= 0) Jumper(new Vector3(-0.5f, 0.42f - i * 0.115f, 0.05f), new Vector3(0.4f, 0.42f - goesTo[i] * 0.115f, 0.05f), i == 6 ? new Color(0.35f, 0.33f, 0.3f) : wireCols[i % wireCols.Length]);
        lp.Box(new Vector3(-0.05f, 0.42f - 0.115f * 0.5f, 0.11f), new Vector3(0.2f, 0.06f, 0.005f), new Color(0.85f, 0.15f, 0.1f));
        Text3("PARTY LINE: LEAVE", new Vector3(-0.05f, 0.42f - 0.115f * 0.5f, 0.115f), 0.028f, Colors.White);
        for (int i = 0; i < 4; i++)                                                                                   // lines in: two fitted and busy, two empty sockets
            mock.AddChild(new MeshInstance3D
            {
                Position = new Vector3(-0.15f + i * 0.1f, 0.53f, 0.04f), Mesh = new BoxMesh { Size = new Vector3(0.05f, 0.05f, 0.03f) },
                MaterialOverride = i < 2 ? new StandardMaterial3D { AlbedoColor = new Color(1f, 0.75f, 0.2f), EmissionEnabled = true, Emission = new Color(1f, 0.7f, 0.15f), EmissionEnergyMultiplier = 2 } : new StandardMaterial3D { AlbedoColor = new Color(0.06f, 0.06f, 0.06f) },
            });
        // the shelf: tone probe, test handset, spare jumpers on a hook, the test switch
        lp.Box(new Vector3(-0.7f, -0.5f, 0.1f), new Vector3(0.26f, 0.05f, 0.05f), new Color(0.95f, 0.8f, 0.15f)).Box(new Vector3(-0.55f, -0.5f, 0.1f), new Vector3(0.05f, 0.02f, 0.02f), new Color(0.7f, 0.7f, 0.72f));
        lp.Box(new Vector3(-0.3f, -0.49f, 0.1f), new Vector3(0.24f, 0.07f, 0.07f), new Color(0.1f, 0.1f, 0.11f));
        for (int i = 0; i < 5; i++) lp.Box(new Vector3(0.1f + i * 0.035f, -0.36f, 0.04f), new Vector3(0.012f, 0.3f, 0.012f), wireCols[i]);
        lp.Box(new Vector3(0.17f, -0.2f, 0.04f), new Vector3(0.22f, 0.02f, 0.03f), brass);
        lp.Box(new Vector3(0.7f, -0.42f, 0.05f), new Vector3(0.16f, 0.2f, 0.03f), new Color(0.15f, 0.15f, 0.16f)).Box(new Vector3(0.7f, -0.36f, 0.12f), new Vector3(0.03f, 0.16f, 0.1f), brass);
        Text3("probe", new Vector3(-0.68f, -0.43f, 0.1f), 0.03f, paper); Text3("test set", new Vector3(-0.3f, -0.42f, 0.1f), 0.03f, paper);
        Text3("jumpers", new Vector3(0.17f, -0.15f, 0.06f), 0.03f, new Color(0.15f, 0.1f, 0.05f)); Text3("TEST", new Vector3(0.7f, -0.28f, 0.07f), 0.035f, paper);
        mock.AddChild(lp.Instance());
        mock.AddChild(new OmniLight3D { Position = new Vector3(0, 0.3f, 1.2f), LightEnergy = 0.8f, OmniRange = 4 });

        _player.Camera.Current = false; cam.Current = true;
        cam.GlobalTransform = mock.GlobalTransform * new Transform3D(new Basis(Vector3.Up, 0.18f), new Vector3(0.38f, -0.02f, 1.75f));
        _hud.Visible = false;
        await Frames(14);
        GetViewport().GetTexture().GetImage().SavePng(path.Replace(".png", "-concept-switchboard.png"));
        _hud.Visible = true;
        mock.QueueFree();
    }

    private async Task TakeScreenshot(string path)
    {
        Input.MouseMode = Input.MouseModeEnum.Visible; // stray mouse motion must not move the camera
        _ignoreHours = true;
        _dayClock = DayLengthSeconds * 0.2f;            // mid-morning light
        var cam = new Camera3D { Near = 0.1f, Far = 60000, Fov = 65 };
        AddChild(cam);
        cam.Current = true;
        async Task Shot(string suffix, Vector3 from, Vector3 lookAt, Vector3? camUp = null)
        {
            cam.LookAtFromPosition(from, lookAt, camUp ?? from.Normalized());
            await Frames(12);
            GetViewport().GetTexture().GetImage().SavePng(path.Replace(".png", suffix + ".png"));
        }
        await Frames(30);
        var hq = _town.Centre; var up = hq.Normalized();
        await Shot("-town", TownBuilder.Place(_planet, 22, 40, 0, 7).Origin, hq + up * 2);
        await Shot("-shop", TownBuilder.Place(_planet, 66, 20, 0, 2.2f).Origin, TownBuilder.Place(_planet, 72, 1, 0, 2).Origin);
        await Shot("-aerial", TownBuilder.Place(_planet, 250, 500, 0, 420).Origin, hq);
        if (_planet.SiteById("farm-hollow") is { } farm)
        {
            var f = TownBuilder.Place(_planet, farm, 0, 0).Origin;
            await Shot("-farm", TownBuilder.Place(_planet, farm, -25, 45, 0, 9).Origin, f + f.Normalized() * 2);
            await Shot("-road", ((hq + f) / 2).Normalized() * (hq.Length() + 260), (hq + f) / 2, (f - hq).Normalized());
        }
        // a house part-way through an install: outside wall, then indoors
        Customer? shown = null;
        for (int i = 0; i < 10 && (shown is null || _town.Lots.First(l => l.Lot.Id == shown.LotId).Install is null); i++) { Sim.State.MessagesWaiting = 1; shown = Sim.TakeEnquiry(); }
        if (shown is not null && _town.Lots.First(l => l.Lot.Id == shown.LotId) is { Install: { } site } home)
        {
            if (_roadBarrier is not null) await Shot("-barrier", _roadBarrier.ToGlobal(new Vector3(5, 2.2f, -14)), _roadBarrier.ToGlobal(new Vector3(0, 1.2f, 0)));
            await Shot("-van", _van.ToGlobal(new Vector3(2.6f, 2.0f, 5.2f)), _van.ToGlobal(new Vector3(0, 1.2f, 1.6f)));
            home.Door.OnUse!();
            var order = site.WorkOrder.ToList();
            Vector3 L(float x, float y, float z) => home.Body.ToGlobal(new Vector3(x, y, z));
            // the kit, carried over and put down by the wall
            _player.PlaceAt(L(-2.2f, 0.3f, 5.2f), -home.Body.GlobalBasis.Z);
            foreach (var item in _cargo.Items.Where(i => i.Tool != Tools.ModemBox))
            {
                _player.PlaceAt(L(-3.4f + 0.7f * _cargo.Items.IndexOf(item), 0.05f, 4.9f), -home.Body.GlobalBasis.Z);
                item.OnUse!(); _cargo.Drop();
            }
            _kitEverywhere = true;
            order[0].OnUse!(); order[1].OnUse!();
            await Shot("-install", L(-1.2f, 1.7f, 7.5f), L(-2.6f, 1.6f, 3.5f));
            for (int i = 2; i < 8; i++) order[i].OnUse!();
            await Shot("-house", L(2.6f, 1.7f, 2.6f), L(-3.2f, 0.7f, 0.6f));
            await Shot("-house2", L(-2.8f, 1.7f, 2.4f), L(2.5f, 0.9f, -2.2f));
        }
        // an odd job being done by hand on someone's rug
        OddJob? odd = null;
        for (int i = 0; i < 12 && (odd is null || _town.Lots.First(l => l.Lot.Id == odd.LotId).Install is null || _installs.ContainsKey(odd.LotId)); i++) odd = Sim.OfferJob();
        if (odd is not null && _town.Lots.First(l => l.Lot.Id == odd.LotId) is { Install: not null } jv)
        {
            jv.Door.OnUse!();
            if (_oddJobs.TryGetValue(jv.Lot.Id, out var doing)) { doing.Bench.OnUse!(); doing.Bench.OnUse!(); }
            await Shot("-oddjob", jv.Body.ToGlobal(new Vector3(-0.6f, 1.5f, 1.6f)), jv.Body.ToGlobal(new Vector3(1.5f, 0.3f, -0.9f)));
        }
        // the pole line to the farm, part built
        if (_lines.Count > 0)
        {
            var (_, line) = _lines[0];
            _kitEverywhere = true;
            Sim.State.Cash += 500;
            for (int i = 0; i < 6; i++) line.PlantSpot.OnUse!();
            for (int i = 0; i < 4; i++) line.StringSpot.OnUse!();
            Vector3 a = line.PolePosition(1), b = line.PolePosition(5), u = a.Normalized();
            await Shot("-poles", a + u * 4f + (a - b).Normalized() * 14f + (b - a).Cross(u).Normalized() * 9f, b + u * 4f);
        }
        await Shot("-orbit", (up + _town.Hq.GlobalBasis.X * 0.5f + _town.Hq.GlobalBasis.Z * 0.35f).Normalized() * (float)_planet.RadiusM * 3.3f, Vector3.Zero, Vector3.Up);

        // inside: the room with its live screens, then leaning in to each
        cam.Current = false; _player.Camera.Current = true;
        _player.Camera.Rotation = new Vector3(-0.15f, 0, 0);
        QuickCustomers(3);
        RefreshIdleScreens();
        async Task Inside(string suffix) { await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout); GetViewport().GetTexture().GetImage().SavePng(path.Replace(".png", suffix + ".png")); }
        await Inside("-room");
        UseFrame(); await Inside("-frame"); _frame.Close(); await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        _almanac.Open(InterfaceTier.Terminal); await Inside("-pip"); _almanac.Close(); await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        _notebook.Open(InterfaceTier.Paper); await Inside("-notebook"); _notebook.Close();
        // the Board at half past seven with the whole town on two lines
        for (int i = 0; i < 12 && (Sim.State.Staff == 0 || TownRules.Prospects(Sim.State, _content).Any() || !_lines.All(l => LineRules.Complete(Sim.State, _planet, l.Premise))); i++) Sim.GodNextStage(_planet);
        Sim.AdvanceDays(2);
        _dayClock = DayLengthSeconds * (19.6f - 6f) / 24f;
        await ToSignal(GetTree().CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);
        _player.Camera.Current = false; cam.Current = true;
        cam.GlobalTransform = _rack.GlobalTransform * new Transform3D(Basis.Identity, new Vector3(0.1f, 0.05f, 1.15f));
        await Frames(12);
        GetViewport().GetTexture().GetImage().SavePng(path.Replace(".png", "-board.png"));

        // the wall of your room: job tickets on the rail and the calendar with pay day on it
        Sim.State.Modems += 4;
        for (int i = 0; i < 6; i++) { Sim.State.MessagesWaiting = 1; Sim.TakeEnquiry(); }
        while (!Clock.IsWorkingDay(Sim.State.Day) || Clock.Weekday(Sim.State.Day) == DayOfWeek.Friday) Sim.AdvanceDay();
        _dayClock = DayLengthSeconds * (9.5f - 6f) / 24f;
        SyncPaper();
        if (StaffRules.Tickets(Sim.State, _content).FirstOrDefault(t => _rail.CanUse(t)) is { } pick) _rail.Use(pick);
        cam.GlobalTransform = _rail.GlobalTransform * new Transform3D(new Basis(Vector3.Up, 0.12f), new Vector3(0.62f, 0.02f, 1.75f));
        await Frames(12);
        GetViewport().GetTexture().GetImage().SavePng(path.Replace(".png", "-wall.png"));
        // Jory waiting at your door for a ticket
        if (_figures.TryGetValue("install", out var jory))
        {
            cam.LookAtFromPosition(jory.GlobalPosition + jory.GlobalBasis.Z * 3.2f + jory.GlobalBasis.X * 1.4f + jory.GlobalBasis.Y * 1.5f, jory.GlobalPosition + jory.GlobalBasis.Y * 1.1f, jory.GlobalPosition.Normalized());
            await Frames(12);
            GetViewport().GetTexture().GetImage().SavePng(path.Replace(".png", "-jory.png"));
        }
        // Main Street at half past seven on a working day: lit windows and cars show who is in
        _ignoreHours = false;
        _dayClock = DayLengthSeconds * (19.5f - 6f) / 24f; _slowTick = 0;
        await Frames(4);
        await Shot("-evening", TownBuilder.Place(_planet, -62, 22, 0, 5).Origin, TownBuilder.Place(_planet, -20, 14, 0, 2).Origin);
        _dayClock = DayLengthSeconds * (11f - 6f) / 24f; _slowTick = 0;
        await Frames(4);
        await Shot("-midday", TownBuilder.Place(_planet, -62, 22, 0, 5).Origin, TownBuilder.Place(_planet, -20, 14, 0, 2).Origin);
        _ignoreHours = true;
        _dayClock = DayLengthSeconds * (19.6f - 6f) / 24f;
        // the sysop's terminal, with a caller waiting and uploads to look through
        cam.Current = false; _player.Camera.Current = true;
        Sim.GodNextAct(); Sim.AdvanceDays(8);
        Sim.State.Customers.First(c => c.Live && c.Installed).HasAccount = false;
        await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
        _board.Open(InterfaceTier.Terminal); await Inside("-terminal"); _board.Press(2); await Inside("-terminal-files"); _board.Close();
        await ConceptSwitchboard(cam, path);
        GD.Print($"SHOTS OK fps={Engine.GetFramesPerSecond()}");
        GetTree().Quit();
    }
}
