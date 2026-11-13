using Godot;
using Telo.Sim.Minigames.Install;
using Telo.Sim.Town;

namespace Telo.Game.World;

/// <summary>
/// The physical side of a house install: the places on and in a house where you clip, screw, staple and plug.
/// It only shows the sim's <see cref="InstallJob"/> and passes clicks to it; what counts as done, bodged or faulty is decided there.
/// Lives in the house's local space (front door faces +Z). All placeholder boxes.
/// </summary>
public partial class InstallSite : Node3D
{
    private sealed record Piece(InstallStep Step, int Index, Node3D Node);
    private sealed record Spot(InstallStep Step, int First, int Last, Interactable Hotspot, Node3D Ghost);

    private static readonly Color Black = new(0.06f, 0.06f, 0.07f), Grey = new(0.62f, 0.64f, 0.66f), Cream = new(0.9f, 0.88f, 0.8f),
        Beige = new(0.78f, 0.74f, 0.62f), Steel = new(0.75f, 0.77f, 0.8f);

    private readonly List<Piece> _pieces = new();
    private readonly List<Spot> _spots = new();
    private Interactable _phone = null!, _sheet = null!;
    private Node3D _sheetMesh = null!;
    private InstallJob? _job;
    private Action<InstallStep, bool>? _act;
    private Action? _signOff;
    private Func<Tools> _onSite = () => Tools.All;
    private Func<Tools, string> _kitNames = t => t.ToString();
    private string _testNote = "";
    private readonly Dictionary<Node3D, Transform3D> _square = new();
    private readonly HashSet<InstallStep> _crooked = new();
    private double _t;
    /// <summary>Real seconds of holding for a step of this many game minutes. Set by the game.</summary>
    public static Func<int, float> HoldFor { get; set; } = minutes => minutes * 0.33f;

    /// <summary>The install here is finished: wire, socket and modem are part of the house now.</summary>
    public bool Fitted { get; private set; }
    public bool Active => _job is not null;
    /// <summary>Work points in the order a careful installer uses them (for automated checks).</summary>
    public IEnumerable<Interactable> WorkOrder => _spots.SelectMany(s => Enumerable.Repeat(s.Hotspot, s.Last - s.First + 1)).Append(_phone).Append(_sheet);

    public InstallSite(float wallH, float floorY, float poleDist)
    {
        Name = "InstallSite";
        float f = floorY;

        // --- outside: down the front wall, left of the window ---
        float x = -3.6f, z = 3.53f;
        Vector3 pole = new(x, 6.5f, poleDist), p1 = new(x, Mathf.Min(wallH - 0.35f, 2.7f), z), p2 = new(x, 2.0f, z), p3 = new(x, 1.35f, z), jb = new(x, 0.85f, z + 0.03f);
        AddPiece(InstallStep.DropWire, 0, Box(p1, new Vector3(0.08f, 0.08f, 0.06f), Steel), Wire(pole, p1));
        AddPiece(InstallStep.DropWire, 1, Box(p2, new Vector3(0.08f, 0.05f, 0.05f), Steel), Wire(p1, p2));
        AddPiece(InstallStep.DropWire, 2, Box(p3, new Vector3(0.08f, 0.05f, 0.05f), Steel), Wire(p2, p3), Wire(p3, jb));
        Point(InstallStep.DropWire, 0, 0, p1); Point(InstallStep.DropWire, 1, 1, p2); Point(InstallStep.DropWire, 2, 2, p3);
        AddPiece(InstallStep.JunctionBox, 0, Box(jb, new Vector3(0.2f, 0.26f, 0.06f), Grey));
        AddPiece(InstallStep.JunctionBox, 1, Box(jb + new Vector3(0, 0, 0.04f), new Vector3(0.22f, 0.28f, 0.03f), Cream));
        Point(InstallStep.JunctionBox, 0, 1, jb);

        // --- inside: through the wall, along the left skirting to a socket by the hall table ---
        float wx = -3.77f, sk = f + 0.1f;
        Vector3 entry = new(x, 0.85f, 3.28f), corner = new(wx, sk, 3.26f), s1 = new(wx, sk, 2.5f), s2 = new(wx, sk, 1.6f), s3 = new(wx, sk, 0.8f),
            socket = new(wx, f + 0.4f, 0.3f), modem = new(-3.35f, f + 0.74f, -0.75f), phone = new(-3.35f, f + 0.76f, -0.25f);
        AddPiece(InstallStep.Socket, 0, Box(socket, new Vector3(0.03f, 0.1f, 0.1f), Grey));
        AddPiece(InstallStep.Socket, 1, Box(socket + new Vector3(0.02f, 0, 0), new Vector3(0.02f, 0.12f, 0.12f), Cream));
        Point(InstallStep.Socket, 0, 1, socket);
        AddPiece(InstallStep.Cable, 0, Wire(entry, corner), Wire(corner, s1), Staple(s1));
        AddPiece(InstallStep.Cable, 1, Wire(s1, s2), Staple(s2));
        AddPiece(InstallStep.Cable, 2, Wire(s2, s3), Staple(s3), Wire(s3, new Vector3(wx, sk, 0.3f)), Wire(new Vector3(wx, sk, 0.3f), socket));
        Point(InstallStep.Cable, 0, 0, s1); Point(InstallStep.Cable, 1, 1, s2); Point(InstallStep.Cable, 2, 2, s3);
        AddPiece(InstallStep.Modem, 0, Box(modem, new Vector3(0.3f, 0.07f, 0.22f), Beige), Wire(socket, modem + new Vector3(-0.1f, 0, 0.1f)),
            Box(modem + new Vector3(-0.1f, 0.04f, 0.06f), new Vector3(0.02f, 0.012f, 0.02f), new Color(0.9f, 0.2f, 0.15f)));
        Point(InstallStep.Modem, 0, 0, modem);

        // the customer's own telephone: always there; during a job it is how you test
        AddChild(Box(phone, new Vector3(0.2f, 0.1f, 0.22f), new Color(0.5f, 0.12f, 0.1f)));
        AddChild(Box(phone + new Vector3(0, 0.07f, 0), new Vector3(0.24f, 0.05f, 0.07f), new Color(0.42f, 0.1f, 0.08f)));
        _phone = Hotspot(phone, new Vector3(0.34f, 0.3f, 0.34f));
        _phone.Prompt = PhonePrompt;
        _phone.CanUse = () => _job is { CanFinish: true };
        _phone.OnUse = () => { if (_job is { CanFinish: true }) _act?.Invoke(InstallStep.Test, false); };
        _phone.HoldSeconds = () => _job is { CanFinish: true } ? HoldFor(InstallJob.TestMinutes) : 0f;

        // the job sheet hangs by the door while you work
        var sheetPos = new Vector3(1.25f, f + 1.4f, 3.27f);
        _sheetMesh = new Node3D();
        _sheetMesh.AddChild(Box(sheetPos, new Vector3(0.3f, 0.4f, 0.03f), new Color(0.45f, 0.3f, 0.18f)));
        _sheetMesh.AddChild(Box(sheetPos + new Vector3(0, -0.02f, -0.02f), new Vector3(0.26f, 0.33f, 0.01f), new Color(0.95f, 0.93f, 0.86f)));
        _sheetMesh.AddChild(new Label3D { Text = "JOB\nSHEET", Position = sheetPos + new Vector3(0, 0, -0.03f), RotationDegrees = new Vector3(0, 180, 0), FontSize = 48, PixelSize = 0.0012f, Modulate = Black, OutlineSize = 0 });
        AddChild(_sheetMesh);
        _sheet = Hotspot(sheetPos, new Vector3(0.4f, 0.5f, 0.2f));
        _sheet.Prompt = SheetPrompt;
        _sheet.CanUse = () => _job is { CanFinish: true };
        _sheet.OnUse = () => { if (_job is { CanFinish: true }) _signOff?.Invoke(); };

        Sync();
    }

    // ---- driven by GameRoot ------------------------------------------------------------------
    /// <param name="onSite">Kit the player has with them right now.</param>
    public void Begin(InstallJob job, Action<InstallStep, bool> act, Action signOff, Func<Tools> onSite, Func<Tools, string> kitNames)
    {
        _job = job; _act = act; _signOff = signOff; _onSite = onSite; _kitNames = kitNames; _testNote = "";
        Sync();
    }

    /// <summary>Job signed off (or the house already had its line when the game loaded).</summary>
    public void ShowFitted()
    {
        if (_job is { } done) foreach (var d in InstallJob.Steps.Where(d => done.IsRushed(d.Step))) _crooked.Add(d.Step);   // a bodge stays visible
        _job = null; Fitted = true;
        Sync();
    }

    public void NoteTest(InstallTestResult r) =>
        _testNote = r.DialTone ? "Dial tone. Clear as a bell." : r.FaultAt is { } s ? $"No dial tone. Something is wrong at the {InstallJob.Def(s).Title.ToLowerInvariant()}: redo it." : "";

    public void Sync()
    {
        foreach (var p in _pieces)
        {
            p.Node.Visible = Fitted || (_job is not null && _job.Progress(p.Step) > p.Index);
            // mistakes show: a bodged stage hangs crooked
            bool crooked = _job?.IsRushed(p.Step) ?? _crooked.Contains(p.Step);
            foreach (var child in p.Node.GetChildren().OfType<Node3D>())
                if (_square.TryGetValue(child, out var square))
                    child.Transform = crooked ? square * new Transform3D(new Basis(Vector3.Back, 0.2f) * new Basis(Vector3.Right, 0.12f), new Vector3(0, -0.015f, 0)) : square;
        }
        foreach (var s in _spots)
        {
            s.Ghost.Visible = _job is not null && _job.Progress(s.Step) <= s.Last;
            s.Hotspot.CollisionLayer = _job is not null ? 2u : 0u;
        }
        _phone.CollisionLayer = _sheet.CollisionLayer = _job is not null ? 2u : 0u;
        _sheetMesh.Visible = _job is not null;
    }

    /// <summary>The marker for the step that is next pulses; the ones after it wait, small.</summary>
    public override void _Process(double delta)
    {
        if (_job is null) return;
        _t += delta;
        foreach (var s in _spots)
            s.Ghost.Scale = Vector3.One * (SpotTurn(s) ? 1.25f + 0.3f * (float)Math.Sin(_t * 6) : 0.55f);
    }

    // ---- prompts -----------------------------------------------------------------------------
    private string SpotPrompt(Spot s)
    {
        if (_job is not { } j) return "";
        var d = InstallJob.Def(s.Step);
        int done = j.Progress(s.Step);
        if (done > s.Last) return $"{d.Title}: done{(j.IsRushed(s.Step) ? " (bodged)" : "")}";
        if (j.CanDo(s.Step) && done >= s.First && j.Missing(s.Step, _onSite()) is var missing && missing != Tools.None)
            return $"{d.Verb}: you need {_kitNames(missing)} here (from the van)";
        if (j.CanDo(s.Step) && done >= s.First)
            return $"{(j.KnownFault == s.Step ? "Redo: " : "")}{d.Verb} ({done + 1} of {d.Clicks})";
        var first = j.NextStep is { } n ? InstallJob.Def(n) : d;
        return first.Step == s.Step ? $"{d.Title}: do the one before this first" : $"First: {first.Verb.ToLowerInvariant()} ({(first.Outside ? "outside" : "inside")})";
    }

    private bool SpotTurn(Spot s) => _job is { } j && j.CanDo(s.Step) && j.Progress(s.Step) >= s.First && j.Progress(s.Step) <= s.Last;
    private bool SpotReady(Spot s) => SpotTurn(s) && _job!.Missing(s.Step, _onSite()) == Tools.None;
    private bool SpotRushable(Spot s) => SpotTurn(s) && InstallJob.Def(s.Step).CanRush && _job!.Missing(s.Step, _onSite(), rush: true) == Tools.None;

    private string PhonePrompt()
    {
        if (_job is not { } j) return "";
        if (!j.CanFinish) return _testNote.Length > 0 ? _testNote : "The telephone. Finish the wiring, then listen for dial tone";
        return j.Tested ? _testNote : "Lift the handset and listen for dial tone";
    }

    private string SheetPrompt()
    {
        if (_job is not { } j) return "";
        if (!j.CanFinish) return $"Job sheet for {j.CustomerName}. Next: {(j.NextStep is { } n ? InstallJob.Def(n).Verb.ToLowerInvariant() : "test")}";
        return j.Tested ? $"Sign off {j.CustomerName}'s install (tested)" : $"Sign off {j.CustomerName}'s install WITHOUT testing (or use their telephone first)";
    }

    // ---- building blocks ---------------------------------------------------------------------
    private void AddPiece(InstallStep step, int index, params Node3D[] parts)
    {
        var group = new Node3D();
        foreach (var p in parts) { group.AddChild(p); _square[p] = p.Transform; }
        AddChild(group);
        _pieces.Add(new Piece(step, index, group));
    }

    private void Point(InstallStep step, int first, int last, Vector3 pos)
    {
        var ghost = new MeshInstance3D
        {
            Position = pos, Mesh = new BoxMesh { Size = Vector3.One * 0.1f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.82f, 0.15f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(ghost);
        var hot = Hotspot(pos, Vector3.One * 0.36f);
        var spot = new Spot(step, first, last, hot, ghost);
        hot.Prompt = () => SpotPrompt(spot);
        hot.CanUse = () => SpotReady(spot);
        hot.OnUse = () => { if (SpotReady(spot)) _act?.Invoke(step, false); };
        hot.HoldSeconds = () => SpotReady(spot) ? HoldFor(InstallJob.Def(step).MinutesPerClick) : 0f;
        hot.AltPrompt = () => SpotRushable(spot) ? "Bodge it" : null;
        hot.OnAlt = () => { if (SpotRushable(spot)) _act?.Invoke(step, true); };
        _spots.Add(spot);
    }

    private Interactable Hotspot(Vector3 pos, Vector3 size)
    {
        var it = new Interactable { Position = pos, CollisionLayer = 0, CollisionMask = 0 };
        it.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(it);
        return it;
    }

    private static MeshInstance3D Box(Vector3 pos, Vector3 size, Color col)
    {
        var mi = new LowPoly().Box(Vector3.Zero, size, col).Instance();
        mi.Position = pos;
        return mi;
    }

    private static MeshInstance3D Staple(Vector3 pos) => Box(pos, new Vector3(0.02f, 0.05f, 0.03f), Steel);

    /// <summary>A thin black cable between two points.</summary>
    private static MeshInstance3D Wire(Vector3 a, Vector3 b, float thickness = 0.022f)
    {
        var d = b - a; float len = d.Length();
        var mi = new LowPoly().Box(Vector3.Zero, new Vector3(thickness, thickness, len), Black).Instance();
        var z = d / len;
        var up = Mathf.Abs(z.Y) > 0.95f ? Vector3.Right : Vector3.Up;
        var x = up.Cross(z).Normalized();
        mi.Transform = new Transform3D(new Basis(x, z.Cross(x), z), (a + b) / 2);
        mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return mi;
    }
}
