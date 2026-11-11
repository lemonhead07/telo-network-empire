using Godot;
using Telo.Sim.Almanac;
using Telo.Sim.World;

namespace Telo.Game.UI;

/// <summary>
/// Always-on readout. Every player-facing number has a home here. In Act 0 it is a wall calendar and a
/// cash tin; it gets more instrument-like as the company earns better information systems.
/// </summary>
public partial class Hud : Control
{
    private Label _left = null!, _right = null!, _prompt = null!, _feed = null!, _crosshair = null!;

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
        _right.AnchorLeft = 1; _right.AnchorRight = 1; _right.OffsetLeft = -520; _right.OffsetRight = -24;
        _feed = Make(new Vector2(24, 0), size: 16);
        _feed.AnchorTop = 1; _feed.AnchorBottom = 1; _feed.OffsetTop = -190; _feed.AutowrapMode = TextServer.AutowrapMode.WordSmart; _feed.Size = new Vector2(900, 170);
        _crosshair = Make(Vector2.Zero, HorizontalAlignment.Center, 22);
        _crosshair.Text = "·";
        _crosshair.SetAnchorsPreset(LayoutPreset.Center);
        _prompt = Make(Vector2.Zero, HorizontalAlignment.Center, 20);
        _prompt.AnchorLeft = 0.5f; _prompt.AnchorRight = 0.5f; _prompt.AnchorTop = 0.5f; _prompt.AnchorBottom = 0.5f;
        _prompt.OffsetLeft = -400; _prompt.OffsetRight = 400; _prompt.OffsetTop = 30;
    }

    public void Refresh(Simulation sim, string? prompt, float dayProgress)
    {
        var s = sim.State;
        var act = sim.Content.ActAt(s.ActIndex);
        int hour = 8 + (int)(dayProgress * 12);
        _left.Text = $"{s.Date}  {hour:00}:00\nAct {act.Index} — {act.Name}\nCash ${s.Cash}   Subscribers {s.LiveSubscribers}   Awaiting install {s.PendingInstalls}";
        _right.Text = $"Archive {s.Archive}\nEcho {s.Echo} ({AlmanacRules.Band(s.Echo).ToString().ToLowerInvariant()})\n[Tab] PIP  [N] Notebook  [F5] Save";
        _prompt.Text = prompt is null ? "" : $"[E] {prompt}";
        _feed.Text = string.Join("\n", s.Log.TakeLast(5).Select(e => $"{new Telo.Sim.Core.GameDate(e.Day)} — {e.Text}"));
    }

    public void SetCrosshair(bool visible) => _crosshair.Visible = visible;
}
