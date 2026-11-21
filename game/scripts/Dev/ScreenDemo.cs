using Godot;
using Telo.Game.UI;
using Telo.Sim.Content;

namespace Telo.Game.Dev;

/// <summary>
/// Stand-in pictures for screens in the Showroom (dev only, no game data): a green site list for the night shift's
/// monitors, charts and a map for the dashboard years, an app grid for the touchscreen phone, a clock on an LCD.
/// Shows what ScreenFaces does before the real views exist.
/// </summary>
public partial class ScreenDemo : Control
{
    public enum Look { Terminal, Dashboard, Phone, Lcd }
    public Look Kind { get; init; }
    public int Seed { get; init; }
    /// <summary>Draw the in-world pointer at this pixel (dashboards: over a button, so it shows the hand).</summary>
    public Vector2? Pointer { get; init; }

    private static readonly string[] Sites = { "VALLEY", "EASTGATE", "COLD STORE", "MILLBROOK", "KESTREL PT", "FARLAND", "CORE A", "CORE B" };

    public override void _Ready()
    {
        if (Pointer is { } p && Kind == Look.Dashboard)
        {
            var c = new ScreenCursor { Tier = InterfaceTier.Dashboard };
            AddChild(c);
            c.MoveTo(p, new Button());
        }
    }

    private float R(int i) { float f = Mathf.Sin(Seed * 12.9898f + i * 78.233f) * 43758.547f; return f - Mathf.Floor(f); }

    public override void _Draw()
    {
        var size = Size;
        var font = ThemeDB.FallbackFont;
        switch (Kind)
        {
            case Look.Terminal:
                DrawRect(new Rect2(Vector2.Zero, size), new Color(0.02f, 0.05f, 0.02f));
                var green = new Color(0.35f, 1f, 0.45f); var amber = new Color(1f, 0.75f, 0.2f);
                DrawString(font, new Vector2(16, 34), $"SITE STATUS  {(Seed % 2 == 0 ? "NIGHT" : "DAY")}", HorizontalAlignment.Left, -1, 24, green);
                for (int i = 0; i < Sites.Length; i++)
                {
                    bool warn = (i + Seed) % 7 == 3;
                    DrawString(font, new Vector2(16, 72 + i * 28), $"{Sites[i],-11} {(warn ? "WARN  lamp amber" : "OK")}", HorizontalAlignment.Left, -1, 20, warn ? amber : green);
                }
                DrawString(font, new Vector2(16, size.Y - 18), "> _", HorizontalAlignment.Left, -1, 20, green);
                break;
            case Look.Dashboard:
                DrawRect(new Rect2(Vector2.Zero, size), new Color(0.08f, 0.10f, 0.14f));
                var ink = new Color(0.85f, 0.88f, 0.92f); var accent = new Color(0.95f, 0.48f, 0.15f); var good = new Color(0.30f, 0.85f, 0.45f);
                DrawString(font, new Vector2(20, 36), (Seed % 4) switch { 0 => "SUBSCRIBERS", 1 => "TRAFFIC", 2 => "COVERAGE", _ => "TICKETS" }, HorizontalAlignment.Left, -1, 26, ink);
                if (Seed % 4 == 2)
                {
                    // a map with coverage rings
                    DrawRect(new Rect2(20, 56, size.X - 40, size.Y - 76), new Color(0.12f, 0.2f, 0.28f));
                    for (int i = 0; i < 6; i++)
                    {
                        var c = new Vector2(60 + R(i) * (size.X - 120), 90 + R(i + 9) * (size.Y - 150));
                        DrawCircle(c, 30 + R(i + 3) * 40, new Color(good, 0.25f));
                        DrawCircle(c, 5, good);
                    }
                }
                else
                {
                    // bars and a line
                    int n = 12; float w = (size.X - 60) / n;
                    for (int i = 0; i < n; i++)
                    {
                        float h = (0.25f + 0.7f * R(i)) * (size.Y - 120);
                        DrawRect(new Rect2(30 + i * w, size.Y - 30 - h, w * 0.7f, h), i == n - 1 ? accent : new Color(0.3f, 0.45f, 0.7f));
                    }
                    var pts = Enumerable.Range(0, n).Select(i => new Vector2(30 + i * w + w * 0.35f, size.Y - 60 - (0.3f + 0.6f * R(i + 20)) * (size.Y - 140))).ToArray();
                    DrawPolyline(pts, good, 3, true);
                }
                // a button in the corner (the pointer lands on it)
                DrawRect(new Rect2(size.X - 170, 18, 150, 34), accent);
                DrawString(font, new Vector2(size.X - 160, 42), "DETAILS", HorizontalAlignment.Left, -1, 18, Colors.White);
                break;
            case Look.Phone:
                DrawRect(new Rect2(Vector2.Zero, size), new Color(0.10f, 0.18f, 0.32f));
                DrawString(font, new Vector2(size.X / 2 - 40, 40), "09:41", HorizontalAlignment.Left, -1, 30, Colors.White);
                var tones = new[] { new Color(0.95f, 0.48f, 0.15f), new Color(0.3f, 0.75f, 0.45f), new Color(0.3f, 0.55f, 0.9f), new Color(0.9f, 0.3f, 0.35f), new Color(0.95f, 0.8f, 0.25f), new Color(0.6f, 0.4f, 0.8f) };
                for (int i = 0; i < 16; i++)
                {
                    float cw = size.X / 4;
                    var at = new Vector2((i % 4) * cw + cw * 0.18f, 80 + (i / 4) * cw * 1.1f);
                    DrawRect(new Rect2(at, new Vector2(cw * 0.64f, cw * 0.64f)), tones[(i + Seed) % tones.Length]);
                }
                break;
            case Look.Lcd:
                DrawRect(new Rect2(Vector2.Zero, size), new Color(0.62f, 0.70f, 0.52f));
                DrawString(font, new Vector2(10, size.Y * 0.45f), "TELO", HorizontalAlignment.Left, -1, (int)(size.Y * 0.3f), new Color(0.15f, 0.2f, 0.12f));
                DrawString(font, new Vector2(10, size.Y * 0.85f), "12:04  ▮▮▮▯", HorizontalAlignment.Left, -1, (int)(size.Y * 0.25f), new Color(0.15f, 0.2f, 0.12f));
                break;
        }
    }
}
