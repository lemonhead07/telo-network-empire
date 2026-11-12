using Godot;
using Telo.Sim.Almanac;
using Telo.Sim.Town;
using Telo.Sim.World;

namespace Telo.Game.UI;

/// <summary>
/// Always-on readout. Every player-facing number has a home here. In Act 0 it is a wall calendar and a
/// cash tin; it gets more instrument-like as the company earns better information systems.
/// </summary>
public partial class Hud : Control
{
    private Label _left = null!, _right = null!, _prompt = null!, _feed = null!, _crosshair = null!, _next = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        var font = new SystemFont { FontNames = new[] { "Courier New", "DejaVu Sans Mono", "monospace" } };
        Label Make(Vector2 pos, HorizontalAlignment align = HorizontalAlignment.Left, int size = 18)
        {
            var l = new Label { Position = pos, HorizontalAlignment = align, MouseFilter = MouseFilterEnum.Ignore };
            l.AddThemeFontOverride("font", font);
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.78f));
            l.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
            l.AddThemeConstantOverride("shadow_offset_x", 2); l.AddThemeConstantOverride("shadow_offset_y", 2);
            AddChild(l);
            return l;
        }
        _left = Make(new Vector2(24, 18));
        _right = Make(new Vector2(0, 18), HorizontalAlignment.Right);
        _right.AnchorLeft = 1; _right.AnchorRight = 1; _right.OffsetLeft = -900; _right.OffsetRight = -24; _right.GrowHorizontal = GrowDirection.Begin;
        _feed = Make(new Vector2(24, 0), size: 16);
        _feed.AnchorTop = 1; _feed.AnchorBottom = 1; _feed.OffsetTop = -190; _feed.AutowrapMode = TextServer.AutowrapMode.WordSmart; _feed.Size = new Vector2(900, 170);
        _next = Make(Vector2.Zero, HorizontalAlignment.Center, 20);
        _next.AnchorLeft = 0.5f; _next.AnchorRight = 0.5f; _next.OffsetLeft = -500; _next.OffsetRight = 500; _next.OffsetTop = 18;
        _next.AddThemeColorOverride("font_color", new Color(1f, 0.86f, 0.45f));
        _crosshair = Make(Vector2.Zero, HorizontalAlignment.Center, 22);
        _crosshair.Text = "·";
        _crosshair.SetAnchorsPreset(LayoutPreset.Center);
        _prompt = Make(Vector2.Zero, HorizontalAlignment.Center, 20);
        _prompt.AnchorLeft = 0.5f; _prompt.AnchorRight = 0.5f; _prompt.AnchorTop = 0.5f; _prompt.AnchorBottom = 0.5f;
        _prompt.OffsetLeft = -400; _prompt.OffsetRight = 400; _prompt.OffsetTop = 30;
        BuildBubble();
    }

    // ---- PIP's speech bubble -------------------------------------------------------------------
    private PanelContainer _bubble = null!;
    private Label _bubbleText = null!;
    private readonly Queue<string> _bubbleQueue = new();
    private float _bubbleLeft;

    private void BuildBubble()
    {
        _bubble = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        var box = new StyleBoxFlat { BgColor = new Color(0.96f, 0.93f, 0.82f, 0.96f), BorderColor = new Color(0.16f, 0.14f, 0.1f) };
        box.SetBorderWidthAll(3); box.SetCornerRadiusAll(16); box.SetContentMarginAll(14);
        box.CornerRadiusBottomLeft = 2; // the corner that points at the speaker
        _bubble.AddThemeStyleboxOverride("panel", box);
        _bubble.AnchorLeft = 0.5f; _bubble.AnchorRight = 0.5f; _bubble.AnchorTop = 1; _bubble.AnchorBottom = 1;
        _bubble.OffsetLeft = -330; _bubble.OffsetRight = 330; _bubble.OffsetBottom = -210; _bubble.GrowVertical = GrowDirection.Begin;
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        _bubble.AddChild(row);
        var who = new Label { Text = "PIP", VerticalAlignment = VerticalAlignment.Top };
        who.AddThemeFontSizeOverride("font_size", 20);
        who.AddThemeColorOverride("font_color", new Color(0.1f, 0.45f, 0.4f));
        row.AddChild(who);
        _bubbleText = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(560, 0) };
        _bubbleText.AddThemeFontSizeOverride("font_size", 19);
        _bubbleText.AddThemeColorOverride("font_color", new Color(0.12f, 0.1f, 0.08f));
        row.AddChild(_bubbleText);
        AddChild(_bubble);
    }

    /// <summary>PIP says something. Lines queue so they never talk over each other.</summary>
    public void Say(string text) => _bubbleQueue.Enqueue(text);
    public bool Speaking => _bubble.Visible;

    public override void _Process(double delta)
    {
        _bubbleLeft -= (float)delta;
        if (_bubbleLeft > 0) return;
        if (_bubbleQueue.Count > 0)
        {
            _bubbleText.Text = _bubbleQueue.Dequeue();
            _bubble.Visible = true;
            _bubbleLeft = Mathf.Clamp(_bubbleText.Text.Length * 0.07f, 4.5f, 9f); // reading time
        }
        else _bubble.Visible = false;
    }

    public void Refresh(Simulation sim, string? prompt, float hour, string where, string next)
    {
        var s = sim.State;
        var act = sim.Content.ActAt(s.ActIndex);
        // Numbers appear when the player first has a use for them (see Town/Onboarding), not all at once.
        var step = Onboarding.Current(s, sim.Content);
        string counters = $"Cash ${s.Cash}"
            + (step >= OnboardingStep.OddJob ? $"   Subscribers {s.LiveSubscribers}" : "")
            + (step >= OnboardingStep.Install ? $"   Awaiting install {s.PendingInstalls}   Modems {s.Modems}" : "")
            + (step >= OnboardingStep.Free && s.LastEvening is { } eve ? $"   Board {eve.Peak}/{eve.LinesIn} lines{(eve.Engaged > 0 ? $", {eve.Engaged} engaged" : "")}" : "")
            + (s.Faults.Count > 0 ? $"   Faults {s.Faults.Count}" : "");
        _left.Text = $"{s.Date}  {(int)hour:00}:{(int)(hour * 60) % 60 / 10 * 10:00}\nAct {act.Index} — {act.Name}\n{counters}";
        _right.Text = (step >= OnboardingStep.Free ? $"Archive {s.Archive}\nEcho {s.Echo} ({AlmanacRules.Band(s.Echo).ToString().ToLowerInvariant()})\n" : "") + $"{where}\n[F1] God keys  [F5] Save";
        _prompt.Text = prompt ?? "";
        _next.Text = next;
        _feed.Text = string.Join("\n", s.Log.TakeLast(step >= OnboardingStep.Free ? 5 : 3).Select(e => $"{new Telo.Sim.Core.GameDate(e.Day)} — {e.Text}"));
    }

    public void SetCrosshair(bool visible) => _crosshair.Visible = visible;

    /// <summary>Hide the status strip while the player is reading something in the world.</summary>
    public void SetReading(bool reading) { _left.Visible = _right.Visible = _feed.Visible = !reading; }
}
