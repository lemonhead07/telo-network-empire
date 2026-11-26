using Godot;
using Telo.Sim.Content;

namespace Telo.Game.World;

/// <summary>
/// A project site in the world (projects.json): the thing being built, a signboard, and the spot where you work.
///
/// Geometry and gameplay as one system (HO1, docs/WORK_POINTS.md): the project's model (its <c>model</c> prop) is built in
/// stages. Parts named <c>stage-&lt;n&gt;-…</c> stand solid once the step that builds stage n is done, and as a see-through
/// ghost while it is under way, so the site shows how far the work has got; everything else in the model is the site as
/// you find it. A step's stage is its index unless <c>projects.json</c> says otherwise (<c>"stage"</c>: several projects
/// share a model; -1 for a step that builds nothing, a wait or a word with someone); stages below the first one a project
/// builds are already there when you arrive. The model also names where a hand works at each stage
/// (<c>wp-s&lt;n&gt;-NN-motion</c> empties), which <see cref="Places"/> hands to <see cref="WorkPoints"/>. A wait (an
/// engineer booked, concrete setting) is written on the signboard.
///
/// Falls back step by step: a model without stages is hidden until built (crates meanwhile); no model at all is the old
/// placeholder frame that grows. Hidden until the project is open to you; once built it stays, finished.
/// </summary>
public partial class ProjectSiteView : Node3D
{
    public ProjectDef Def { get; }
    public Interactable Spot { get; }
    private readonly Label3D _label, _board;
    private readonly MeshInstance3D _frame;
    private readonly StandardMaterial3D _mat;
    private readonly Node3D? _built;
    private readonly List<(int Stage, Node3D Part)> _stages = new();
    private readonly List<Node3D> _crates = new();
    private readonly bool _shownElsewhere;
    private readonly float _left, _right, _front, _top;
    private Vector3 _signAt;
    private int _step;
    private bool _started;

    /// <param name="frame">False where something else shows the work (the data centre): no growing frame, just crates of parts.</param>
    public ProjectSiteView(ProjectDef def, bool frame = true)
    {
        Def = def;
        Name = def.Id;
        _shownElsewhere = !frame;
        // the thing being built, if it has a model: its size places the sign, the crates and the work spot
        float left = -1.0f, right = 1.0f, front = 0.6f, top = 1.0f;
        bool model = def.Model is { } m && Props.HasModel(m);
        if (model || !frame)
        {
            _built = model ? Props.Make(def.Model!) : new Node3D { Name = "Nothing" };
            AddChild(_built);
            foreach (var part in Props.Parts(_built, "stage-"))
                if (part.Name.ToString().Split('-') is { Length: > 1 } bits && int.TryParse(bits[1], out int n))
                    _stages.Add((n, part));
            _built.Visible = _stages.Count > 0;   // a model with no stages only shows once built
            if (model)
            {
                var b = Props.Bounds(_built);
                left = Mathf.Min(b.Position.X, -1.0f); right = Mathf.Max(b.End.X, 1.0f); front = Mathf.Max(b.End.Z, 0.6f); top = Mathf.Min(b.End.Y, 6f);
            }
            // crates of parts beside it: full when the parts arrive, emptying as the work goes on
            foreach (var (x, z, y, yaw) in new[] { (0.6f, -0.2f, 0f, 0.1f), (0.7f, 0.8f, 0f, -0.2f), (0.65f, 0.3f, 0.6f, 0.05f), (1.6f, 0.4f, 0f, 0.6f) })
            {
                var c = Props.Make("site.parts-crate", () => new LowPoly().Box(new Vector3(0, 0.3f, 0), new Vector3(0.8f, 0.6f, 0.6f), new Color(0.72f, 0.58f, 0.36f)).Instance());
                c.Position = new Vector3(right + x, y, front + z - 1.0f); c.Rotation = new Vector3(0, yaw, 0); c.Visible = false;
                AddChild(c); _crates.Add(c);
            }
        }
        _left = left; _right = right; _front = front; _top = top;
        // the signboard, off to the left of whatever is there: the project's name, the step, or what it waits on
        float sx = left - 0.7f;
        _signAt = new Vector3(sx, 1.55f, front + 0.085f);
        var post = new LowPoly().Box(new Vector3(sx, 0.9f, front), new Vector3(0.1f, 1.8f, 0.1f), new Color(0.4f, 0.3f, 0.2f))
            .Box(new Vector3(sx, 1.55f, front + 0.06f), new Vector3(1.3f, 0.8f, 0.04f), new Color(0.95f, 0.92f, 0.82f)).Instance();
        AddChild(post);
        _board = new Label3D
        {
            Text = def.Name, Position = _signAt, FontSize = 40, PixelSize = 0.0028f, Width = 430,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(0.12f, 0.1f, 0.08f), OutlineSize = 0, DoubleSided = false,
        };
        AddChild(_board);
        _label = new Label3D
        {
            Text = def.Name, Position = new Vector3(0, Mathf.Max(3.6f, top + 1.0f), 0), FontSize = 48, PixelSize = 0.006f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = new Color(1f, 0.93f, 0.7f), OutlineSize = 10, OutlineModulate = new Color(0.1f, 0.08f, 0.04f),
        };
        AddChild(_label);
        // the placeholder: a frame that grows with the work (only when there is no model)
        _mat = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.6f, 0.62f) };
        _frame = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.6f, 1f, 1.2f) }, MaterialOverride = _mat, Visible = _built is null };
        AddChild(_frame);
        // where you stand to work: in front of it (the work itself is done at points on the thing, see Places)
        Spot = new Interactable { Position = new Vector3(0, 1.0f, _built is null ? 1.0f : front + 0.5f), CollisionLayer = 2, CollisionMask = 0 };
        Spot.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.8f, 2.0f, 1.0f) } });
        AddChild(Spot);
        Visible = false;
    }

    /// <summary>The model stage step <paramref name="i"/> builds (-1: none).</summary>
    private int StageOf(int i) => i < 0 || i >= Def.Steps.Count ? -1 : Def.Steps[i].Stage ?? i;

    /// <summary>
    /// Where the hands go for the next piece of work: starting, at the crates (or the signboard, for paperwork); then the
    /// model's <c>wp-s&lt;stage&gt;-…</c> points for the step under way; else points across the front of the thing.
    /// </summary>
    public IReadOnlyList<WorkPlace> Places(WorkPoints w)
    {
        Vector3 Here(Vector3 local) => local - Spot.Position;   // the spot is square to the site, so its space is the site's, shifted
        if (!_started)
        {
            if (Def.Parts == PartsBy.None) return new[] { new WorkPlace(Here(_signAt + new Vector3(0.3f, -0.2f, 0.03f)), "check"), new WorkPlace(Here(_signAt + new Vector3(-0.3f, -0.2f, 0.03f)), "place") };
            var c0 = _crates.Count > 0 ? _crates[0].Position : new Vector3(1.6f, 0, 0.4f);
            var c1 = _crates.Count > 1 ? _crates[1].Position : c0 + new Vector3(0.1f, 0, 1f);
            return Def.Parts == PartsBy.Lorry
                ? new[] { new WorkPlace(Here(c0 + new Vector3(0, 0.62f, 0)), "check"), new WorkPlace(Here(c1 + new Vector3(0, 0.62f, 0)), "check") }
                : new[] { new WorkPlace(Here(c1 + new Vector3(0, 0.62f, 0)), "lift"), new WorkPlace(Here(c0 + new Vector3(0, 0.62f, 0)), "place") };
        }
        int stage = StageOf(_step);
        if (stage >= 0 && w.FromModel(_built, $"s{stage}") is { Count: > 0 } fromModel) return fromModel;
        // no points in the model: across the front of the thing (the frame, or the room's work spot), at working height
        int count = Mathf.Clamp(Mathf.CeilToInt(Spot.HoldSeconds() / 1.5f), 2, 4);
        var motions = WorkPoints.Guess(Spot.Prompt(), count);
        float w0 = Mathf.Clamp(_right - _left, 1.2f, 2.4f) * 0.35f;
        return Enumerable.Range(0, count).Select(i =>
        {
            float f = count == 1 ? 0.5f : (float)i / (count - 1);
            float y = motions[i] is "dig" or "tamp" ? 0.08f : i % 2 == 0 ? 1.15f : 0.8f;
            float z = motions[i] is "dig" or "tamp" ? _front + 0.25f : _front + 0.02f;
            return new WorkPlace(Here(new Vector3((f - 0.5f) * 2 * w0, y, z)), motions[i]);
        }).ToList();
    }

    /// <param name="open">Open to you (or under way, or built).</param>
    /// <param name="step">The step under way (0-based; the step count once built).</param>
    /// <param name="started">The parts are unloaded and the work has begun.</param>
    /// <param name="waiting">The step under way waits on someone else: no ghost, nothing to do.</param>
    /// <param name="fraction">How much of it is done, 0..1 (counting clicks).</param>
    /// <param name="line">The floating line above the site.</param>
    /// <param name="board">What the signboard says (the step, or the wait and until when).</param>
    /// <param name="partsHere">The parts are at the site (unloaded, or left by the lorry): crates stand there.</param>
    public void Sync(bool open, int step, bool started, bool built, bool waiting, float fraction, string line, string board, bool partsHere = true)
    {
        Visible = open && !(built && _shownElsewhere);   // where something else shows the work, the sign goes once it's built
        _step = step; _started = started;
        fraction = Mathf.Clamp(fraction, 0, 1);
        if (_stages.Count > 0)
        {
            // stages below the first one this project builds are there already; then whatever its done steps built
            int found = Enumerable.Range(0, Def.Steps.Count).Select(StageOf).Where(x => x >= 0).DefaultIfEmpty(0).Min();
            int doneUpTo = Enumerable.Range(0, Math.Min(step, Def.Steps.Count)).Select(StageOf).DefaultIfEmpty(-1).Max();
            int now = built ? -1 : StageOf(step);
            foreach (var (n, part) in _stages)
            {
                bool solid = built || n < found || n <= doneUpTo;
                bool ghost = !solid && started && !waiting && n == now;
                part.Visible = solid || ghost;
                Ghost(part, ghost);
            }
        }
        else if (_built is not null) _built.Visible = built;
        else
        {
            float h = Mathf.Lerp(0.15f, 2.6f, fraction);
            _frame.Scale = new Vector3(1, h, 1);
            _frame.Position = new Vector3(0, h / 2, 0);
            _mat.AlbedoColor = built ? new Color(0.35f, 0.62f, 0.4f) : fraction > 0 ? new Color(0.85f, 0.7f, 0.3f) : new Color(0.55f, 0.6f, 0.62f);
        }
        // the parts left to fit, in crates: full when they arrive, emptying as you go, gone once built
        int crates = built || !partsHere ? 0 : Mathf.Max(1, Mathf.RoundToInt((1 - fraction) * _crates.Count));
        for (int i = 0; i < _crates.Count; i++) _crates[i].Visible = i < crates;
        _label.Text = line;
        _board.Text = board;
        Spot.CollisionLayer = open && !built ? 2u : 0u;
    }

    /// <summary>The next stage stands as a see-through outline while you build it.</summary>
    private static void Ghost(Node3D part, bool ghost)
    {
        var shadow = ghost ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On;
        foreach (var gi in part.FindChildren("*", "GeometryInstance3D", true, false).OfType<GeometryInstance3D>().Append(part as GeometryInstance3D).OfType<GeometryInstance3D>())
        {
            gi.Transparency = ghost ? 0.6f : 0f;
            gi.CastShadow = shadow;
        }
    }
}
