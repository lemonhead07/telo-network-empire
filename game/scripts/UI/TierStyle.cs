using Godot;
using Telo.Sim.Content;

namespace Telo.Game.UI;

/// <summary>
/// Diegetic information: the interface is only as good as the infrastructure behind it.
/// Paper → green-screen terminal → database forms → dashboards. Same data, different instrument.
/// </summary>
public static class TierStyle
{
    public static Theme For(InterfaceTier tier)
    {
        var (bg, fg, accent, fontNames, size) = tier switch
        {
            InterfaceTier.Paper => (new Color(0.93f, 0.89f, 0.78f), new Color(0.18f, 0.13f, 0.08f), new Color(0.55f, 0.15f, 0.1f), new[] { "Courier New", "Courier", "serif" }, 19),
            InterfaceTier.Terminal => (new Color(0.02f, 0.05f, 0.02f), new Color(0.35f, 1f, 0.45f), new Color(1f, 0.75f, 0.2f), new[] { "DejaVu Sans Mono", "monospace" }, 18),
            InterfaceTier.Database => (new Color(0.78f, 0.78f, 0.76f), new Color(0.05f, 0.05f, 0.1f), new Color(0.1f, 0.2f, 0.6f), new[] { "Liberation Sans", "Arial", "sans-serif" }, 17),
            _ => (new Color(0.07f, 0.09f, 0.14f), new Color(0.88f, 0.92f, 1f), new Color(0.3f, 0.75f, 1f), new[] { "Inter", "Noto Sans", "sans-serif" }, 17),
        };

        var t = new Theme { DefaultFont = new SystemFont { FontNames = fontNames }, DefaultFontSize = size };
        var panel = new StyleBoxFlat { BgColor = bg, BorderColor = accent * 0.8f };
        panel.SetBorderWidthAll(tier == InterfaceTier.Paper ? 1 : 2);
        panel.SetCornerRadiusAll(tier == InterfaceTier.Dashboard ? 10 : 0);
        panel.SetContentMarginAll(18);
        t.SetStylebox("panel", "PanelContainer", panel);
        foreach (var type in new[] { "Label", "Button", "RichTextLabel" }) t.SetColor("font_color", type, fg);
        t.SetColor("default_color", "RichTextLabel", fg);

        StyleBoxFlat Btn(float k)
        {
            var b = new StyleBoxFlat { BgColor = bg.Lerp(accent, k), BorderColor = accent };
            b.SetBorderWidthAll(1); b.SetCornerRadiusAll(tier == InterfaceTier.Dashboard ? 6 : 0);
            b.SetContentMarginAll(6);
            return b;
        }
        t.SetStylebox("normal", "Button", Btn(0.08f));
        t.SetStylebox("hover", "Button", Btn(0.25f));
        t.SetStylebox("pressed", "Button", Btn(0.45f));
        t.SetStylebox("disabled", "Button", Btn(0f));
        t.SetColor("font_hover_color", "Button", fg);
        t.SetColor("font_pressed_color", "Button", fg);
        t.SetColor("font_disabled_color", "Button", fg * new Color(1, 1, 1, 0.4f));
        t.SetColor("accent", "TELO", accent);
        return t;
    }

    public static Color Accent(Theme t) => t.GetColor("accent", "TELO");
}
