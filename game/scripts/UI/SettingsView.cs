using Godot;

namespace Telo.Game.UI;

/// <summary>
/// The settings page: mouse, screen, graphics, the length of a day, volume (GameSettings). A page drawn on an object
/// like every other screen, in that object's look. Worked with the keyboard (arrows or W/S to choose, arrows or A/D to change,
/// Enter to step through, a number to jump to a line, 0 or Esc to go back) or by clicking the arrows.
/// Every change takes effect at once and is saved. Back closes the view (ModalView.Closed).
/// </summary>
public partial class SettingsView : ModalView
{
    private sealed class Row
    {
        public string Name = "";
        public Func<string> Value = () => "";
        /// <summary>0..1 for a row shown as a bar (mouse, volume); null for a row shown as a word.</summary>
        public Func<float>? Fill;
        /// <summary>Move the setting one step (-1 or +1); the flag says whether it may wrap round (Enter steps through).</summary>
        public Action<int, bool> Step = (_, _) => { };
        public Func<string> Hint = () => "";
        public PanelContainer Panel = null!;
        public Label Marker = null!, ValueLabel = null!;
        public ColorRect[] Cells = Array.Empty<ColorRect>();
    }

    private readonly List<Row> _rows;
    private int _sel;
    private Label _hint = null!;

    public SettingsView()
    {
        static int Cycle(int cur, int count, int d, bool wrap) => wrap || count == 2 ? ((cur + d) % count + count) % count : Math.Clamp(cur + d, 0, count - 1);
        _rows = new()
        {
            new Row
            {
                Name = "Mouse speed", Value = () => $"{GameSettings.Mouse}", Fill = () => GameSettings.Mouse / (float)GameSettings.MouseSteps,
                Step = (d, _) => GameSettings.Mouse = Math.Clamp(GameSettings.Mouse + d, 1, GameSettings.MouseSteps),
                Hint = () => "How far you turn for each nudge of the mouse. 5 is how it was made.",
            },
            new Row
            {
                Name = "Invert up and down", Value = () => GameSettings.InvertY ? "On" : "Off",
                Step = (_, _) => GameSettings.InvertY = !GameSettings.InvertY,
                Hint = () => GameSettings.InvertY ? "Push the mouse away to look down, like an aeroplane's stick." : "Push the mouse away to look up.",
            },
            new Row
            {
                Name = "Screen", Value = () => GameSettings.Fullscreen ? "Full screen" : "In a window",
                Step = (_, _) => GameSettings.Fullscreen = !GameSettings.Fullscreen,
                Hint = () => GameSettings.Fullscreen ? "The whole screen." : "A window you can move about and put beside other things.",
            },
            new Row
            {
                Name = "Graphics", Value = () => GameSettings.QualityName(GameSettings.Graphics),
                Step = (d, wrap) => GameSettings.Graphics = (GraphicsQuality)Cycle((int)GameSettings.Graphics, 3, d, wrap),
                Hint = () => GameSettings.Graphics switch
                {
                    GraphicsQuality.Low => "For older or smaller computers: fewer pixels drawn, short hard shadows, distant trees fade sooner. Pages stay sharp.",
                    GraphicsQuality.High => "Smoother edges, longer and softer shadows, trees out to the horizon. Wants a strong graphics card.",
                    _ => "How the town is meant to look.",
                },
            },
            new Row
            {
                Name = "Length of a day", Value = () => $"{GameSettings.DayMinutes} minutes",
                Step = (d, wrap) => GameSettings.DayMinutes = GameSettings.DayMinuteChoices[Cycle(Array.IndexOf(GameSettings.DayMinuteChoices, GameSettings.DayMinutes), GameSettings.DayMinuteChoices.Length, d, wrap)],
                Hint = () => GameSettings.DayMinutes == 8
                    ? "Real minutes from six in the morning till ten at night. Eight is the pace the game was made for."
                    : "A slower day: more time for each job before the sun goes down, and the whole game takes longer to play.",
            },
            new Row
            {
                Name = "Volume", Value = () => GameSettings.Volume == 0 ? "Off" : $"{GameSettings.Volume}%", Fill = () => GameSettings.Volume / 100f,
                Step = (d, _) => GameSettings.Volume = Math.Clamp(GameSettings.Volume + d * 10, 0, 100),
                Hint = () => "Everything you hear, all at once.",
            },
        };
    }

    private Color Fg => Sheet.Theme?.GetColor("font_color", "Label") ?? Colors.White;
    private Color Accent => Sheet.Theme is { } t ? TierStyle.Accent(t) : Colors.Orange;

    public override void Rebuild()
    {
        Clear(Body); Clear(Footer);
        Title.Text = "SETTINGS";
        Title.AddThemeFontSizeOverride("font_size", 36);
        Text(Body, "Changes take effect at once, and are kept for next time.", 20).Modulate = new Color(1, 1, 1, 0.75f);

        for (int i = 0; i < _rows.Count; i++) Body.AddChild(MakeRow(i));

        _hint = Text(Body, "", 21);
        _hint.CustomMinimumSize = new Vector2(0, 64);

        Btn(Footer, "Back [Esc]", Close).AddThemeFontSizeOverride("font_size", 22);
        Btn(Footer, "Defaults", () => { GameSettings.ResetToDefaults(); Changed(); }).AddThemeFontSizeOverride("font_size", 22);
        var keys = new Label { Text = "↑↓ choose   ←→ change   1–6 jump to a line", SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Modulate = new Color(1, 1, 1, 0.7f) };
        keys.AddThemeFontSizeOverride("font_size", 19);
        Footer.AddChild(keys);
        Refresh();
    }

    private PanelContainer MakeRow(int i)
    {
        var r = _rows[i];
        r.Panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        r.Panel.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { if (_sel != i) Telo.Game.Audio.Sfx.Play("ui.move"); _sel = i; Refresh(); } };
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 14);
        r.Panel.AddChild(h);

        r.Marker = Small(h, "", 26, 34);
        Small(h, $"[{i + 1}]", 22, 48).Modulate = new Color(1, 1, 1, 0.6f);
        var name = Small(h, r.Name, 26, 0);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        Arrow(h, "◀", () => { _sel = i; Change(-1, false); });
        var value = new HBoxContainer { CustomMinimumSize = new Vector2(330, 0), Alignment = BoxContainer.AlignmentMode.Center };
        value.AddThemeConstantOverride("separation", 4);
        h.AddChild(value);
        if (r.Fill is not null)
        {
            r.Cells = new ColorRect[10];
            for (int c = 0; c < r.Cells.Length; c++)
            {
                r.Cells[c] = new ColorRect { CustomMinimumSize = new Vector2(20, 24), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
                value.AddChild(r.Cells[c]);
            }
            value.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0) });
        }
        r.ValueLabel = Small(value, "", 26, r.Fill is null ? 330 : 70);
        r.ValueLabel.HorizontalAlignment = r.Fill is null ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        Arrow(h, "▶", () => { _sel = i; Change(+1, false); });
        return r.Panel;
    }

    private static Label Small(Container parent, string text, int size, float minWidth)
    {
        var l = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center, CustomMinimumSize = new Vector2(minWidth, 0), MouseFilter = MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", size);
        parent.AddChild(l);
        return l;
    }

    private void Arrow(Container parent, string text, Action onPress)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(52, 44), FocusMode = FocusModeEnum.None };
        b.AddThemeFontSizeOverride("font_size", 22);
        b.Pressed += onPress;
        parent.AddChild(b);
    }

    /// <summary>Show the current values and which line is chosen, without rebuilding the page.</summary>
    private void Refresh()
    {
        if (_hint is null || !IsInstanceValid(_hint)) return;
        Color fg = Fg, accent = Accent;
        for (int i = 0; i < _rows.Count; i++)
        {
            var r = _rows[i];
            bool on = i == _sel && IsOpen;
            var box = new StyleBoxFlat { BgColor = on ? new Color(accent, 0.16f) : new Color(0, 0, 0, 0), BorderColor = accent };
            box.BorderWidthLeft = on ? 5 : 0;
            box.SetContentMarginAll(6); box.ContentMarginLeft = 10;
            r.Panel.AddThemeStyleboxOverride("panel", box);
            r.Marker.Text = on ? "▸" : "";
            r.Marker.AddThemeColorOverride("font_color", accent);
            r.ValueLabel.Text = r.Value();
            if (r.Fill is { } fill)
            {
                int lit = Mathf.RoundToInt(fill() * r.Cells.Length);
                for (int c = 0; c < r.Cells.Length; c++) r.Cells[c].Color = c < lit ? (on ? accent : fg) : new Color(fg, 0.18f);
            }
        }
        _hint.Text = _rows[Math.Clamp(_sel, 0, _rows.Count - 1)].Hint();
    }

    private void Change(int d, bool wrap)
    {
        Telo.Game.Audio.Sfx.Play("ui.select");
        _rows[_sel].Step(d, wrap);
        Changed();
    }

    private void Changed()
    {
        GameSettings.Apply();
        GameSettings.Save();
        Refresh();
    }

    public override bool OnKey(InputEventKey key)
    {
        switch (key.PhysicalKeycode)
        {
            case Key.Up or Key.W: _sel = (_sel + _rows.Count - 1) % _rows.Count; Refresh(); Telo.Game.Audio.Sfx.Play("ui.move"); return true;
            case Key.Down or Key.S: _sel = (_sel + 1) % _rows.Count; Refresh(); Telo.Game.Audio.Sfx.Play("ui.move"); return true;
            case Key.Left or Key.A: Change(-1, false); return true;
            case Key.Right or Key.D: Change(+1, false); return true;
            case Key.Enter or Key.KpEnter or Key.Space: Change(+1, true); return true;
            case Key.Key0 or Key.Kp0 or Key.Backspace: Telo.Game.Audio.Sfx.Play("ui.back"); Close(); return true;
        }
        int n = key.PhysicalKeycode switch
        {
            >= Key.Key1 and <= Key.Key9 => (int)(key.PhysicalKeycode - Key.Key0),
            >= Key.Kp1 and <= Key.Kp9 => (int)(key.PhysicalKeycode - Key.Kp0),
            _ => 0,
        };
        if (n < 1 || n > _rows.Count) return false;
        _sel = n - 1; Refresh();
        Telo.Game.Audio.Sfx.Play("ui.move");
        return true;
    }

    /// <summary>Off a world screen (an overlay), keys come here instead of through WorldScreen.</summary>
    public override void _UnhandledInput(InputEvent e)
    {
        if (IsOpen && !InWorld && e is InputEventKey { Pressed: true, Echo: false } k && k.PhysicalKeycode != Key.Escape && OnKey(k)) { GetViewport().SetInputAsHandled(); return; }
        base._UnhandledInput(e);
    }
}
