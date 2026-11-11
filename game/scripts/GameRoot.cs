using Godot;
using Telo.Game.Player;
using Telo.Game.UI;
using Telo.Game.World;
using Telo.Sim.Content;
using Telo.Sim.Minigames.Frame;
using Telo.Sim.Save;
using Telo.Sim.Work;
using Telo.Sim.World;

namespace Telo.Game;

/// <summary>Composition root: loads content, owns the Simulation, builds the room, wires views. No game rules here.</summary>
public partial class GameRoot : Node3D
{
    public const float DayLengthSeconds = 90f;
    public const string SavePath = "user://save.json";

    public Simulation Sim { get; private set; } = null!;
    private FirstPersonController _player = null!;
    private Hud _hud = null!;
    private FrameView _frame = null!;
    private AlmanacView _almanac = null!;
    private NotebookView _notebook = null!;
    private float _dayClock;
    private ContentDb _content = null!;

    private bool AnyModalOpen => _frame.Visible || _almanac.Visible || _notebook.Visible;

    public override void _Ready()
    {
        InputSetup.Register();
        _content = LoadContent();
        Sim = Simulation.NewGame(_content, seed: (ulong)Time.GetUnixTimeFromSystem());

        RoomBuilder.BuildShell(this);
        BuildProps();

        _player = new FirstPersonController { Position = new Vector3(0.5f, 0.05f, 1.2f), IsFrozen = () => AnyModalOpen };
        AddChild(_player);

        var ui = new CanvasLayer();
        AddChild(ui);
        _hud = new Hud(); ui.AddChild(_hud);
        _frame = new FrameView { Sim = Sim }; ui.AddChild(_frame);
        _almanac = new AlmanacView { Sim = Sim }; ui.AddChild(_almanac);
        _notebook = new NotebookView { Sim = Sim }; ui.AddChild(_notebook);

        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--smoke")) CallDeferred(nameof(RunSmoke));
        if (args.FirstOrDefault(a => a.StartsWith("--shot=")) is { } shot) _ = TakeScreenshot(shot["--shot=".Length..]);
    }

    private static ContentDb LoadContent() => ContentDb.FromJson(ContentDb.FileNames.ToDictionary(
        f => f, f => Godot.FileAccess.GetFileAsString($"res://content/{f}") is { Length: > 0 } t ? t
            : throw new InvalidOperationException($"Missing res://content/{f}")));

    private void BuildProps()
    {
        float back = -RoomBuilder.D / 2 + 0.1f;

        // The frame: wooden backboard with rows of terminal blocks.
        RoomBuilder.Prop(this, "Frame", new Vector3(-1.4f, 1.35f, back), new Vector3(1.5f, 1.2f, 0.08f), RoomBuilder.Board, FramePrompt, UseFrame);
        for (int row = 0; row < 6; row++)
            RoomBuilder.Box(this, new Vector3(-1.4f, 0.9f + row * 0.18f, back + 0.07f), new Vector3(1.2f, 0.07f, 0.05f), RoomBuilder.Beige);
        RoomBuilder.Label(this, "FRAME", new Vector3(-1.4f, 2.05f, back + 0.06f), 0.12f);

        // Desk with answering machine, PIP's handset, and the notebook.
        RoomBuilder.Box(this, new Vector3(1.3f, 0.37f, back + 0.45f), new Vector3(1.7f, 0.74f, 0.8f), RoomBuilder.Wood, solid: true);
        RoomBuilder.Prop(this, "AnsweringMachine", new Vector3(0.75f, 0.8f, back + 0.4f), new Vector3(0.28f, 0.1f, 0.2f), new Color(0.08f, 0.08f, 0.08f),
            () => Sim.State.MessagesWaiting > 0 ? $"Play messages ({Sim.State.MessagesWaiting} waiting)" : "No new messages", PlayMessages);
        RoomBuilder.Prop(this, "Handset", new Vector3(1.35f, 0.77f, back + 0.45f), new Vector3(0.08f, 0.04f, 0.16f), new Color(0.15f, 0.16f, 0.2f),
            () => "Open PIP's Almanac", OpenAlmanac);
        RoomBuilder.Prop(this, "Notebook", new Vector3(1.8f, 0.76f, back + 0.5f), new Vector3(0.22f, 0.03f, 0.3f), new Color(0.5f, 0.12f, 0.1f),
            () => "Open the company notebook", OpenNotebook);

        // Camp bed. Sleeping is how days pass quickly.
        RoomBuilder.Prop(this, "Bed", new Vector3(-2.2f, 0.22f, 1.4f), new Vector3(0.9f, 0.44f, 1.9f), RoomBuilder.Fabric,
            () => "Sleep until morning", () => { Sim.AdvanceDay(); _dayClock = 0; });
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

    private void OpenAlmanac() => _almanac.Open(Telo.Sim.Content.InterfaceTier.Terminal); // PIP is from 2035: always a screen
    private void OpenNotebook() => _notebook.Open(WorkRules.InterfaceFor(Sim.State, Sim.Content, "billing"));

    // ---- loop --------------------------------------------------------------------------------
    public override void _Process(double delta)
    {
        if (!AnyModalOpen)
        {
            _dayClock += (float)delta;
            if (_dayClock >= DayLengthSeconds) { _dayClock = 0; Sim.AdvanceDay(); }
        }
        _hud.SetCrosshair(!AnyModalOpen);
        _hud.Refresh(Sim, AnyModalOpen ? null : _player.Focus?.Prompt(), _dayClock / DayLengthSeconds);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (AnyModalOpen) return;
        if (e.IsActionPressed("almanac")) OpenAlmanac();
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
        Sim = new Simulation(_content, SaveCodec.FromJson(Godot.FileAccess.GetFileAsString(SavePath)));
        _frame.Sim = _almanac.Sim = _notebook.Sim = Sim;
    }

    // ---- automation hooks for CI / screenshots -----------------------------------------------
    private void RunSmoke()
    {
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
        bool ok = outcome.Perfect && reloaded.State.Day == Sim.State.Day && Sim.State.ActIndex >= 1;
        GD.Print($"SMOKE {(ok ? "OK" : "FAIL")}: {Sim.State.Date} act={Sim.State.ActIndex} subs={Sim.State.LiveSubscribers} cash={Sim.State.Cash} archive={Sim.State.Archive}");
        GetTree().Quit(ok ? 0 : 1);
    }

    private async Task TakeScreenshot(string path)
    {
        Input.MouseMode = Input.MouseModeEnum.Visible; // stray mouse motion must not move the camera
        for (int i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _player.Rotation = new Vector3(0, 0.35f, 0);
        _player.Camera.Rotation = new Vector3(-0.12f, 0, 0);
        for (int i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetViewport().GetTexture().GetImage().SavePng(path);
        UseFrameForShot();
        for (int i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetViewport().GetTexture().GetImage().SavePng(path.Replace(".png", "-frame.png"));
        GetTree().Quit();
    }

    private void UseFrameForShot()
    {
        for (int i = 0; i < 3; i++) PlayMessages();
        UseFrame();
    }
}
