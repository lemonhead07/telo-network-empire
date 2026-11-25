using Godot;

namespace Telo.Game.UI;

/// <summary>
/// A short list of choices on a screen in the world: the title letter pinned above the desk, and the pause page on PIP's
/// handset. The mouse clicks a line on the surface; the keys work too: up/down (or W/S) and Enter, or a line's number.
/// Esc goes back a page; on the first page it does whatever <see cref="OnHomeCancel"/> says (nothing, if unset).
/// The pages are data handed in by the caller, rebuilt each time they are shown, so labels are always current.
/// </summary>
public partial class MenuView : ModalView
{
    public sealed record Item(string Text, Action? Do, bool Enabled = true);
    public sealed record Page(string Heading, string? Lead, IReadOnlyList<Item> Items, string? Foot = null);
    /// <summary>The small print at the bottom of every page (how to work it), unless the page has its own.</summary>
    public string Foot { get; set; } = "Click a line, or ↑ ↓ and Enter, or its number.   Esc: back";

    public Func<Page> Home { get; set; } = () => new Page("", null, Array.Empty<Item>());
    /// <summary>Esc on the first page (the pause page resumes the game; the title stays put).</summary>
    public Action? OnHomeCancel { get; set; }
    public int HeadingSize { get; set; } = 44;
    public int LeadSize { get; set; } = 24;
    public int ItemSize { get; set; } = 30;
    public int ItemGap { get; set; } = 8;

    private readonly Stack<Func<Page>> _pages = new();
    private readonly List<(Button Button, Item Item)> _shown = new();
    private int _sel;
    private string _status = "";

    /// <summary>Open a page over this one (Esc or Back returns).</summary>
    public void Go(Func<Page> page) { _pages.Push(page); _sel = -1; _status = ""; Rebuild(); }
    public void Back() { if (_pages.Count > 0) _pages.Pop(); _sel = -1; Rebuild(); }
    /// <summary>Back to the first page with nothing selected yet (each time the menu is brought up).</summary>
    public void Reset() { _pages.Clear(); _sel = -1; _status = ""; }
    /// <summary>A line under the choices saying what just happened ("Saved in slot 2.").</summary>
    public void Say(string text) { _status = text; if (IsInsideTree()) Rebuild(); }

    public override void Rebuild()
    {
        Clear(Body); Clear(Footer); _shown.Clear();
        var page = (_pages.Count > 0 ? _pages.Peek() : Home)();
        Title.Text = page.Heading;
        Title.AddThemeFontSizeOverride("font_size", HeadingSize);
        Body.AddThemeConstantOverride("separation", ItemGap);
        if (page.Lead is { Length: > 0 } lead) Text(Body, lead, LeadSize);
        if (_sel < 0 || _sel >= page.Items.Count || !page.Items[_sel].Enabled) _sel = Next(page.Items, -1, +1);
        var theme = Sheet.Theme;
        for (int i = 0; i < page.Items.Count; i++)
        {
            var item = page.Items[i]; int at = i;
            var b = Btn(Body, "", () => Activate(item), item.Enabled);
            b.Alignment = HorizontalAlignment.Left;
            b.AutowrapMode = TextServer.AutowrapMode.WordSmart;   // a long slot label takes two lines rather than running off the page
            b.FocusMode = Control.FocusModeEnum.None;
            b.AddThemeFontSizeOverride("font_size", ItemSize);
            b.MouseEntered += () => { if (item.Enabled && _sel != at) { _sel = at; Highlight(); } };
            _shown.Add((b, item));
        }
        Highlight();
        if (_status.Length > 0) Text(Body, _status, LeadSize);
        if ((page.Foot ?? Foot) is { Length: > 0 } foot) Text(Footer, foot, Math.Clamp(LeadSize - 6, 16, 24)).Modulate = new Color(1, 1, 1, 0.7f);
    }

    /// <summary>Where a line of the current page is on the screen's own pixels (for automated checks that click it).</summary>
    public Vector2? PointOf(string text) => _shown.FirstOrDefault(s => s.Item.Text.StartsWith(text)) is { Button: { } b } ? b.GetGlobalRect().GetCenter() : null;

    /// <summary>The chosen line looks pressed-in and carries the arrow, whether it was chosen by the keys or the mouse.</summary>
    private void Highlight()
    {
        var hover = Sheet.Theme?.GetStylebox("hover", "Button");
        for (int i = 0; i < _shown.Count; i++)
        {
            var (b, item) = _shown[i];
            bool on = i == _sel;
            b.Text = $"{(on ? "▶" : " ")} {i + 1}  {item.Text}";
            if (on && hover is not null) b.AddThemeStyleboxOverride("normal", hover);
            else b.RemoveThemeStyleboxOverride("normal");
        }
    }

    private static int Next(IReadOnlyList<Item> items, int from, int step)
    {
        for (int k = 1; k <= items.Count; k++)
        {
            int i = ((from + step * k) % items.Count + items.Count) % items.Count;
            if (items[i].Enabled) return i;
        }
        return -1;
    }

    /// <summary>Choices run after the click or key has been dealt with: they may swap the page, cover it, or reload the world.</summary>
    private void Activate(Item item)
    {
        if (!item.Enabled || item.Do is null) return;
        Callable.From(() => { item.Do(); if (IsOpen && IsInsideTree()) Rebuild(); }).CallDeferred();
    }

    public override bool OnKey(InputEventKey key)
    {
        var items = _shown.Select(s => s.Item).ToList();
        switch (key.Keycode)
        {
            case Key.Up or Key.W: _sel = Next(items, _sel, -1); Highlight(); return true;
            case Key.Down or Key.S or Key.Tab: _sel = Next(items, _sel, +1); Highlight(); return true;
            case Key.Enter or Key.KpEnter or Key.Space or Key.E:
                if (_sel >= 0 && _sel < items.Count) Activate(items[_sel]);
                return true;
            case Key.Backspace or Key.Left: if (_pages.Count > 0) Back(); return true;
        }
        int n = key.Keycode is >= Key.Key1 and <= Key.Key9 ? (int)(key.Keycode - Key.Key1)
            : key.Keycode is >= Key.Kp1 and <= Key.Kp9 ? (int)(key.Keycode - Key.Kp1) : -1;
        if (n >= 0 && n < items.Count && items[n].Enabled) { _sel = n; Highlight(); Activate(items[n]); return true; }
        return false;
    }

    public override bool OnCancel()
    {
        if (_pages.Count > 0) { Back(); return true; }
        OnHomeCancel?.Invoke();
        return true;
    }
}
