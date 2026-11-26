using Godot;

namespace Telo.Game.World;

/// <summary>One place on a thing where a piece of the work is done, in the host's local space.</summary>
/// <param name="Motion">What your hands do there: screw, bolt, clip, place, dig, tamp, pull, tape, plug, check (see <see cref="WorkPoints.Motions"/>).</param>
public sealed record WorkPlace(Vector3 Pos, string Motion);

/// <summary>
/// No progress bars (HO1, D58, docs/WORK_POINTS.md): a job step that takes time is done at several real places on the thing.
/// Attach to any <see cref="Interactable"/> that has a <c>HoldSeconds</c>: the host keeps the rules (CanUse, Prompt, OnUse,
/// OnAlt, its sounds) and this splits the hold over 2–5 points. Each point is a small piece (a screw, a clip, a spadeful of
/// earth) that you hold E on; the piece itself moves while you work it, so the progress is in the world, not on a bar.
/// When the last piece is done the host's OnUse fires, exactly as the old hold did, and the total real time is the same.
///
/// Where the points go: a model can name them. Any node in the model called <c>wp-…</c> is a point, in name order
/// (<c>wp-01-dig</c>, <c>wp-02-place</c>, <c>wp-03-tamp</c>; a step tag narrows it: <c>wp-s2-01-bolt</c>). So the
/// geometry decides where you work and the sim decides how long: make the model, name the empties, done.
/// Without a model the caller gives fallback places, or points are spread across the host's box.
///
/// While there is work to do (the host's HoldSeconds is above zero) the host is parked on its own collision layer, where
/// no look ray finds it, and only the next point can be looked at; otherwise the host is back where its owner put it, so
/// its prompt (a wait, "built") still reads. Code that switches the host on and off (its layer / 0) keeps working unchanged.
/// </summary>
public partial class WorkPoints : Node3D
{
    /// <summary>The owner's "on" layer, and the layer the host waits on while its points are worked (no ray looks there).</summary>
    public const uint Live = 2, Parked = 32;
    /// <summary>The motions a point can have, and the words shown for each.</summary>
    public static readonly Dictionary<string, string> Motions = new()
    {
        ["screw"] = "drive the screw", ["bolt"] = "tighten the bolt", ["clip"] = "push the clip home", ["place"] = "set it in place",
        ["dig"] = "dig", ["tamp"] = "tamp it down", ["pull"] = "pull it taut", ["tape"] = "tape it", ["plug"] = "plug it in",
        ["check"] = "check it", ["solder"] = "solder the joint", ["staple"] = "staple it", ["lift"] = "lift it", ["cut"] = "cut it",
    };

    /// <summary>What working a point sounds like (held, and done for every point but the last, which is the step's own).</summary>
    private static (string Hold, string Done) MotionSound(string motion) => motion switch
    {
        "screw" or "bolt" => ("tool.screw", "tool.clip"),
        "clip" or "plug" => ("tool.clip", "tool.clip"),
        "staple" => ("tool.staple", "tool.staple"),
        "dig" => ("tool.dig", "tool.dig"),
        "tamp" => ("tool.thump", "tool.hammer"),
        "pull" or "tape" => ("tool.cable", "tool.clip"),
        "cut" or "solder" => ("tool.cable", "tool.clip"),
        "check" => ("sw.probe", "work.done"),
        "place" or "lift" => ("work.tick", "kit.putdown"),
        _ => ("work.tick", "work.done"),
    };

    public Interactable Host { get; }
    /// <summary>The points of the step under way, in the order they are worked.</summary>
    public IReadOnlyList<Interactable> Points => _points;
    /// <summary>The point to work next (null when none are live).</summary>
    public Interactable? Next => _done < _points.Count ? _points[_done] : null;
    /// <summary>Points are laid out and the next one can be worked.</summary>
    public bool IsWorking { get; private set; }

    private readonly Func<WorkPoints, IReadOnlyList<WorkPlace>> _places;
    private readonly List<Interactable> _points = new();
    private readonly List<Node3D> _pieces = new();
    private readonly List<WorkPlace> _plan = new();
    private int _done;
    private bool _on;
    private uint _ownerLayer = Live;
    /// <summary>A soft glow on the point to work next, so you can find it on the thing (no marker floats above it).</summary>
    private MeshInstance3D? _glow;
    private static StandardMaterial3D? _glowMat;
    private string _key = "";
    private double _t;

    private WorkPoints(Interactable host, Func<WorkPoints, IReadOnlyList<WorkPlace>> places)
    {
        Host = host; _places = places; Name = "WorkPoints";
    }

    /// <summary>Split <paramref name="host"/>'s held work over points. <paramref name="places"/> is asked at the start of each step.</summary>
    public static WorkPoints Attach(Interactable host, Func<WorkPoints, IReadOnlyList<WorkPlace>>? places = null)
    {
        var wp = new WorkPoints(host, places ?? (w => w.Spread()));
        host.Pieces = wp;
        host.AddChild(wp);
        return wp;
    }

    // ---- where the points go ----------------------------------------------------------------------

    /// <summary>
    /// The <c>wp-</c> nodes under <paramref name="model"/> (optionally only those tagged <paramref name="step"/>), as places in
    /// the host's space. Empty if the model names none: then use your fallback.
    /// </summary>
    public IReadOnlyList<WorkPlace> FromModel(Node3D? model, string? step = null)
    {
        if (model is null || !model.IsInsideTree() || !Host.IsInsideTree()) return Array.Empty<WorkPlace>();
        var inv = Host.GlobalTransform.AffineInverse();
        return Props.Parts(model, "wp-")
            .Select(n => (n, parts: n.Name.ToString().Split('-', '.', '_')))
            .Where(x => step is null || x.parts.Contains(step))
            .OrderBy(x => x.n.Name.ToString(), StringComparer.Ordinal)
            .Select(x => new WorkPlace(inv * x.n.GlobalPosition, x.parts.LastOrDefault(Motions.ContainsKey) ?? "place"))
            .ToList();
    }

    /// <summary>Points spread across the front of the host's box, with motions guessed from what the host says it is doing.</summary>
    public IReadOnlyList<WorkPlace> Spread(int count = 0)
    {
        var size = Host.GetChildren().OfType<CollisionShape3D>().FirstOrDefault()?.Shape is BoxShape3D b ? b.Size : new Vector3(0.6f, 0.6f, 0.6f);
        if (count <= 0) count = Mathf.Clamp(Mathf.CeilToInt(Host.HoldSeconds() / 1.5f), 2, 4);
        var motions = Guess(Host.Prompt(), count);
        var list = new List<WorkPlace>();
        for (int i = 0; i < count; i++)
        {
            float f = count == 1 ? 0.5f : (float)i / (count - 1);
            float x = (f - 0.5f) * size.X * 0.7f, y = (i % 2 == 0 ? 0.12f : -0.12f) * size.Y;
            list.Add(new WorkPlace(new Vector3(x, y, size.Z * 0.5f), motions[i]));
        }
        return list;
    }

    /// <summary>A plausible order of motions for a step from its verb ("Plant pole 3" → dig, lift, place, tamp).</summary>
    public static string[] Guess(string verb, int count)
    {
        string v = verb.ToLowerInvariant();
        bool Has(params string[] words) => words.Any(v.Contains);
        string[] seq =
              Has("test", "ring ", "ring it", "listen", "record", "send a", "time it", "watch", "show the") ? new[] { "check", "check", "check" }
            : Has("undo", "open it", "unscrew", "unbox") ? new[] { "screw", "screw", "lift", "place" }
            : Has("up again", "put it back", "back together") ? new[] { "place", "screw", "screw", "check" }
            : Has("replace", "swap") ? new[] { "lift", "place", "screw", "check" }
            : Has("unload", "delivery", "carry", "crate", "wheel in") ? new[] { "lift", "place", "check" }
            : Has("plant") ? new[] { "dig", "lift", "place", "tamp" }
            : Has("string", "span", "cable", "run ", "wire", "pull", "haul", "blow") ? new[] { "clip", "pull", "clip", "tape" }
            : Has("staple") ? new[] { "staple", "pull", "staple" }
            : Has("card", "slot", "plug", "modem") ? new[] { "place", "plug", "screw" }
            : Has("socket", "box", "fit", "mount", "terminal") ? new[] { "place", "screw", "screw", "check" }
            : Has("dig", "trench", "duct", "pour", "peg") ? new[] { "dig", "dig", "place", "tamp" }
            : Has("solder", "splice", "joint", "fibre", "patch") ? new[] { "cut", "solder", "tape" }
            : Has("rack", "bolt", "mast", "dish", "frame", "stand", "raise", "steel") ? new[] { "lift", "bolt", "bolt", "check" }
            : Has("repair", "fix", "mend") ? new[] { "check", "screw", "tape", "check" }
            : Has("check", "commission", "light", "throw", "bring it up", "aim") ? new[] { "check", "check" }
            : Has("form", "fill in", "sign", "agree", "label", "plan", "pay ") ? new[] { "check", "place" }
            : new[] { "place", "screw", "bolt", "check" };
        return Enumerable.Range(0, count).Select(i => seq[Math.Min(i, seq.Length - 1)]).ToArray();
    }

    // ---- the step under way --------------------------------------------------------------------------

    private float PieceSeconds => _plan.Count == 0 ? 0 : Mathf.Max(Host.HoldSeconds() / _plan.Count, 0.25f);

    /// <summary>Throw away the step under way (its pieces vanish); the next frame lays out the next one.</summary>
    public void Reset()
    {
        foreach (var p in _points) p.QueueFree();
        foreach (var p in _pieces) p.QueueFree();
        _points.Clear(); _pieces.Clear(); _plan.Clear();
        _done = 0; _key = "";
    }

    public override void _PhysicsProcess(double delta)
    {
        // the host's owner switches it on (layer 2) or off (0); we park it while there are points to work
        uint layer = Host.CollisionLayer;
        if (layer == 0) _on = false;
        else if (layer != Parked) { _on = true; _ownerLayer = layer; }
        bool working = _on && Host.HoldSeconds() > 0;
        if (_on) Host.CollisionLayer = working ? Parked : _ownerLayer;
        if (working)
        {
            // a different step (the prompt's first words changed with nothing done) means new places
            string key = Host.Prompt();
            int cut = key.IndexOf('(');
            key = cut > 0 ? key[..cut] : key;
            if (_plan.Count == 0 || (_done == 0 && key != _key)) Lay(key);
        }
        else if (_plan.Count > 0) Reset();
        for (int i = 0; i < _points.Count; i++) _points[i].CollisionLayer = working && i == _done ? Live : 0;
        IsWorking = working && _points.Count > 0;
        Visible = _on;
    }

    public override void _Process(double delta)
    {
        _t += delta;
        if (_glow is not null)
        {
            bool show = IsWorking && Next is not null;
            _glow.Visible = show;
            if (show)
            {
                _glow.Position = _plan[_done].Pos;
                float held = _pieces[_done].GetMeta("held", 0f).AsSingle();
                _glow.Scale = Vector3.One * (held > 0 ? 0.6f : 1f + 0.25f * Mathf.Sin((float)_t * 5));
            }
        }
        for (int i = _done; i < _pieces.Count; i++)
        {
            var piece = _pieces[i];
            if (piece.GetMeta("held", 0f).AsSingle() > 0) continue;
            float pulse = i == _done ? 1f + 0.15f * Mathf.Sin((float)_t * 6) : 0.7f;
            piece.Scale = Vector3.One * pulse;
        }
    }

    private void Lay(string key)
    {
        Reset();
        _key = key;
        if (_glow is null)
        {
            _glowMat ??= new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add, AlbedoColor = new Color(1f, 0.75f, 0.25f, 0.55f), NoDepthTest = false,
            };
            _glow = new MeshInstance3D
            {
                Name = "Glow", Mesh = new SphereMesh { Radius = 0.07f, Height = 0.14f, RadialSegments = 10, Rings = 5 },
                MaterialOverride = _glowMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false,
            };
            AddChild(_glow);
        }
        _plan.AddRange(_places(this) is { Count: > 0 } given ? given : Spread());
        for (int i = 0; i < _plan.Count; i++)
        {
            var place = _plan[i];
            var piece = Piece(place.Motion);
            piece.Position = place.Pos;
            AddChild(piece);
            _pieces.Add(piece);
            Ghost(piece, true);

            int index = i;
            bool first = i == 0, last = i == _plan.Count - 1;
            var sound = MotionSound(place.Motion);
            var hot = new Interactable { Name = $"wp{i}", Position = place.Pos, CollisionLayer = 0, CollisionMask = 0, Pieces = this };
            hot.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.22f } });
            hot.Prompt = () => $"{Host.Prompt()}: {Motions.GetValueOrDefault(place.Motion, place.Motion)} [{index + 1}/{_plan.Count}]";
            hot.CanUse = () => Host.CanUse() && index == _done;
            hot.HoldSeconds = () => PieceSeconds;
            hot.OnHold = t => Animate(index, t);
            hot.OnUse = () => Finish(index);
            hot.AltPrompt = () => Host.AltPrompt();
            hot.OnAlt = () => { Host.OnAlt?.Invoke(); Reset(); };
            // the sounds: the step's start on the first piece, each piece's own while you work it, the step's own on the last
            hot.StartSound = first ? Host.StartSound : () => null;
            hot.HoldSound = () => sound.Hold;
            hot.DoneSound = last ? Host.DoneSound : () => sound.Done;
            AddChild(hot);
            _points.Add(hot);
        }
    }

    private void Finish(int index)
    {
        if (index != _done || !Host.CanUse()) return;
        Animate(index, 1f);
        Ghost(_pieces[index], false);
        _pieces[index].SetMeta("held", 1f);
        _done++;
        if (_done < _plan.Count) return;
        Host.OnUse?.Invoke();
        Reset();
    }

    /// <summary>The piece moves as you work it: t runs 0 → 1 while E is held, back to 0 if you let go.</summary>
    private void Animate(int index, float t)
    {
        if (index >= _pieces.Count || index < _done && t < 1) return;
        var piece = _pieces[index];
        piece.SetMeta("held", t);
        string motion = _plan[index].Motion;
        float e = 1 - (1 - t) * (1 - t);
        piece.Scale = Vector3.One;
        piece.Rotation = Vector3.Zero;
        piece.Position = _plan[index].Pos;
        switch (motion)
        {
            case "screw": case "bolt":
                piece.Rotation = new Vector3(0, 0, e * Mathf.Tau * 3); piece.Position += new Vector3(0, 0, 0.03f * (1 - e)); break;
            case "clip": case "plug": case "staple":
                piece.Position += new Vector3(0, 0, 0.08f * (1 - e)); break;
            case "place": case "lift":
                piece.Position += new Vector3(0, 0.25f * (1 - e), 0); piece.Scale = Vector3.One * Mathf.Lerp(0.6f, 1f, e); break;
            case "dig":
                piece.Scale = new Vector3(1, Mathf.Lerp(0.1f, 1f, e), 1); break;
            case "tamp":
                piece.Scale = new Vector3(1, Mathf.Lerp(1f, 0.35f, e), 1); break;
            case "pull": case "tape":
                piece.Scale = new Vector3(Mathf.Lerp(0.2f, 1f, e), 1, 1); break;
            case "cut": case "solder":
                piece.Rotation = new Vector3(0, e * Mathf.Pi, 0); break;
            default:
                piece.Scale = Vector3.One * (1 + 0.3f * Mathf.Sin(e * Mathf.Pi)); break;
        }
    }

    private static void Ghost(Node3D piece, bool ghost)
    {
        foreach (var mi in piece.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Append(piece as MeshInstance3D).OfType<MeshInstance3D>())
            mi.Transparency = ghost ? 0.45f : 0f;
    }

    /// <summary>The little thing you work at each point. Code-built for now; a prop "piece.&lt;motion&gt;" replaces it when modelled.</summary>
    private static Node3D Piece(string motion)
    {
        Color steel = new(0.75f, 0.77f, 0.8f), brass = new(0.8f, 0.64f, 0.25f), earth = new(0.36f, 0.26f, 0.16f), black = new(0.08f, 0.08f, 0.09f),
            tape = new(0.15f, 0.2f, 0.55f), mark = new(1f, 0.82f, 0.15f), probe = new(0.85f, 0.2f, 0.15f);
        var id = $"piece.{motion}";
        if (Props.HasModel(id)) return Props.Make(id);
        var lp = new LowPoly();
        switch (motion)
        {
            case "screw": lp.Cylinder(Vector3.Zero, 0.018f, 0.018f, 0.012f, 8, brass).Box(new Vector3(0, 0.006f, 0), new Vector3(0.026f, 0.004f, 0.004f), black); break;
            case "bolt": lp.Cylinder(Vector3.Zero, 0.03f, 0.03f, 0.025f, 6, steel); break;
            case "clip": case "staple": lp.Box(Vector3.Zero, new Vector3(0.06f, 0.035f, 0.03f), steel); break;
            case "plug": lp.Box(Vector3.Zero, new Vector3(0.05f, 0.05f, 0.07f), new Color(0.9f, 0.88f, 0.8f)); break;
            case "dig": lp.Cylinder(Vector3.Zero, 0.24f, 0.1f, 0.16f, 7, earth); break;
            case "tamp": lp.Cylinder(Vector3.Zero, 0.22f, 0.22f, 0.06f, 7, earth); break;
            case "pull": lp.Box(Vector3.Zero, new Vector3(0.5f, 0.02f, 0.02f), black); break;
            case "tape": lp.Box(Vector3.Zero, new Vector3(0.12f, 0.04f, 0.04f), tape); break;
            case "cut": case "solder": lp.Box(Vector3.Zero, new Vector3(0.08f, 0.015f, 0.015f), new Color(0.75f, 0.45f, 0.25f)); break;
            case "check": lp.Box(Vector3.Zero, new Vector3(0.05f, 0.05f, 0.05f), black).Box(new Vector3(0, 0, 0.03f), new Vector3(0.02f, 0.02f, 0.03f), probe); break;
            case "place": case "lift": lp.Box(Vector3.Zero, new Vector3(0.14f, 0.14f, 0.14f), mark); break;
            default: lp.Gem(Vector3.Zero, 0.05f, 0.06f, 0.06f, 4, mark); break;
        }
        var mi = lp.Instance(id.Replace('.', '_'));
        mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return mi;
    }
}
