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
    public event Action? Opened;

    /// <summary>True when this view is drawn on an object in the world (see WorldScreen) rather than over the screen.</summary>
    public bool InWorld { get; set; }
    /// <summary>The player is currently using this view.</summary>
    public bool IsOpen { get; private set; }
    /// <summary>The instrument's era (paper, terminal, database, dashboard): sets the look, and the pointer drawn on it in the world.</summary>
    public InterfaceTier Tier { get; private set; }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        if (!InWorld)
        {
            var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
            AddChild(dim);
            dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right" }) margin.AddThemeConstantOverride($"margin_{side}", InWorld ? 0 : 220);
        foreach (var side in new[] { "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{side}", InWorld ? 0 : 70);
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
        Visible = InWorld;
    }

    /// <summary>Set the look (paper, terminal, …) and redraw. Used for screens that are visible even when nobody is using them.</summary>
    public void Show(InterfaceTier tier)
    {
        Tier = tier;
        Sheet.Theme = TierStyle.For(tier);
        if (!IsOpen) Rebuild();
    }

    public void Open(InterfaceTier tier)
    {
        Tier = tier;
        Sheet.Theme = TierStyle.For(tier);
        Visible = true;
        IsOpen = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Rebuild();
        Opened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Visible = InWorld;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        Closed?.Invoke();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (IsOpen && !InWorld && e.IsActionPressed("cancel")) { Telo.Game.Audio.Sfx.Play("ui.back"); Close(); GetViewport().SetInputAsHandled(); }
    }

    public abstract void Rebuild();

    /// <summary>A key pressed while the player is using this view in the world. Return true if it was used. (Terminals are worked from the keyboard.)</summary>
    public virtual bool OnKey(InputEventKey key) => false;

    /// <summary>Esc while the player is using this view in the world. Return true if the view dealt with it (went back a page);
    /// false steps away from the screen, as Esc always has.</summary>
    public virtual bool OnCancel() => false;

    protected static void Clear(Container c) { foreach (var n in c.GetChildren()) { c.RemoveChild(n); n.QueueFree(); } }

    protected Label Text(Container parent, string text, int size = 0)
    {
        var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        if (size > 0) l.AddThemeFontSizeOverride("font_size", size);
        parent.AddChild(l);
        return l;
    }

    /// <summary>What a button sounds like when it's pressed (EA7): a page on paper, a soft beep on a screen. Null: the view makes its own.</summary>
    protected virtual string? ClickSound => Tier == InterfaceTier.Paper ? "paper.page" : "ui.select";

    /// <param name="sound">This button's own sound instead of <see cref="ClickSound"/>.</param>
    protected Button Btn(Container parent, string text, Action onPress, bool enabled = true, string? sound = null)
    {
        var b = new Button { Text = text, Disabled = !enabled };
        if ((sound ?? ClickSound) is { } click) b.Pressed += () => Telo.Game.Audio.Sfx.Play(click);
        b.Pressed += onPress;
        parent.AddChild(b);
        return b;
    }

    protected Button CloseButton() => Btn(Footer, InWorld ? "Step back [Esc]" : "Close [Esc]", Close, IsOpen, sound: "ui.back");
}
