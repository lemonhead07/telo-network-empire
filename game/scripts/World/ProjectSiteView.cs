using Godot;
using Telo.Sim.Content;

namespace Telo.Game.World;

/// <summary>
/// A project site in the world (projects.json): the thing being built, a signboard, and the spot where you work.
///
/// Geometry and gameplay as one system (EA5, docs/WORK_POINTS.md): the project's model (its <c>model</c> prop) is built in
/// stages. Parts named <c>stage-&lt;n&gt;-…</c> appear once step n is done, and stand as a ghost while step n is under way, so
/// the site shows how far the work has got; everything else in the model is the site as you find it. The model also names
/// where a hand works at each step (<c>wp-s&lt;n&gt;-NN-motion</c> empties), which <see cref="Places"/> hands to
/// <see cref="WorkPoints"/>. A wait (an engineer booked, concrete setting) is written on the signboard.
///
/// Falls back step by step: a model without stages is hidden until built (crates pile up meanwhile); no model at all is the
/// old placeholder frame that grows. Hidden until the project is open to you; once built it stays, finished.
/// </summary>
public partial class ProjectSiteView : Node3D
{
    public ProjectDef Def { get; }
    public Interactable Spot { get; }
    /// <summary>The step under way (0-based), for the work points.</summary>
    public int Step { get; private set; }
    private readonly Label3D _label;
    private readonly Label3D _board;
    private readonly MeshInstance3D _frame;
    private readonly StandardMaterial3D _mat;
    private readonly Node3D? _model;
    private readonly List<(int Step, Node3D Part)> _stages = new();
    private readonly List<Node3D> _crates = new();

    public ProjectSiteView(ProjectDef def)
    {
        Def = def;
        Name = def.Id;
        // the thing being built, if it has a model: its size places the sign, the crates and the work spot
        float left = -1.0f, right = 1.0f, front = 0.6f, top = 1.0f;
        if (def.Model is { } model && Props.HasModel(model))
        {
            _model = Props.Make(model);
            AddChild(_model);
            foreach (var part in Props.Parts(_model, "stage-"))
                if (part.Name.ToString().Split('-') is { Length: > 1 } bits && int.TryParse(bits[1], out int n))
                    _stages.Add((n, part));
            _model.Visible = _stages.Count > 0;   // a model with no stages only shows once built
            var b = Props.Bounds(_model);
            left = Mathf.Min(b.Position.X, -1.0f); right = Mathf.Max(b.End.X, 1.0f); front = Mathf.Max(b.End.Z, 0.6f); top = Mathf.Min(b.End.Y, 6f);
            // crates of parts beside it: they empty as the work goes on
            foreach (var (x, z, y, yaw) in new[] { (0.6f, -0.2f, 0f, 0.1f), (0.7f, 0.8f, 0f, -0.2f), (0.65f, 0.3f, 0.6f, 0.05f), (1.6f, 0.4f, 0f, 0.6f) })
            {
                var c = Props.Make("site.parts-crate", () => new LowPoly().Box(new Vector3(0, 0.3f, 0), new Vector3(0.8f, 0.6f, 0.6f), new Color(0.72f, 0.58f, 0.36f)).Instance());
                c.Position = new Vector3(right + x, y, front + z - 1.0f); c.Rotation = new Vector3(0, yaw, 0); c.Visible = false;
                AddChild(c); _crates.Add(c);
            }
        }
        // the signboard, off to the left of whatever is there: the project's name, the step, or what it waits on
        float sx = left - 0.7f;
        var post = new LowPoly().Box(new Vector3(sx, 0.9f, front), new Vector3(0.1f, 1.8f, 0.1f), new Color(0.4f, 0.3f, 0.2f))
            .Box(new Vector3(sx, 1.55f, front + 0.06f), new Vector3(1.3f, 0.8f, 0.04f), new Color(0.95f, 0.92f, 0.82f)).Instance();
        AddChild(post);
        _board = new Label3D
        {
            Text = def.Name, Position = new Vector3(sx, 1.55f, front + 0.085f), FontSize = 40, PixelSize = 0.0028f, Width = 430,
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
        _frame = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.6f, 1f, 1.2f) }, MaterialOverride = _mat, Visible = _model is null };
        AddChild(_frame);
        // where you stand to work: in front of it (the work itself is done at points on the model, see Places)
        Spot = new Interactable { Position = new Vector3(0, 1.0f, _model is null ? 1.0f : front + 0.5f), CollisionLayer = 2, CollisionMask = 0 };
        Spot.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.8f, 2.0f, 1.0f) } });
        AddChild(Spot);
        Visible = false;
    }

    /// <summary>Where the hands go for the step under way: the model's <c>wp-s&lt;step&gt;-…</c> points, else spread over the spot.</summary>
    public IReadOnlyList<WorkPlace> Places(WorkPoints w) =>
        w.FromModel(_model, $"s{Step}") is { Count: > 0 } fromModel ? fromModel : w.Spread();

    /// <param name="open">Open to you (or under way, or built).</param>
    /// <param name="stepsDone">How many whole steps are done (all of them once built).</param>
    /// <param name="fraction">How much of it is done, 0..1 (counting clicks).</param>
    /// <param name="waiting">The step under way is waiting on someone else: no ghost, nothing to do.</param>
    /// <param name="line">The floating line above the site.</param>
    /// <param name="board">What the signboard says (the wait, when there is one).</param>
    public void Sync(bool open, int stepsDone, float fraction, bool built, bool waiting, string line, string board)
    {
        Visible = open;
        Step = stepsDone;
        fraction = Mathf.Clamp(fraction, 0, 1);
        if (_model is not null && _stages.Count > 0)
        {
            bool started = fraction > 0 || stepsDone > 0;
            foreach (var (n, part) in _stages)
            {
                bool done = built || n < stepsDone;
                bool ghost = !done && started && !waiting && n == stepsDone;
                part.Visible = done || ghost;
                Ghost(part, ghost);
            }
        }
        else if (_model is not null) _model.Visible = built;
        else
        {
            float h = Mathf.Lerp(0.15f, 2.6f, fraction);
            _frame.Scale = new Vector3(1, h, 1);
            _frame.Position = new Vector3(0, h / 2, 0);
            _mat.AlbedoColor = built ? new Color(0.35f, 0.62f, 0.4f) : fraction > 0 ? new Color(0.85f, 0.7f, 0.3f) : new Color(0.55f, 0.6f, 0.62f);
        }
        // the parts left to fit, in crates: full before you start, emptying as you go, gone once built
        int crates = built ? 0 : Mathf.Max(1, Mathf.RoundToInt((1 - fraction) * _crates.Count));
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
