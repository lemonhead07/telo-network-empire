using Godot;
using Telo.Sim.Net;
using Telo.Sim.World;

namespace Telo.Game.World;

/// <summary>
/// The switchboard as an object: subscriber terminals with paper tags down the left, Board ports with lamps down the right,
/// real jumper wires hanging between them, and your tools on the shelf. Lean in and the mouse is your hand.
/// It only shows and drives the sim's SwitchRules; what a wire does is decided there.
/// </summary>
public partial class SwitchboardStation : Node3D
{
    public const float W = 1.6f, H = 1.36f, Face = 0.04f;
    private enum Hand { Empty, Probe, TestSet, Wire, Dirty }
    private enum Spot { Terminal, Port, Probe, TestSet, Hook, DirtyHook, Switch, Panel }
    private sealed record Hot(Rect2 Area, Spot Kind, int Index, string Key);

    private static readonly Color[] WireColours =
    {
        new(0.8f, 0.15f, 0.12f), new(0.15f, 0.3f, 0.8f), new(0.9f, 0.8f, 0.2f), new(0.15f, 0.6f, 0.25f), new(0.9f, 0.9f, 0.88f), new(0.85f, 0.45f, 0.1f),
    };
    private static readonly Color Dirty = new(0.36f, 0.33f, 0.29f), Ply = new(0.72f, 0.58f, 0.4f), Brass = new(0.8f, 0.66f, 0.3f), Paper = new(0.95f, 0.93f, 0.85f), Ink = new(0.12f, 0.1f, 0.3f);

    public Simulation Sim { get; set; } = null!;
    /// <summary>Customer ids on a call to the Board right now (their port lamps burn bright).</summary>
    public Func<HashSet<string>> OnLineNow { get; set; } = () => new();
    /// <summary>PIP says something the first time it comes up.</summary>
    public Action<string> Tip { get; set; } = _ => { };
    public event Action? Opened, Closed;
    public bool IsOpen { get; private set; }
    /// <summary>What the hand is over and what a click would do. Shown as the usual small prompt.</summary>
    public string HoverText { get; private set; } = "";
    public Interactable Use { get; }

    private readonly List<Hot> _hots = new();
    private readonly Dictionary<int, StandardMaterial3D> _lamps = new();
    private Node3D _static = new(), _dynamic = new(), _handNode = new();
    private Hand _hand;
    private string? _wireFrom;
    private string _sig = "";
    private int _builtStage = -1;
    private Vector2 _at;
    private Dictionary<string, ThrowResult> _lastThrow = new();
    private double _flash;

    public SwitchboardStation()
    {
        Name = "Switchboard";
        AddChild(_static); AddChild(_dynamic); AddChild(_handNode);
        Use = new Interactable();
        Use.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(W, H, 0.12f) } });
        Use.OnUse = Open;
        AddChild(Use);
    }

    // ---- layout ----------------------------------------------------------------------------------
    private BoardState B => Sim.State.Switch;
    private float Step => 0.95f / (SwitchRules.Size(B) + 1);
    private float RowY(int row) => 0.5f - row * Step;
    private int PortRow(int port) => SwitchRules.IsSpare(B, port) ? SwitchRules.Size(B) + (port - SwitchRules.SpareBase) : port;
    private Vector3 TerminalAt(int row) => new(-0.4f, RowY(row), Face + 0.02f);
    private Vector3 PortAt(int port) => new(0.3f, RowY(PortRow(port)), Face + 0.02f);
    private static string PortName(int port) => port >= SwitchRules.SpareBase ? $"S{port - SwitchRules.SpareBase + 1}" : $"{port + 1}";
    private static readonly Vector2 ProbeAt = new(-0.64f, -0.58f), TestAt = new(-0.38f, -0.58f), HookAt = new(-0.12f, -0.53f), DirtyAt = new(0.06f, -0.53f), SwitchAt = new(0.62f, -0.52f), PanelAt = new(0.64f, 0.625f);

    private void BuildStatic()
    {
        foreach (var old in _static.GetChildren()) old.QueueFree();
        _hots.Clear(); _lamps.Clear();
        _builtStage = B.Stage;
        var lp = new LowPoly().Box(Vector3.Zero, new Vector3(W, H, Face), Ply).Box(new Vector3(0, -0.64f, 0.1f), new Vector3(W, 0.04f, 0.22f), Ply * 0.85f);
        void Text(string t, Vector3 at, float size, Color col) =>
            _static.AddChild(new Label3D { Text = t, Position = at, FontSize = 48, PixelSize = size / 48f, Modulate = col, OutlineSize = 0 });
        var dark = new Color(0.15f, 0.1f, 0.05f);
        Text("SUBSCRIBERS", new Vector3(-0.58f, 0.6f, Face), 0.045f, dark);
        Text("BOARD PORTS", new Vector3(0.3f, 0.6f, Face), 0.04f, dark);

        int n = SwitchRules.Size(B);
        float h = Mathf.Min(0.07f, Step * 0.72f);
        for (int row = 0; row < n; row++)
        {
            lp.Box(new Vector3(-0.62f, RowY(row), Face + 0.004f), new Vector3(0.34f, h, 0.008f), Paper);
            lp.Box(TerminalAt(row), new Vector3(0.045f, Mathf.Min(0.045f, h), 0.03f), Brass);
            _hots.Add(new Hot(new Rect2(-0.8f, RowY(row) - Step / 2, 0.45f, Step), Spot.Terminal, row, $"terminal:{row}"));
        }
        foreach (int port in SwitchRules.PortList(B))
        {
            lp.Box(PortAt(port), new Vector3(0.045f, Mathf.Min(0.045f, h), 0.03f), SwitchRules.IsSpare(B, port) ? new Color(0.7f, 0.72f, 0.75f) : Brass);
            Text(PortName(port), PortAt(port) + new Vector3(0.09f, 0, -0.01f), Mathf.Min(0.04f, h * 0.8f), dark);
            var mat = new StandardMaterial3D { AlbedoColor = Colors.Black, EmissionEnabled = true, Emission = Colors.Black, EmissionEnergyMultiplier = 2 };
            _lamps[port] = mat;
            _static.AddChild(new MeshInstance3D { Position = PortAt(port) + new Vector3(0.19f, 0, 0), Mesh = new BoxMesh { Size = new Vector3(0.045f, Mathf.Min(0.045f, h), 0.03f) }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            _hots.Add(new Hot(new Rect2(0.24f, RowY(PortRow(port)) - Step / 2, 0.32f, Step), Spot.Port, port, $"port:{port}"));
        }

        // the shelf: tone probe, test set, clean jumpers on a hook, old dirty ones beside them, the test switch
        lp.Box(new Vector3(ProbeAt.X, ProbeAt.Y, 0.1f), new Vector3(0.2f, 0.045f, 0.045f), new Color(0.95f, 0.8f, 0.15f)).Box(new Vector3(ProbeAt.X + 0.12f, ProbeAt.Y, 0.1f), new Vector3(0.05f, 0.018f, 0.018f), new Color(0.7f, 0.7f, 0.72f));
        lp.Box(new Vector3(TestAt.X, TestAt.Y, 0.1f), new Vector3(0.2f, 0.065f, 0.065f), new Color(0.1f, 0.1f, 0.11f));
        lp.Box(new Vector3(HookAt.X, HookAt.Y + 0.06f, Face + 0.02f), new Vector3(0.16f, 0.018f, 0.03f), Brass).Box(new Vector3(DirtyAt.X, DirtyAt.Y + 0.06f, Face + 0.02f), new Vector3(0.1f, 0.018f, 0.03f), Brass * 0.6f);
        for (int i = 0; i < 5; i++) lp.Box(new Vector3(HookAt.X - 0.06f + i * 0.03f, HookAt.Y, Face + 0.02f), new Vector3(0.011f, 0.11f, 0.011f), WireColours[i]);
        for (int i = 0; i < 3; i++) lp.Box(new Vector3(DirtyAt.X - 0.03f + i * 0.03f, DirtyAt.Y - i * 0.006f, Face + 0.02f), new Vector3(0.011f, 0.1f, 0.011f), Dirty);
        lp.Box(new Vector3(SwitchAt.X, SwitchAt.Y, Face + 0.02f), new Vector3(0.15f, 0.19f, 0.03f), new Color(0.15f, 0.15f, 0.16f)).Box(new Vector3(SwitchAt.X, SwitchAt.Y + 0.05f, 0.11f), new Vector3(0.03f, 0.15f, 0.09f), Brass);
        var light = new Color(0.95f, 0.93f, 0.85f);
        Text("probe", new Vector3(ProbeAt.X, ProbeAt.Y + 0.05f, 0.1f), 0.026f, dark); Text("test set", new Vector3(TestAt.X, TestAt.Y + 0.06f, 0.1f), 0.026f, dark);
        Text("jumpers", new Vector3(HookAt.X, HookAt.Y + 0.09f, Face + 0.03f), 0.026f, dark); Text("old", new Vector3(DirtyAt.X, DirtyAt.Y + 0.09f, Face + 0.03f), 0.026f, dark);
        Text("TEST", new Vector3(SwitchAt.X, SwitchAt.Y - 0.07f, Face + 0.04f), 0.03f, light);
        _hots.Add(new Hot(new Rect2(ProbeAt.X - 0.12f, ProbeAt.Y - 0.05f, 0.26f, 0.12f), Spot.Probe, 0, "probe"));
        _hots.Add(new Hot(new Rect2(TestAt.X - 0.11f, TestAt.Y - 0.05f, 0.22f, 0.13f), Spot.TestSet, 0, "testset"));
        _hots.Add(new Hot(new Rect2(HookAt.X - 0.085f, HookAt.Y - 0.07f, 0.17f, 0.18f), Spot.Hook, 0, "hook"));
        _hots.Add(new Hot(new Rect2(DirtyAt.X - 0.055f, DirtyAt.Y - 0.07f, 0.11f, 0.18f), Spot.DirtyHook, 0, "dirty"));
        _hots.Add(new Hot(new Rect2(SwitchAt.X - 0.09f, SwitchAt.Y - 0.11f, 0.18f, 0.24f), Spot.Switch, 0, "switch"));
        if (B.Stage < SwitchRules.MaxStage)
        {
            lp.Box(new Vector3(PanelAt.X, PanelAt.Y, Face + 0.005f), new Vector3(0.28f, 0.06f, 0.01f), new Color(0.3f, 0.3f, 0.32f));
            Text($"+ PANEL ${SwitchRules.PanelPrice}", new Vector3(PanelAt.X, PanelAt.Y, Face + 0.012f), 0.026f, light);
            _hots.Add(new Hot(new Rect2(PanelAt.X - 0.14f, PanelAt.Y - 0.03f, 0.28f, 0.06f), Spot.Panel, 0, "panel"));
        }
        _static.AddChild(lp.Instance());
        _sig = "";
    }

    /// <summary>Catch the object up with the sim: tags, wires and lamps.</summary>
    public void Sync()
    {
        var s = Sim.State;
        if (_builtStage != B.Stage) BuildStatic();
        string sig = string.Join("|", B.Positions.Select(p => $"{p.CustomerId},{p.AssignedPort},{p.TagRead},{p.Jumper},{p.HotWired},{p.Loose},{SwitchRules.IsLive(s, p)}"));
        if (sig != _sig)
        {
            _sig = sig;
            foreach (var n in _dynamic.GetChildren()) n.QueueFree();
            float h = Mathf.Min(0.07f, Step * 0.72f);
            for (int row = 0; row < B.Positions.Count; row++)
            {
                var p = B.Positions[row];
                string name = SwitchRules.Who(s, p)?.Name ?? "?";
                if (name.Length > 15) name = name[..15];
                _dynamic.AddChild(new Label3D
                {
                    Text = p.TagRead ? $"{name}  ›{PortName(p.AssignedPort)}" : "▒▒▒▒▒▒▒  ›?", Position = new Vector3(-0.62f, RowY(row), Face + 0.01f),
                    FontSize = 40, PixelSize = Mathf.Min(0.034f, h * 0.62f) / 40f, Modulate = Ink, OutlineSize = 0,
                });
                if (p.Jumper < 0) continue;
                var to = PortAt(p.Jumper) + (p.Loose ? new Vector3(-0.035f, -0.03f, 0.03f) : Vector3.Zero);   // a loose jumper hangs out of its jack
                AddWire(_dynamic, TerminalAt(row) + new Vector3(0, 0, 0.015f), to + new Vector3(0, 0, 0.015f), p.HotWired ? Dirty : WireColours[row % WireColours.Length], p.Loose ? 0.16f : 0.07f);
            }
        }
        var onLine = OnLineNow();
        bool blink = (int)(_flash * 6) % 2 == 0;
        foreach (var (port, mat) in _lamps)
        {
            var on = SwitchRules.OnPort(s, port);
            Color col = new(0.05f, 0.05f, 0.05f); bool lit = false;
            if (on is not null && _lastThrow.TryGetValue(on.CustomerId, out var r) && r == ThrowResult.Crossed && _flash > 0) { col = new Color(1f, 0.1f, 0.1f); lit = blink; }
            else if (on is not null && SwitchRules.IsLive(s, on)) { col = new Color(1f, 0.72f, 0.18f); lit = onLine.Contains(on.CustomerId); if (!lit) col *= 0.45f; }
            else if (SwitchRules.KnownGood(s, port) is { } good) col = good ? new Color(0.1f, 0.4f, 0.15f) : new Color(0.75f, 0.08f, 0.06f);
            mat.AlbedoColor = col; mat.Emission = lit ? col : Colors.Black;
        }
    }

    public override void _Process(double delta)
    {
        if (_flash > 0) { _flash -= delta; Sync(); }
    }

    private static void AddWire(Node3D parent, Vector3 from, Vector3 to, Color col, float sag)
    {
        var prev = from;
        for (int k = 1; k <= 8; k++)
        {
            float t = k / 8f, bow = Mathf.Sin(t * Mathf.Pi);
            var pt = from.Lerp(to, t) + new Vector3(0, -sag * bow, sag * 1.1f * bow);
            var d = pt - prev; float len = d.Length();
            if (len < 1e-4f) continue;
            var z = d / len; var x = (Mathf.Abs(z.Y) > 0.95f ? Vector3.Right : Vector3.Up).Cross(z).Normalized();
            var seg = new LowPoly().Box(Vector3.Zero, new Vector3(0.012f, 0.012f, len + 0.004f), col).Instance();
            seg.Transform = new Transform3D(new Basis(x, z.Cross(x), z), (pt + prev) / 2);
            seg.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            parent.AddChild(seg);
            prev = pt;
        }
    }

    // ---- using it --------------------------------------------------------------------------------
    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true; _hand = Hand.Empty; _wireFrom = null;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Sync(); DrawHand();
        Tip("switch");
        Opened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false; _hand = Hand.Empty; _wireFrom = null; HoverText = "";
        Input.MouseMode = Input.MouseModeEnum.Captured;
        DrawHand();
        Closed?.Invoke();
    }

    /// <summary>Where a camera should sit to work at the board.</summary>
    public Transform3D LeanPose(float fovDeg, float aspect)
    {
        float tan = Mathf.Tan(Mathf.DegToRad(fovDeg) / 2);
        float dist = Mathf.Max((H + 0.16f) / 2 / tan, W / 2 / (tan * aspect)) / 0.9f;
        return new Transform3D(GlobalBasis.Orthonormalized(), GlobalPosition - GlobalBasis.Y.Normalized() * 0.03f + GlobalBasis.Z.Normalized() * dist);
    }

    /// <summary>Where on the player's display a named spot ("hook", "switch", "terminal:0", "port:3") currently appears. For automated checks.</summary>
    public Vector2 DisplayPointFor(string key, Camera3D cam)
    {
        var c = _hots.First(h => h.Key == key).Area.GetCenter();
        return cam.UnprojectPosition(GlobalTransform * new Vector3(c.X, c.Y, Face));
    }

    public override void _Input(InputEvent e)
    {
        if (!IsOpen) return;
        if (e.IsActionPressed("cancel")) { Close(); GetViewport().SetInputAsHandled(); return; }
        if (e is not InputEventMouse m) return;
        var cam = GetViewport().GetCamera3D();
        if (cam is null) return;
        var inv = GlobalTransform.AffineInverse();
        var o = inv * cam.ProjectRayOrigin(m.Position);
        var d = inv.Basis * cam.ProjectRayNormal(m.Position);
        if (Mathf.Abs(d.Z) < 1e-5f) return;
        var hit = o + d * ((Face - o.Z) / d.Z);
        _at = new Vector2(hit.X, hit.Y);
        var hot = _hots.FirstOrDefault(h => h.Area.HasPoint(_at));
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && hot is not null) { Click(hot); Sync(); }
        HoverText = hot is null ? HandText() : Describe(hot);
        DrawHand();
        GetViewport().SetInputAsHandled();
    }

    private void Click(Hot hot)
    {
        var s = Sim.State;
        BoardPosition? At(int row) => row < B.Positions.Count ? B.Positions[row] : null;
        void EmptyHandOn(BoardPosition p)
        {
            if (p.Loose) { if (SwitchRules.Reseat(s, p.CustomerId)) Tip("reseat"); }
            else if (p.Jumper >= 0) { if (SwitchRules.Pull(s, p.CustomerId) is not null) Tip("pulled"); }
        }
        switch (hot.Kind)
        {
            case Spot.Probe: _hand = _hand == Hand.Probe ? Hand.Empty : Hand.Probe; _wireFrom = null; break;
            case Spot.TestSet: _hand = _hand == Hand.TestSet ? Hand.Empty : Hand.TestSet; _wireFrom = null; break;
            case Spot.Hook: _hand = _hand == Hand.Wire ? Hand.Empty : Hand.Wire; _wireFrom = null; break;
            case Spot.DirtyHook: _hand = _hand == Hand.Dirty ? Hand.Empty : Hand.Dirty; _wireFrom = null; break;
            case Spot.Terminal when At(hot.Index) is { } p:
                if (_hand == Hand.Probe) { if (SwitchRules.ReadTag(s, p.CustomerId)) Tip("probe"); }
                else if (_hand == Hand.Wire) _wireFrom = p.CustomerId;
                else if (_hand == Hand.Dirty) { if (SwitchRules.HotWire(s, p.CustomerId)) { _hand = Hand.Empty; Tip("hotwire"); } }
                else if (_hand == Hand.Empty) EmptyHandOn(p);
                break;
            case Spot.Port:
                if (_hand == Hand.TestSet) { if (!SwitchRules.TestPort(s, hot.Index)) Tip("badport"); }
                else if (_hand == Hand.Wire && _wireFrom is not null)
                {
                    if (SwitchRules.Jumper(s, _wireFrom, hot.Index) is not null) Tip("pulled");
                    _hand = Hand.Empty; _wireFrom = null;
                }
                else if (_hand == Hand.Empty && SwitchRules.OnPort(s, hot.Index) is { } on) EmptyHandOn(on);
                break;
            case Spot.Switch:
                _lastThrow = Sim.ThrowSwitch().ToDictionary(r => r.CustomerId, r => r.Result);
                _flash = 4;
                if (_lastThrow.Count > 0) Tip(_lastThrow.Values.All(r => r == ThrowResult.Live) ? "thrown" : "thrownbad");
                break;
            case Spot.Panel: SwitchRules.AddPanel(s); break;
        }
    }

    private string HandText() => _hand switch
    {
        Hand.Probe => "Tone probe in hand: touch a smudged tag to read it",
        Hand.TestSet => "Test set in hand: hold it to a port to see if the port is good",
        Hand.Wire => _wireFrom is null ? "Jumper in hand: touch a subscriber's terminal" : "Now touch the port the tag names",
        Hand.Dirty => "An old jumper in hand: touch a terminal to hot-wire it. Quick, and it will come back to you",
        _ => "",
    };

    private string Describe(Hot hot)
    {
        var s = Sim.State;
        string Person(BoardPosition p) => p.TagRead ? SwitchRules.Who(s, p)?.Name ?? "?" : "a smudged tag";
        string OnWire(BoardPosition p) =>
            p.Loose ? $"{Person(p)}: this jumper is loose. Click to push it home"
            : SwitchRules.IsLive(s, p) ? $"{Person(p)}: LIVE. Pulling this wire cuts them off"
            : p.Jumper >= 0 ? $"{Person(p)}: jumpered to {PortName(p.Jumper)}{(p.HotWired ? " with an old wire" : "")}, not yet tested. Click to pull it"
            : p.TagRead ? $"{Person(p)}: waiting. Their tag says port {PortName(p.AssignedPort)}" : "Smudged tag: use the tone probe";
        switch (hot.Kind)
        {
            case Spot.Probe: return _hand == Hand.Probe ? "Put the tone probe down" : "Pick up the tone probe (reads smudged tags)";
            case Spot.TestSet: return _hand == Hand.TestSet ? "Put the test set down" : "Pick up the test set (checks a port is good)";
            case Spot.Hook: return _hand == Hand.Wire ? "Hang the jumper back up" : "Take a jumper wire";
            case Spot.DirtyHook: return _hand == Hand.Dirty ? "Hang it back up" : "Take an old dirty jumper (hot-wire: fast, works today, trouble later)";
            case Spot.Switch: return SwitchRules.Pending(s).Any(p => p.Jumper >= 0) ? "Throw the test switch: try every new line" : "The test switch (nothing new is jumpered)";
            case Spot.Panel: return SwitchRules.CanAddPanel(s) ? $"Bolt on a second panel: ${SwitchRules.PanelPrice}, room for {16} subscribers" : $"A second panel is ${SwitchRules.PanelPrice}. You have ${s.Cash}";
            case Spot.Terminal:
                if (hot.Index >= B.Positions.Count) return "An empty terminal";
                var p = B.Positions[hot.Index];
                return _hand switch
                {
                    Hand.Probe => p.TagRead ? $"{Person(p)}: the tag is readable" : "Touch with the probe to read this tag",
                    Hand.Wire => $"Start the jumper at {Person(p)}",
                    Hand.Dirty => SwitchRules.IsLive(s, p) ? $"{Person(p)} is already live" : $"Hot-wire {Person(p)}",
                    _ => OnWire(p),
                };
            default:
                string port = $"Port {PortName(hot.Index)}";
                string state = SwitchRules.KnownGood(s, hot.Index) is { } good ? (good ? " (tested good)" : " (tested BAD)") : "";
                if (_hand == Hand.TestSet) return $"Test {port.ToLowerInvariant()}{state}";
                if (_hand == Hand.Wire && _wireFrom is not null)
                    return SwitchRules.OnPort(s, hot.Index) is { } taken && SwitchRules.IsLive(s, taken) ? $"{port}: {Person(taken)} is LIVE on it. Plugging here cuts them off" : $"Plug the jumper into {port.ToLowerInvariant()}{state}";
                return SwitchRules.OnPort(s, hot.Index) is { } on ? OnWire(on) : port + (SwitchRules.IsSpare(B, hot.Index) ? ", a spare: for someone whose own port is bad" : "") + state;
        }
    }

    /// <summary>What is in your hand follows the cursor; a jumper already started trails back to its terminal.</summary>
    private void DrawHand()
    {
        foreach (var n in _handNode.GetChildren()) n.QueueFree();
        if (!IsOpen || _hand == Hand.Empty) return;
        var at = new Vector3(_at.X, _at.Y, 0.16f);
        var lp = new LowPoly();
        switch (_hand)
        {
            case Hand.Probe: lp.Box(at + new Vector3(0.06f, -0.03f, 0), new Vector3(0.16f, 0.04f, 0.04f), new Color(0.95f, 0.8f, 0.15f)).Box(at, new Vector3(0.05f, 0.016f, 0.016f), new Color(0.7f, 0.7f, 0.72f)); break;
            case Hand.TestSet: lp.Box(at + new Vector3(0.05f, -0.04f, 0), new Vector3(0.16f, 0.06f, 0.06f), new Color(0.1f, 0.1f, 0.11f)); break;
            default:
                var col = _hand == Hand.Dirty ? Dirty : WireColours[B.Positions.FindIndex(p => p.CustomerId == _wireFrom) is >= 0 and var i ? i % WireColours.Length : 0];
                lp.Box(at, new Vector3(0.03f, 0.03f, 0.03f), col);
                if (_wireFrom is not null && B.Positions.FindIndex(p => p.CustomerId == _wireFrom) is >= 0 and var row)
                    AddWire(_handNode, TerminalAt(row) + new Vector3(0, 0, 0.015f), at, col, 0.05f);
                else lp.Box(at + new Vector3(0, -0.07f, 0), new Vector3(0.012f, 0.12f, 0.012f), col);
                break;
        }
        _handNode.AddChild(lp.Instance());
    }
}
