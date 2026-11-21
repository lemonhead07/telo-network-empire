using Godot;
using Telo.Sim.Content;

namespace Telo.Game.UI;

/// <summary>
/// The mouse pointer drawn on the surface of a screen in the world, so the hand you are using is part of the thing
/// you are looking at (no floating system cursor). It takes the look of the instrument's era: a pencil point on
/// paper, a pulsing block on the green terminal, a chunky arrow on the database forms, and a modern arrow that turns
/// into a pointing hand over anything you can press on a dashboard.
/// </summary>
public partial class ScreenCursor : Control
{
    public InterfaceTier Tier { get; set; }
    public Vector2 Point { get; private set; }
    private bool _overButton;
    private double _t;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = false;
        ZIndex = 100;
    }

    public void MoveTo(Vector2 px, Control? hovered)
    {
        Point = px;
        _overButton = hovered is BaseButton { Disabled: false };
        Visible = true;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _t += delta;
        if (Tier == InterfaceTier.Terminal) QueueRedraw();   // the block blinks
    }

    public override void _Draw()
    {
        var p = Point;
        switch (Tier)
        {
            case InterfaceTier.Paper:
                // a pencil, point first, lying from bottom-right to the point
                var dir = new Vector2(1, 1.6f).Normalized();
                var tip = p; var wood = p + dir * 16; var end = p + dir * 70;
                var side = new Vector2(-dir.Y, dir.X) * 6;
                DrawColoredPolygon(new[] { tip, wood + side, wood - side }, new Color(0.85f, 0.72f, 0.52f));
                DrawColoredPolygon(new[] { tip, tip + dir * 6 + side * 0.4f, tip + dir * 6 - side * 0.4f }, new Color(0.2f, 0.2f, 0.22f));
                DrawColoredPolygon(new[] { wood + side, end + side, end - side, wood - side }, new Color(0.92f, 0.75f, 0.2f));
                DrawColoredPolygon(new[] { end + side, end + dir * 10 + side, end + dir * 10 - side, end - side }, new Color(0.85f, 0.45f, 0.5f));
                break;
            case InterfaceTier.Terminal:
                // a character cell under the hand, pulsing like the phosphor (never fully off, so you can always find it)
                var cell = new Vector2(Mathf.Floor(p.X / 12) * 12, Mathf.Floor(p.Y / 24) * 24);
                float glow = 0.55f + 0.35f * Mathf.Abs(Mathf.Sin((float)_t * 3.2f));
                DrawRect(new Rect2(cell, new Vector2(12, 22)), new Color(0.35f, 1f, 0.45f, glow));
                break;
            case InterfaceTier.Database:
                Arrow(p, 1.8f, Colors.Black, Colors.White, shadow: false);
                break;
            default:
                if (_overButton) Hand(p, 1.6f); else Arrow(p, 1.5f, Colors.White, Colors.Black, shadow: true);
                break;
        }
    }

    private static readonly Vector2[] ArrowPts =
    {
        new(0, 0), new(0, 22), new(5.5f, 17), new(9.5f, 26), new(13, 24.5f), new(9, 16), new(16, 16),
    };

    private void Arrow(Vector2 p, float s, Color fill, Color edge, bool shadow)
    {
        var pts = ArrowPts.Select(v => p + v * s).ToArray();
        if (shadow) DrawColoredPolygon(pts.Select(v => v + new Vector2(2, 3)).ToArray(), new Color(0, 0, 0, 0.3f));
        DrawColoredPolygon(pts, fill);
        DrawPolyline(pts.Append(pts[0]).ToArray(), edge, 1.6f, true);
    }

    private void Hand(Vector2 p, float s)
    {
        // a pointing finger whose tip is the hot spot, the rest of the hand below it
        var white = Colors.White; var ink = Colors.Black;
        Rect2 R(float x, float y, float w, float h) => new(p + new Vector2(x, y) * s, new Vector2(w, h) * s);
        var finger = R(-3, 0, 7, 16);
        var palm = R(-6, 12, 19, 14);
        DrawRect(new Rect2(palm.Position + new Vector2(2, 3) * s, palm.Size), new Color(0, 0, 0, 0.3f));
        DrawRect(palm, white); DrawRect(palm, ink, false, 1.5f * s);
        DrawRect(finger, white); DrawRect(finger, ink, false, 1.5f * s);
        for (int i = 0; i < 3; i++) DrawLine(p + new Vector2(4 + i * 3, 12) * s, p + new Vector2(4 + i * 3, 18) * s, ink, 1.2f * s);
    }
}
