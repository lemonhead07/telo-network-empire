using Godot;
using Telo.Sim.Content;

namespace Telo.Game.UI;

/// <summary>Full-screen overlay with a styled sheet. Subclasses fill Body and call Rebuild() after state changes.</summary>
public abstract partial class ModalView : Control
{
    protected PanelContainer Sheet = null!;
    protected Label Title = null!;
    protected VBoxContainer Body = null!;
    protected HBoxContainer Footer = null!;

    public event Action? Closed;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right" }) margin.AddThemeConstantOverride($"margin_{side}", 220);
        foreach (var side in new[] { "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{side}", 70);
        AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Sheet = new PanelContainer();
        margin.AddChild(Sheet);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        Sheet.AddChild(v);
        Title = new Label();
        Title.AddThemeFontSizeOverride("font_size", 28);
        v.AddChild(Title);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(scroll);
        Body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Body.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(Body);
        Footer = new HBoxContainer();
        Footer.AddThemeConstantOverride("separation", 10);
        v.AddChild(Footer);
        Visible = false;
    }

    public void Open(InterfaceTier tier)
    {
        Sheet.Theme = TierStyle.For(tier);
        Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Rebuild();
    }

    public void Close()
    {
        if (!Visible) return;
        Visible = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        Closed?.Invoke();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (Visible && e.IsActionPressed("cancel")) { Close(); GetViewport().SetInputAsHandled(); }
    }

    public abstract void Rebuild();

    protected static void Clear(Container c) { foreach (var n in c.GetChildren()) { c.RemoveChild(n); n.QueueFree(); } }

    protected Label Text(Container parent, string text, int size = 0)
    {
        var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        if (size > 0) l.AddThemeFontSizeOverride("font_size", size);
        parent.AddChild(l);
        return l;
    }

    protected Button Btn(Container parent, string text, Action onPress, bool enabled = true)
    {
        var b = new Button { Text = text, Disabled = !enabled };
        b.Pressed += onPress;
        parent.AddChild(b);
        return b;
    }

    protected Button CloseButton() => Btn(Footer, "Close [Esc]", Close);
}
