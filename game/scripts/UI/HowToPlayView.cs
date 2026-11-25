using Godot;

namespace Telo.Game.UI;

/// <summary>
/// How to play, from PIP: seven short pages, the whole manual. Turned with the arrow keys (or A/D, Enter, a page number)
/// or the buttons; 0 or Esc goes back. Back closes the view (ModalView.Closed). Keys named here are the real bindings
/// (InputSetup); the god keys are left out on purpose.
/// </summary>
public partial class HowToPlayView : ModalView
{
    private sealed record Page(string Heading, (string Keys, string What)[] Keys, string Text);

    private static readonly Page[] Pages =
    {
        new("Hello. I'm PIP.", new[] { ("← →", "turn my pages"), ("Esc", "put me down") },
            "I'm the handset on your desk, and the phone in your pocket. You've rented a room, wired a frame, and promised this town " +
            "a telephone network. Nobody asked you to. They'll be glad you did, eventually.\n\n" +
            "These pages are the whole manual. The rest you learn by doing, and I'll pipe up the first time you do each thing."),

        new("Looking and walking", new[] { ("Mouse", "look about"), ("W A S D", "walk"), ("Shift", "hurry"), ("Space", "hop") },
            "Look straight at a thing to see what it's for: the words in the middle of your view say what E will do with it. " +
            "If nothing comes up, it's scenery. Lovely scenery, mind.\n\n" +
            "Mouse too quick, or too slow? There's a dial for it on the settings page."),

        new("Your hands", new[] { ("E", "use what you're looking at"), ("Hold E", "work that takes time: done when the bar is full"),
                ("R", "the other choice, when there is one: often the quick way"), ("G", "put down what you're carrying"), ("Esc", "step back from anything you've leaned into") },
            "The words in the middle of your view say what E and R will do. Keep holding E and you carry straight on to the next step.\n\n" +
            "The quick way (a bodge, a thump, an old wire) is quick today and trouble in a few weeks. It never fails on the spot; " +
            "it fails later, at somebody's house. I keep a note of the date."),

        new("The van and its kit", new[] { ("E", "at the driver's door: drive. E again to get out"), ("W S", "go and stop"), ("A D", "steer") },
            "Roads are twice as fast as grass. The kit rides on the van: cable drum, ladder, toolbox, a boxed modem. " +
            "One thing in your hands at a time, and the drum and the ladder slow you down. Anything lying within a few steps of you " +
            "counts as to hand. Use the van to put a thing back."),

        new("Customers and installs", Array.Empty<(string, string)>(),
            "People sign up on the answering machine on your desk, or when you knock on their door. Their house gets the ▼.\n\n" +
            "At the house, yellow markers show the work: drop wire, junction box, socket, the cable along the skirting, the modem. " +
            "Outside first, top to bottom, then indoors. Lift their telephone and listen for a dial tone, then sign the job sheet by the door. " +
            "Modems come from Sol's Electronics at the east end of the street."),

        new("The switchboard", Array.Empty<(string, string)>(),
            "A house isn't on the network until it's wired in at the switchboard in your room. Lean in: the mouse is your hand.\n\n" +
            "Take a jumper off the hook, touch the customer's terminal, then the port their tag names. A smudged tag? The tone probe reads it. " +
            "Then throw the test switch. A lamp that lights is a live line, and a live line pays every month. Dark means the wrong port " +
            "or a bad one. Two blinking red means crossed: somebody can hear a stranger."),

        new("What next, and when", Array.Empty<(string, string)>(),
            "The ▶ line on my screen always says what wants doing next, and a ▼ hangs over the place to do it. " +
            "A $ is an odd job going; a ! is a fault on somebody's line.\n\n" +
            "Time only passes as you live it. The day runs from six in the morning till ten at night: be in bed by ten, " +
            "or it comes off tomorrow. Sleeping ends the day and saves the game, and when nothing needs you the bed lets days " +
            "go by until something does.\n\nThat's everything. Off you go. I'll be in your pocket."),
    };

    private int _page;

    /// <summary>Which page is showing (0 = the first). Hosts can open the book at a page.</summary>
    public int PageIndex { get => _page; set { _page = Math.Clamp(value, 0, Pages.Length - 1); if (Body is not null) Rebuild(); } }
    public static int PageCount => Pages.Length;

    private Color Fg => Sheet.Theme?.GetColor("font_color", "Label") ?? Colors.White;
    private Color Accent => Sheet.Theme is { } t ? TierStyle.Accent(t) : Colors.Orange;

    public override void Rebuild()
    {
        Clear(Body); Clear(Footer);
        var p = Pages[_page];
        Title.Text = $"HOW TO PLAY     {_page + 1} of {Pages.Length}";
        Title.AddThemeFontSizeOverride("font_size", 26);
        Title.Modulate = new Color(1, 1, 1, 0.75f);

        var heading = Text(Body, p.Heading, 40);
        heading.AddThemeColorOverride("font_color", Accent);

        if (p.Keys.Length > 0)
        {
            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", 18);
            grid.AddThemeConstantOverride("v_separation", 10);
            Body.AddChild(grid);
            foreach (var (keys, what) in p.Keys)
            {
                var caps = new HBoxContainer { CustomMinimumSize = new Vector2(190, 0) };
                caps.AddThemeConstantOverride("separation", 6);
                grid.AddChild(caps);
                foreach (var k in keys.StartsWith("Hold ") ? new[] { keys } : keys.Split(' ')) caps.AddChild(Cap(k));
                var l = new Label { Text = what, VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(760, 0) };
                l.AddThemeFontSizeOverride("font_size", 25);
                grid.AddChild(l);
            }
            Body.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        }
        Text(Body, p.Text, 25);

        var prev = Btn(Footer, "◀ Previous", () => Turn(-1), _page > 0);
        var next = Btn(Footer, _page < Pages.Length - 1 ? "Next ▶" : "Done", () => { if (_page < Pages.Length - 1) Turn(+1); else Close(); });
        foreach (var b in new[] { prev, next }) { b.AddThemeFontSizeOverride("font_size", 22); b.FocusMode = FocusModeEnum.None; }
        var dots = new Label { Text = string.Join(" ", Enumerable.Range(0, Pages.Length).Select(i => i == _page ? "●" : "○")), SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        dots.AddThemeFontSizeOverride("font_size", 22);
        Footer.AddChild(dots);
        var back = Btn(Footer, "Back [Esc]", Close);
        back.AddThemeFontSizeOverride("font_size", 22); back.FocusMode = FocusModeEnum.None;
    }

    /// <summary>A key drawn as a key: its name in a little box.</summary>
    private Control Cap(string name)
    {
        var box = new PanelContainer();
        var style = new StyleBoxFlat { BgColor = new Color(Fg, 0.07f), BorderColor = new Color(Fg, 0.8f) };
        style.SetBorderWidthAll(2); style.BorderWidthBottom = 4; style.SetCornerRadiusAll(4);
        style.ContentMarginLeft = style.ContentMarginRight = 10; style.ContentMarginTop = 2; style.ContentMarginBottom = 2;
        box.AddThemeStyleboxOverride("panel", style);
        var l = new Label { Text = name, HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", 22);
        box.AddChild(l);
        box.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        return box;
    }

    private void Turn(int d)
    {
        _page = Math.Clamp(_page + d, 0, Pages.Length - 1);
        Rebuild();
    }

    public override bool OnKey(InputEventKey key)
    {
        switch (key.PhysicalKeycode)
        {
            case Key.Left or Key.A or Key.Up or Key.W or Key.Pageup: Turn(-1); return true;
            case Key.Right or Key.D or Key.Down or Key.S or Key.Pagedown or Key.Space: Turn(+1); return true;
            case Key.Enter or Key.KpEnter: if (_page < Pages.Length - 1) Turn(+1); else Close(); return true;
            case Key.Key0 or Key.Kp0 or Key.Backspace: Close(); return true;
        }
        int n = key.PhysicalKeycode switch
        {
            >= Key.Key1 and <= Key.Key9 => (int)(key.PhysicalKeycode - Key.Key0),
            >= Key.Kp1 and <= Key.Kp9 => (int)(key.PhysicalKeycode - Key.Kp0),
            _ => 0,
        };
        if (n < 1 || n > Pages.Length) return false;
        _page = n - 1; Rebuild();
        return true;
    }

    /// <summary>Off a world screen (an overlay), keys come here instead of through WorldScreen.</summary>
    public override void _UnhandledInput(InputEvent e)
    {
        if (IsOpen && !InWorld && e is InputEventKey { Pressed: true, Echo: false } k && k.PhysicalKeycode != Key.Escape && OnKey(k)) { GetViewport().SetInputAsHandled(); return; }
        base._UnhandledInput(e);
    }
}
