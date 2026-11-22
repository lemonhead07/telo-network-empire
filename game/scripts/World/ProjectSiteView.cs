using Godot;
using Telo.Sim.Content;

namespace Telo.Game.World;

/// <summary>
/// A project site in the world (projects.json): a signboard with the project's name, the work under way, and the spot where
/// you hold E to do the next one. With a model (the project's <c>model</c> prop): parts crates pile up as the steps are done
/// and the finished thing stands there once it is built. Without one: the old placeholder frame that grows.
/// Hidden until the project is open to you; once built it stays, finished.
/// </summary>
public partial class ProjectSiteView : Node3D
{
    public ProjectDef Def { get; }
    public Interactable Spot { get; }
    private readonly Label3D _label;
    private readonly MeshInstance3D _frame;
    private readonly StandardMaterial3D _mat;
    private readonly Node3D? _built;
    private readonly List<Node3D> _crates = new();

    public ProjectSiteView(ProjectDef def)
    {
        Def = def;
        Name = def.Id;
        // the finished thing, if it has a model: its size places the sign and the work spot
        float left = -1.0f, front = 0.6f, top = 1.0f;
        if (def.Model is { } model && Props.HasModel(model))
        {
            _built = Props.Make(model);
            _built.Visible = false;
            AddChild(_built);
            var b = Props.Bounds(_built);
            left = Mathf.Min(b.Position.X, -1.0f); front = Mathf.Max(b.End.Z, 0.6f); top = Mathf.Min(b.End.Y, 6f);
            // crates of parts, stacked as the work goes on
            foreach (var (x, z, y, yaw) in new[] { (-0.5f, front + 0.9f, 0f, 0.1f), (0.5f, front + 1.0f, 0f, -0.2f), (0f, front + 0.95f, 0.6f, 0.05f), (1.4f, front + 0.7f, 0f, 0.6f) })
            {
                var c = Props.Make("site.parts-crate", () => new LowPoly().Box(new Vector3(0, 0.3f, 0), new Vector3(0.8f, 0.6f, 0.6f), new Color(0.72f, 0.58f, 0.36f)).Instance());
                c.Position = new Vector3(x, y, z); c.Rotation = new Vector3(0, yaw, 0); c.Visible = false;
                AddChild(c); _crates.Add(c);
            }
        }
        // the signboard, off to the left of whatever is there
        float sx = left - 0.6f;
        var post = new LowPoly().Box(new Vector3(sx, 0.9f, front), new Vector3(0.1f, 1.8f, 0.1f), new Color(0.4f, 0.3f, 0.2f))
            .Box(new Vector3(sx, 1.7f, front + 0.06f), new Vector3(1.3f, 0.7f, 0.04f), new Color(0.95f, 0.92f, 0.82f)).Instance();
        AddChild(post);
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
        // where you work: in front of it
        Spot = new Interactable { Position = new Vector3(0, 1.0f, _built is null ? 1.0f : front + 0.5f), CollisionLayer = 2, CollisionMask = 0 };
        Spot.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.8f, 2.0f, 1.0f) } });
        AddChild(Spot);
        Visible = false;
    }

    /// <param name="open">Open to you (or under way, or built).</param>
    /// <param name="fraction">How much of it is done, 0..1.</param>
    /// <param name="partsHere">The parts are at the site (unloaded, or left by the lorry): crates stand there.</param>
    public void Sync(bool open, float fraction, bool built, string line, bool partsHere = true)
    {
        Visible = open;
        fraction = Mathf.Clamp(fraction, 0, 1);
        if (_built is not null)
        {
            _built.Visible = built;
            int crates = built || !partsHere ? 0 : 1 + (int)(fraction * (_crates.Count - 1) + 0.5f);
            for (int i = 0; i < _crates.Count; i++) _crates[i].Visible = i < crates;
        }
        else
        {
            float h = Mathf.Lerp(0.15f, 2.6f, fraction);
            _frame.Scale = new Vector3(1, h, 1);
            _frame.Position = new Vector3(0, h / 2, 0);
            _mat.AlbedoColor = built ? new Color(0.35f, 0.62f, 0.4f) : fraction > 0 ? new Color(0.85f, 0.7f, 0.3f) : new Color(0.55f, 0.6f, 0.62f);
        }
        _label.Text = line;
        Spot.CollisionLayer = open && !built ? 2u : 0u;
    }
}
