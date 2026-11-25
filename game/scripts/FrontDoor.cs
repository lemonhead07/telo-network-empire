using Godot;
using Telo.Game.UI;
using Telo.Game.World;
using Telo.Sim.Content;
using Telo.Sim.Save;
using Telo.Sim.Town;
using Telo.Sim.World;

namespace Telo.Game;

/// <summary>
/// The front door (EA6a): what a player sees before and around the game, all of it on things in the room.
///   The title is a typed letter pinned above the desk in the rented room, the world built behind it as the backdrop.
///   Continue, New game, Load, Settings, How to play, Quit; worked with the mouse on the letter or with the keys.
///   Pause (Esc) brings PIP's handset up in front of you and stops the game: Resume, Save, Load, Settings, How to play,
///   Quit to title, Quit game. Esc again puts it away.
///   Saves: three slots and an autosave (each morning and on quitting), see <see cref="SaveSlots"/>.
/// Developer flags (--smoke, --shot, --autoplay..., --from=...) skip all of this, so automated checks run as before.
/// </summary>
public partial class GameRoot
{
    /// <summary>A game chosen from Continue or Load, carried over the reload that rebuilds the planet from its seed.</summary>
    private static GameState? _pendingState;
    /// <summary>Developer flags only count for the launch they were given on (Quit to title shows the title).</summary>
    private static bool _booted;
    /// <summary>An automated or developer start: no title, no autosaves, the old save left alone.</summary>
    private bool _devRun;
    /// <summary>The title letter is up: nothing moves and the clock is still.</summary>
    private bool _frontDoor;
    /// <summary>PIP's handset is held up in front of you showing a page (pause, settings...): the game is stopped.</summary>
    private bool _deviceUp;
    private bool _autosaveDue;
    private Node3D? _titleCard;
    private WorldScreen? _titleScreen;
    private MenuView _title = null!, _pause = null!;
    private Transform3D _phoneRest;
    private Node3D _deviceBody = null!;
    private static readonly string[] DevFlags = { "--smoke", "--shot=", "--shots=", "--autoplay", "--from=" };

    private static bool IsDevRun(string[] args) => args.Any(a => DevFlags.Any(a.StartsWith));

    private static string Version => ProjectSettings.GetSetting("application/config/version").AsString() is { Length: > 0 } v ? $"v{v.TrimStart('v')}" : "";

    // ---- hooks for the settings and how-to-play pages (EA6: the lead wires the real views in here) -------------------
    /// <summary>The settings page (EA6b, SettingsView), on the title letter or on PIP's handset.</summary>
    public void OpenSettings() => ShowOnDevice(Fresh(new SettingsView()));

    /// <summary>PIP's how-to-play pages (EA6b, HowToPlayView), on the title letter or on PIP's handset.</summary>
    public void OpenHowToPlay() => ShowOnDevice(Fresh(new HowToPlayView()));

    /// <summary>Pages made fresh each time they're opened are freed when they close.</summary>
    private static T Fresh<T>(T v) where T : ModalView { v.Closed += () => v.QueueFree(); return v; }

    /// <summary>
    /// Show any page on the surface in front of the player, with a way back: on the title letter while the title is up,
    /// otherwise on PIP's handset held up in front of the camera (which stops the game until it is put away). Esc, or
    /// the page closing itself, returns to whatever was under it; when nothing is left, the handset goes back to the edge
    /// of view and the game carries on.
    /// </summary>
    public void ShowOnDevice(ModalView view)
    {
        if (_frontDoor && _titleScreen is not null) { _titleScreen.Push(view, InterfaceTier.Paper); return; }
        RaiseDevice();
        _phone.Push(view, InterfaceTier.Terminal);
    }

    // ---- PIP's handset as the device -------------------------------------------------------------------------------
    private void SetUpDevice()
    {
        _phone.ProcessMode = ProcessModeEnum.Always;   // it is the one thing that works while the game is stopped
        _phone.Uncovered += () => { if (_phone.View == _phoneView) LowerDevice(); };
        // the handset's body, seen when it is held up: a dark case round the screen, a grille above, keys below (placeholder)
        float w = 0.24f, h = w * WorldScreen.PxH / WorldScreen.PxW;
        var ink = new Color(0.09f, 0.1f, 0.12f);
        var lp = new LowPoly()
            .Box(new Vector3(0, -0.015f, -0.012f), new Vector3(w + 0.02f, h + 0.052f, 0.018f), ink)
            .Box(new Vector3(0, h / 2 + 0.0055f, -0.002f), new Vector3(0.06f, 0.004f, 0.002f), new Color(0.2f, 0.21f, 0.23f));
        for (int k = -2; k <= 2; k++) lp.Box(new Vector3(k * 0.034f, -h / 2 - 0.021f, -0.002f), new Vector3(0.024f, 0.011f, 0.004f), k == 0 ? new Color(0.75f, 0.55f, 0.15f) : new Color(0.28f, 0.29f, 0.31f));
        _deviceBody = lp.Instance("HandsetBody");
        _deviceBody.Visible = false;
        _phone.AddChild(_deviceBody);
        _pause = new MenuView { HeadingSize = 46, LeadSize = 26, ItemSize = 34, OnHomeCancel = () => _pause.Close() };
        _pause.Home = PauseHome;
    }

    private void RaiseDevice()
    {
        if (_deviceUp) return;
        _deviceUp = true;
        _phoneRest = _phone.Transform;
        var cam = _phone.GetParent() as Camera3D ?? _player.Camera;
        var size = GetViewport().GetVisibleRect().Size;
        float d = _phone.FitDistance(cam.Fov, size.X / size.Y, 0.6f);
        _phone.Transform = new Transform3D(Basis.Identity, new Vector3(0, d * 0.09f, -d));   // a little high, so its keys show below
        _phone.Visible = true;
        _deviceBody.Visible = true;
        // the status strip is not updated while the game is stopped: clear what it was saying about the thing in front of you
        _hud.Refresh(Sim, null, Hour, "", ""); _hud.SetHold(0); _hud.SetCrosshair(false);
        GetTree().Paused = true;
    }

    private void LowerDevice()
    {
        if (!_deviceUp) return;
        _deviceUp = false;
        _phone.Transform = _phoneRest;
        _deviceBody.Visible = false;
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void OpenPause()
    {
        _pause.Reset();
        ShowOnDevice(_pause);
    }

    private MenuView.Page PauseHome()
    {
        var s = Sim.State;
        return new MenuView.Page("PAUSED",
            $"{s.Date}  {Clock.Text(s)}   Act {s.ActIndex}: {_content.ActAt(s.ActIndex).Name}   ${s.Cash}\nThe clock is stopped. Esc goes back to work.",
            new MenuView.Item[]
            {
                new("Resume", () => _pause.Close()),
                new("Save", () => _pause.Go(SavePage)),
                new("Load", () => _pause.Go(() => LoadPage(_pause))),
                new("Settings", OpenSettings),
                new("How to play", OpenHowToPlay),
                new("Quit to title", QuitToTitle),
                new("Quit game", QuitGame),
            });
    }

    private MenuView.Page SavePage() => new("SAVE", "Which slot? A slot that holds a game is written over.",
        SaveSlots.Manual.Select(slot => new MenuView.Item($"{SaveSlots.Name(slot)}: {Describe(SaveSlots.Read(slot))}", () =>
        {
            bool ok = SaveSlots.Write(slot, Sim.State);
            _pause.Back();
            _pause.Say(ok ? $"Saved in {SaveSlots.Name(slot)}." : "Couldn't write that slot.");
        })).Append(new MenuView.Item("Back", () => _pause.Back())).ToList());

    private MenuView.Page LoadPage(MenuView menu) => new("LOAD", menu == _pause ? "Pick a saved game. The one you're playing isn't saved by this." : "Pick a saved game.",
        SaveSlots.All.Select(slot => SaveSlots.Read(slot) is { } info
                ? new MenuView.Item($"{SaveSlots.Name(slot)}: {Describe(info)}", () => LoadSlot(slot, menu.Say))
                : new MenuView.Item($"{SaveSlots.Name(slot)}: empty", null, false))
            .Append(new MenuView.Item("Back", () => menu.Back())).ToList());

    /// <summary>"14 Mar 1984, Act 1, $2340", then "saved 25 Nov, 14:02" on a line of its own under it; or "empty".</summary>
    private static string Describe(SaveSlots.Info? info) => info is null ? "empty" : $"{info.Date}, Act {info.Act}, ${info.Cash}\n     saved {info.Saved}";

    // ---- the title -------------------------------------------------------------------------------------------------
    /// <summary>The title letter, pinned to the wall above the desk; the camera framed on it and the player held still.</summary>
    private void EnterFrontDoor()
    {
        _frontDoor = true;
        if (_titleCard is null)
        {
            float back = -RoomBuilder.D / 2 + 0.1f;
            // on the wall above the desk, clear of the answering machine and PIP's handset below it
            _titleCard = new Node3D { Name = "TitleLetter", Position = new Vector3(1.3f, 1.52f, back + 0.02f) };
            _town.Hq.AddChild(_titleCard);
            // a cork board in a wooden frame with the letter drawing-pinned to it (placeholder: boxes)
            _titleCard.AddChild(new LowPoly()
                .Box(new Vector3(0, 0, -0.012f), new Vector3(1.46f, 0.98f, 0.024f), new Color(0.36f, 0.24f, 0.14f))
                .Box(new Vector3(0, 0, -0.001f), new Vector3(1.38f, 0.9f, 0.006f), new Color(0.58f, 0.43f, 0.28f))
                .Box(new Vector3(0, 0.405f, 0.006f), new Vector3(0.018f, 0.018f, 0.012f), new Color(0.75f, 0.12f, 0.1f)).Instance());
            _title = new MenuView { HeadingSize = 84, LeadSize = 30, ItemSize = 42, ItemGap = 12 };
            _title.Home = TitleHome;
            _titleScreen = WorldScreen.Create(_title, 1.3f, glows: false);
            _titleScreen.Position = new Vector3(0, -0.01f, 0.004f);
            _titleCard.AddChild(_titleScreen);
        }
        _title.Reset();
        _title.Open(InterfaceTier.Paper);
        // frame the letter as if from the desk chair: a step back and a little below, so the wall, the board's frame and
        // the top of the desk show round it
        var cam = _player.Camera;
        _cameraHome = cam.Transform;
        var size = GetViewport().GetVisibleRect().Size;
        var screen = _titleScreen!;
        var up = screen.GlobalBasis.Y.Normalized();
        var at = screen.GlobalPosition - up * 0.06f;
        // level with the letter: from lower down, the answering machine's aerial stood up in front of its bottom lines
        var from = at + screen.GlobalBasis.Z.Normalized() * screen.FitDistance(cam.Fov, size.X / size.Y, 0.6f) + up * 0.06f;
        cam.GlobalTransform = new Transform3D(Basis.Identity, from).LookingAt(at, up);
    }

    private MenuView.Page TitleHome()
    {
        var newest = SaveSlots.Newest();
        var items = new List<MenuView.Item>();
        if (newest is not null) items.Add(new($"Continue   {newest.Date}, Act {newest.Act}, ${newest.Cash}", () => LoadSlot(newest.Slot, _title.Say)));
        items.Add(new("New game", StartNewGame));
        items.Add(new("Load", () => _title.Go(() => LoadPage(_title)), SaveSlots.All.Any(s => SaveSlots.Read(s) is not null)));
        items.Add(new("Settings", OpenSettings));
        items.Add(new("How to play", OpenHowToPlay));
        items.Add(new("Quit", () => GetTree().Quit()));
        return new MenuView.Page("TELO: Network Empire",
            $"A telephone company, from one rented room. 1983 to 2035.{(Version.Length > 0 ? $"      {Version}" : "")}", items);
    }

    /// <summary>New game: the world behind the letter is already a fresh one. Put the letter away and the opening begins.</summary>
    private void StartNewGame()
    {
        if (!_frontDoor) return;
        _frontDoor = false;
        _title.Close();
        _titleCard?.QueueFree(); _titleCard = null; _titleScreen = null;
        LeanBack();
        Input.MouseMode = Input.MouseModeEnum.Captured;
        if (_lastStep == OnboardingStep.Enquiry) _pip.Once("hello");
    }

    // ---- saving, loading, quitting ---------------------------------------------------------------------------------
    private void Autosave()
    {
        if (_devRun || _frontDoor) return;
        SaveSlots.Write(SaveSlots.Autosave, Sim.State);
    }

    /// <summary>Load a slot: check it reads first (a bad file says so on the menu), then rebuild the world from it.</summary>
    private void LoadSlot(string slot, Action<string> complain)
    {
        if (SaveSlots.Load(slot, _content, out var error) is not { } state) { complain(error ?? "That save can't be read."); return; }
        _pendingState = state;
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();   // the planet is rebuilt from the save's seed
    }

    private void QuitToTitle()
    {
        Autosave();
        _pendingState = null;
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }

    private void QuitGame()
    {
        Autosave();
        GetTree().Quit();
    }

    public override void _Notification(int what)
    {
        // closing the window is quitting too
        if (what == NotificationWMCloseRequest && Sim is not null) Autosave();
    }

    /// <summary>F5: a quick save, into the autosave (it is what Continue picks up). F9: load the newest save.</summary>
    public void Save()
    {
        if (!SaveSlots.Write(SaveSlots.Autosave, Sim.State)) return;
        Sim.State.Emit(Telo.Sim.Core.EventKind.Info, "Saved.");
        _pip.Say("Saved. Esc, then Save, puts it in a slot of its own.");
    }

    public void Load()
    {
        if (SaveSlots.Newest() is { } newest) LoadSlot(newest.Slot, _pip.Say);
    }

    /// <summary>
    /// --shots=title: the title letter as a returning player sees it, its Load page, then the pause page on PIP's handset out
    /// in the street and its Save page. Driven by real clicks on the letter and real key presses, so it is a check as well;
    /// it ends by saving a slot and loading it back, and the rebuilt world reports and quits (so this returns false: don't quit).
    /// </summary>
    private async Task<bool> TitleShots(string path, Camera3D shotCam)
    {
        shotCam.Current = false; _player.Camera.Current = true;
        void Snap(string suffix) => GetViewport().GetTexture().GetImage().SavePng(path.Replace(".png", suffix + ".png"));
        async Task Key(Key k) { foreach (bool down in new[] { true, false }) { GetViewport().PushInput(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = down }); await Frames(3); } }
        async Task Click(Vector2 at) { foreach (bool down in new[] { true, false }) { GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = down, Position = at, GlobalPosition = at }, true); await Frames(3); } }
        // a returning player: a scratch saves folder (never the real one) with a slot and a newer autosave in it
        SaveSlots.Dir = "user://shot-saves";
        var later = SaveCodec.FromJson(SaveCodec.ToJson(Sim.State), _content);
        later.Day += 610; later.ActIndex = 1; later.Cash = 2340;
        SaveSlots.Write("slot-1", later);
        later.Day += 3; later.Cash = 2415;
        SaveSlots.Write(SaveSlots.Autosave, later);
        bool continues = SaveSlots.Newest() is { Cash: 2415 } && SaveSlots.Read("slot-1") is { Cash: 2340, Act: 1 };

        EnterFrontDoor();
        float clock = Sim.State.Minute + _minuteFrac;
        await Frames(20);
        Snap("-title");
        bool held = _frontDoor && !_phone.Visible && Sim.State.Minute + _minuteFrac == clock;   // nothing moves behind the letter
        if (_title.PointOf("Load") is { } load) await Click(_titleScreen!.DisplayPointFor(load, _player.Camera));   // a click on the letter
        await Frames(6);
        Snap("-title-load");
        bool loadPage = _title.PointOf("Slot 1") is not null;
        await Key(Godot.Key.Escape);                                                           // back to the first page
        int n = _title.PointOf("Continue") is null ? 3 : 4;                                    // Settings
        await Key(Godot.Key.Key1 + n - 1);
        await Frames(6);
        Snap("-title-settings");
        bool onLetter = _titleScreen!.View != _title && _titleScreen.View.IsOpen;              // shown on the letter, over the menu
        await Key(Godot.Key.Escape);                                                           // and Esc comes back to it
        onLetter &= _titleScreen.View == _title && _title.IsOpen && _frontDoor;
        await Key(Godot.Key.Key2);                                                             // 2: New game
        await Frames(4);
        bool started = !_frontDoor && _titleCard is null;

        // out on Main Street, a few steps from the room, looking along the road
        var stand = TownBuilder.Place(_planet, 14, TownBuilder.StreetSouth + 2.5f, 0, 0.4f);
        _player.PlaceAt(stand.Origin, TownBuilder.Place(_planet, 40, TownBuilder.StreetSouth).Origin - stand.Origin);
        _player.Camera.Transform = new Transform3D(Basis.Identity, new Vector3(0, 1.62f, 0));
        for (int i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await Key(Godot.Key.Escape);                                                           // Esc: PIP's handset comes up
        clock = Sim.State.Minute + _minuteFrac;
        await Frames(20);
        Snap("-pause");
        bool paused = _deviceUp && GetTree().Paused && _pause.IsOpen;
        await Key(Godot.Key.Down); await Key(Godot.Key.Enter);                                 // down to Save, Enter
        await Frames(6);
        Snap("-pause-save");
        bool savePage = _pause.PointOf("Slot 1") is not null;
        await Key(Godot.Key.Escape);                                                           // back to the pause page
        bool stillPaused = _deviceUp && _pause.IsOpen && savePage && Sim.State.Minute + _minuteFrac == clock;   // the clock stood still
        await Key(Godot.Key.Key4);                                                             // Settings, on the handset
        await Frames(6);
        Snap("-pause-settings");
        bool onHandset = _phone.View != _pause && _phone.View.IsOpen && _deviceUp;
        await Key(Godot.Key.Escape);                                                           // back to the pause page
        onHandset &= _phone.View == _pause && _pause.IsOpen && _deviceUp && GetTree().Paused;
        await Key(Godot.Key.Escape);                                                           // Esc again: back to work
        await Frames(4);
        bool resumed = !_deviceUp && !GetTree().Paused && !_pause.IsOpen;
        bool ok = continues && held && loadPage && onLetter && started && paused && stillPaused && onHandset && resumed;
        GD.Print($"TITLE {(ok ? "OK" : "FAIL")}: continueReadsNewest={continues} titleHolds={held} clickedLoad={loadPage} settingsOnLetter={onLetter} newGame={started} "
            + $"escPauses={paused} escBacksOut={stillPaused} settingsOnHandset={onHandset} escResumes={resumed}");

        // save into slot 2 from the pause page, then load it back: the world is rebuilt from the slot and play carries on
        Sim.State.Cash = 4321;
        int day = Sim.State.Day;
        await Key(Godot.Key.Escape); await Key(Godot.Key.Key2); await Key(Godot.Key.Key2);   // pause, Save, Slot 2
        bool saved = SaveSlots.Read("slot-2") is { Cash: 4321 } && _pause.IsOpen;
        await Key(Godot.Key.Key3);                                                             // Load
        _afterLoad = root => _ = root.CheckLoaded(day, ok && saved);
        // Slot 2 is the third line (autosave, slot 1, slot 2). Pressed without waiting: this world is about to go
        foreach (bool down in new[] { true, false }) GetViewport().PushInput(new InputEventKey { Keycode = Godot.Key.Key3, PhysicalKeycode = Godot.Key.Key3, Pressed = down });
        return false;
    }

    /// <summary>A check hands itself to the world that a load rebuilds (the old one is gone by then).</summary>
    private static Action<GameRoot>? _afterLoad;

    private async Task CheckLoaded(int day, bool before)
    {
        await Frames(20);
        bool ok = before && !_frontDoor && !_deviceUp && !GetTree().Paused && Sim.State.Day == day && Sim.State.Cash == 4321;
        GD.Print($"LOAD {(ok ? "OK" : "FAIL")}: savedAndReloaded={before} day={Sim.State.Day} cash={Sim.State.Cash} title={_frontDoor} paused={GetTree().Paused}");
        foreach (var slot in SaveSlots.All) DirAccess.RemoveAbsolute(SaveSlots.PathOf(slot));
        DirAccess.RemoveAbsolute(SaveSlots.Dir);
        SaveSlots.Dir = "user://saves";
        GetTree().Quit(ok ? 0 : 1);
    }
}
