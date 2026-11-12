using Godot;
using Telo.Game.Player;
using Telo.Game.UI;
using Telo.Game.World;
using Telo.Sim.Content;
using Telo.Sim.Minigames.Install;
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
    private WorldScreen _frameScreen = null!, _pipScreen = null!, _notebookScreen = null!;
    private float _dayClock = DayLengthSeconds * 0.1f;
    private float _slowTick;
    private ContentDb _content = null!;
    private PlanetGen _planet = null!;
    private PlanetSky _sky = null!;
    private Town _town = null!;
    private Van _van = null!;
    private Cargo _cargo = null!;
    /// <summary>Automated checks pretend all the kit is to hand.</summary>
    private bool _kitEverywhere;
    private Transform3D _cameraHome;
    private Tween? _lean;
    /// <summary>House installs under way, by lot. Not saved: a half-done job is simply started again.</summary>
    private readonly Dictionary<string, InstallJob> _installs = new();

    private float Hour => (6f + _dayClock / DayLengthSeconds * 24f) % 24f;
    private bool AnyScreenOpen => _frame.IsOpen || _almanac.IsOpen || _notebook.IsOpen;

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
        _town = TownBuilder.Build(this, _planet, _content);
        foreach (var lot in _town.Lots) WireLot(lot);
        _ = BuildRoadsWhenPhysicsIsReady();

        _frame = new FrameView { Sim = Sim };
        _almanac = new AlmanacView { Sim = Sim };
        _notebook = new NotebookView { Sim = Sim };
        BuildRoom(_town.Hq);
        foreach (var v in new ModalView[] { _frame, _almanac, _notebook })
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
        _cargo = new Cargo(_van, _player, this, () => Sim.State.Modems - _installs.Values.Count(j => j.IsDone(InstallStep.Modem)));
        _van.Door.Prompt = () => _cargo.Held is { } h ? $"Put the {h.Label} back on the van" : "Drive the van";
        _van.Door.OnUse = () => { if (_cargo.Held is not null) _cargo.StowHeld(); else EnterVan(); };

        // The only overlay left: a small status strip. (Target: wall calendar, cash tin, pager.)
        var ui = new CanvasLayer();
        AddChild(ui);
        _hud = new Hud(); ui.AddChild(_hud);

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
    }

    // ---- the room: every "menu" is an object -------------------------------------------------
    private void BuildRoom(Node3D room)
    {
        float back = -RoomBuilder.D / 2 + 0.1f;

        // The frame: a backboard on the wall. Its face is the work surface.
        RoomBuilder.Prop(room, "Frame", new Vector3(-1.4f, 1.35f, back), new Vector3(1.6f, 1.1f, 0.08f), RoomBuilder.Board, FramePrompt, UseFrame);
        _frameScreen = Screen(room, _frame, 1.44f, new Vector3(-1.4f, 1.35f, back + 0.05f), Vector3.Zero, glows: false);
        RoomBuilder.Label(room, "FRAME", new Vector3(-1.4f, 2.0f, back + 0.06f), 0.12f);

        // Desk with answering machine, PIP's handset on a stand, and the notebook lying open.
        RoomBuilder.Box(room, new Vector3(1.3f, 0.37f, back + 0.45f), new Vector3(1.7f, 0.74f, 0.8f), RoomBuilder.Wood, solid: true);
        RoomBuilder.Prop(room, "AnsweringMachine", new Vector3(0.72f, 0.8f, back + 0.4f), new Vector3(0.26f, 0.1f, 0.2f), new Color(0.08f, 0.08f, 0.08f),
            () => Sim.State.MessagesWaiting > 0 ? $"Play messages ({Sim.State.MessagesWaiting} waiting)" : "No new messages", PlayMessages);

        RoomBuilder.Prop(room, "Handset", new Vector3(1.3f, 0.86f, back + 0.36f), new Vector3(0.38f, 0.26f, 0.03f), new Color(0.1f, 0.11f, 0.14f),
            () => "Read PIP's Almanac", () => _almanac.Open(InterfaceTier.Terminal)).RotationDegrees = new Vector3(-18, 0, 0);
        _pipScreen = Screen(room, _almanac, 0.34f, new Vector3(1.3f, 0.865f, back + 0.385f), new Vector3(-18, 0, 0), glows: true);

        RoomBuilder.Prop(room, "Notebook", new Vector3(1.85f, 0.745f, back + 0.5f), new Vector3(0.52f, 0.02f, 0.34f), new Color(0.5f, 0.12f, 0.1f),
            () => "Read the company notebook", () => _notebook.Open(WorkRules.InterfaceFor(Sim.State, Sim.Content, "billing")));
        _notebookScreen = Screen(room, _notebook, 0.48f, new Vector3(1.85f, 0.758f, back + 0.5f), new Vector3(-90, 0, 0), glows: false);

        // Camp bed. Sleeping is how days pass quickly.
        RoomBuilder.Prop(room, "Bed", new Vector3(-2.2f, 0.22f, 1.4f), new Vector3(0.9f, 0.44f, 1.9f), RoomBuilder.Fabric,
            () => "Sleep until morning", () => { Sim.AdvanceDay(); _dayClock = DayLengthSeconds * 0.05f; RefreshIdleScreens(); });
    }

    private static WorldScreen Screen(Node3D room, ModalView view, float widthM, Vector3 pos, Vector3 rotDeg, bool glows)
    {
        var s = WorldScreen.Create(view, widthM, glows);
        s.Position = pos; s.RotationDegrees = rotDeg;
        room.AddChild(s);
        return s;
    }

    private WorldScreen ScreenOf(ModalView v) => v == _frame ? _frameScreen : v == _almanac ? _pipScreen : _notebookScreen;

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
        _frame.Start(job);
        _frame.Open(WorkRules.InterfaceFor(Sim.State, Sim.Content, "install"));
    }

    private void PlayMessages()
    {
        if (Sim.State.MessagesWaiting == 0) return;
        Sim.TakeEnquiry();
    }

    /// <summary>What can be done at this building right now? Decided from sim state every time it is asked.</summary>
    private (string Prompt, Action? Use) LotAction(LotDef lot)
    {
        var s = Sim.State;
        // a broken line comes before anything else at this door
        if (TownRules.FaultAt(s, lot.Id) is { } fault && TownRules.CustomerAt(s, lot.Id) is { } broken)
        {
            var missing = TownRules.RepairNeeds & ~KitOnSite();
            return missing == Tools.None
                ? ($"Fix {broken.Name}'s line: {fault.Text} ({TownRules.RepairMinutes} min)", () => { if (TownRules.Repair(s, fault.Id, KitOnSite())) SpendHours(TownRules.RepairMinutes / 60f); })
                : ($"{broken.Name} has a fault: {fault.Text}. You need {Cargo.Names(missing)} from the van", null);
        }
        if (TownRules.JobAt(s, lot.Id) is { } job)
            return ($"{job.Title} for {lot.Resident} (+${job.Pay}, {job.Hours} h)", () => { TownRules.CompleteJob(s, _content, job.Id); SpendHours(job.Hours); });

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
                ? ($"Fit the wall socket for {cust.Name} ({TownRules.SocketHours} h, uses a modem)", () => { TownRules.FitSocket(s, cust.Id); SpendHours(TownRules.SocketHours); })
                : ($"{cust.Name} is waiting for a socket. You have no modems (Sol's Electronics sells them)", null);
        if (cust is not null)
            return (cust.Installed ? $"{lot.Resident}: subscriber, satisfaction {cust.Satisfaction}%" : $"{lot.Resident}: socket fitted. Wire their line at the frame", null);

        if (lot.Premise is not null)
            return ($"{lot.Resident}: \"A line out here? I've asked twice.\" (Promise Ledger, in your notebook)", null);

        var h = TownRules.HouseholdOf(s, lot.Id);
        if (s.Day < h.RetryDay)
            return (h.Status == HouseholdStatus.Declined ? $"{lot.Resident} said no. Try again in {h.RetryDay - s.Day} days" : $"Nobody home at {lot.Resident}'s. Try tomorrow", null);
        return ($"Knock on {lot.Resident}'s door", () => { Sim.Knock(lot.Id); SpendHours(TownRules.KnockMinutes / 60f); });
    }

    // ---- the install at a customer's house: the sim owns the job, the house shows it -------------
    private void StartInstall(LotView view, Customer cust)
    {
        if (Sim.NewInstallJob(cust.Id) is not { } job) return;
        _installs[view.Lot.Id] = job;
        OpenDoor(view, true);
        view.Install!.Begin(job,
            (step, rush) =>
            {
                int before = job.ElapsedMinutes;
                if (step == InstallStep.Test) view.Install.NoteTest(job.Test());
                else if (rush) job.Rush(step, KitOnSite());
                else job.Do(step, KitOnSite());
                SpendHours((job.ElapsedMinutes - before) / 60f);
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
            if (v.DoorOpen && !_installs.ContainsKey(v.Lot.Id) && (v.Position - playerPos).Length() > 6.5f) OpenDoor(v, false);
        }
    }

    private void WireLot(LotView view)
    {
        view.Door.Prompt = () => LotAction(view.Lot).Prompt;
        view.Door.CanUse = () => LotAction(view.Lot).Use is not null;
        view.Door.OnUse = () => LotAction(view.Lot).Use?.Invoke();
        if (view.Counter is { } counter)
        {
            counter.Prompt = () => Sim.State.Cash >= TownRules.ModemPrice
                ? $"Buy a modem at the counter: ${TownRules.ModemPrice} (you have {Sim.State.Modems})"
                : $"A modem is ${TownRules.ModemPrice}. You have ${Sim.State.Cash}";
            counter.CanUse = () => Sim.State.Cash >= TownRules.ModemPrice;
            counter.OnUse = () => TownRules.BuyModem(Sim.State);
        }
    }

    /// <summary>Work takes time. Long jobs can roll the calendar over.</summary>
    private void SpendHours(float hours)
    {
        _dayClock += hours / 24f * DayLengthSeconds;
        while (_dayClock >= DayLengthSeconds) { _dayClock -= DayLengthSeconds; Sim.AdvanceDay(); }
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

    // ---- loop --------------------------------------------------------------------------------
    public override void _Process(double delta)
    {
        if (!AnyScreenOpen)
        {
            _dayClock += (float)delta;
            if (_dayClock >= DayLengthSeconds) { _dayClock -= DayLengthSeconds; Sim.AdvanceDay(); }
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
            _cargo.Refresh();
        }

        if (targetLot is not null && _town.Lots.FirstOrDefault(l => l.Lot.Id == targetLot) is { } t)
            next += $"  ({(t.Position - who.GlobalPosition).Length():0} m)";

        var biome = _planet.BiomeAt(PlanetBuilder.ToSim(who.GlobalPosition));
        string where = $"{biome}  alt {who.Altitude:0} m" + (_van.Driven ? $"  {Mathf.Abs(_van.Speed) * 3.6f:0} km/h{(_van.OnRoad ? "" : " (off-road)")}" : _player.Flying ? "  SURVEY FLIGHT" : "");
        string? prompt = AnyScreenOpen ? null
            : _van.Driven ? "[E] Get out   [W/S] drive   [A/D] steer"
            : _player.Focus is { } f ? (f.CanUse() ? "[E] " : "") + f.Prompt() + (f.AltPrompt() is { } alt ? $"   [R] {alt}" : "")
            : _cargo.Held is { } held ? $"Carrying the {held.Label}   [G] put it down" : null;
        _hud.Refresh(Sim, prompt, Hour, where, AnyScreenOpen ? "" : "▶ " + next);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (AnyScreenOpen) return;
        if (_van.Driven && e.IsActionPressed("interact")) { ExitVan(); GetViewport().SetInputAsHandled(); }
        else if (e.IsActionPressed("drop") && !_van.Driven) _cargo.Drop();
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
        var jobLot = _town.Lots.First(l => TownRules.JobAt(Sim.State, l.Lot.Id) is not null);
        jobLot.Door.OnUse!();
        bool paid = Sim.State.Cash > cash;

        // --- house install: sign someone up, get let in, walk through the door, do every step by hand ---
        Customer? first = null;
        for (int i = 0; i < 6 && first is null; i++) { Sim.State.MessagesWaiting = 1; first = Sim.TakeEnquiry(); }
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
            foreach (var item in _cargo.Items) _cargo.Stow(item);
            house = carried && walkedIn && first!.SocketFitted && site.Fitted && Sim.State.Defects.All(d => d.Source != "install");
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
        Sim.State.Cash += 1000; Sim.State.Modems += 3;
        bool hired = WorkRules.Upgrade(Sim.State, _content, "install") == UpgradeResult.Ok;
        Customer? next = null;
        for (int i = 0; i < 6 && next is null; i++) { Sim.State.MessagesWaiting = 1; next = Sim.TakeEnquiry(); }
        Sim.AdvanceDays(2);
        bool techDid = next is { Installed: true } && Sim.State.TechInstalls > 0;
        Sim.State.Defects.Add(new LatentDefect { Id = "defect-smoke", SurfaceDay = Sim.State.Day + 1, Source = "technician", CustomerId = next!.Id, Text = "a loose terminal" });
        Sim.AdvanceDay();
        var faultLot = _town.Lots.First(l => l.Lot.Id == next.LotId);
        bool needsKit = TownRules.FaultAt(Sim.State, faultLot.Lot.Id) is not null && !faultLot.Door.CanUse();
        _kitEverywhere = true;
        faultLot.Door.OnUse!();
        _kitEverywhere = false;
        bool auto = hired && techDid && needsKit && Sim.State.Faults.Count == 0;
        GD.Print($"AUTOMATION {(auto ? "OK" : "FAIL")}: hired={hired} techDid={techDid} faultNeedsKit={needsKit} faultsLeft={Sim.State.Faults.Count}");
        Sim.AdvanceDays(700);
        var reloaded = new Simulation(_content, SaveCodec.FromJson(SaveCodec.ToJson(Sim.State)));
        bool ok = world && doors && paid && house && auto && clicked && outcome.Perfect && reloaded.State.Day == Sim.State.Day && Sim.State.ActIndex >= 1;
        GD.Print($"SMOKE {(ok ? "OK" : "FAIL")}: doors={doors} paid={paid} perfect={outcome.Perfect} {Sim.State.Date} act={Sim.State.ActIndex} subs={Sim.State.LiveSubscribers} cash={Sim.State.Cash}");
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
        for (int i = 0; i < 6 && shown is null; i++) { Sim.State.MessagesWaiting = 1; shown = Sim.TakeEnquiry(); }
        if (shown is not null && _town.Lots.First(l => l.Lot.Id == shown.LotId) is { Install: { } site } home)
        {
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
        GD.Print($"SHOTS OK fps={Engine.GetFramesPerSecond()}");
        GetTree().Quit();
    }
}
