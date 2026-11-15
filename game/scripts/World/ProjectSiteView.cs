using Godot;
using Telo.Sim.Content;

namespace Telo.Game.World;

/// <summary>
/// A project site in the world (projects.json): a signboard with the project's name, a placeholder frame that fills in as
/// the steps are done, and the spot where you hold E to do the next one. Placeholder boxes until models (D15).
/// Hidden until the project is open to you; once built it stays, finished.
/// </summary>
public partial class ProjectSiteView : Node3D
{
    public ProjectDef Def { get; }
    public Interactable Spot { get; }
    private readonly Label3D _label;
    private readonly MeshInstance3D _frame;
    private readonly StandardMaterial3D _mat;

    public ProjectSiteView(ProjectDef def)
    {
        Def = def;
        Name = def.Id;
        // the signboard
        var post = new LowPoly().Box(new Vector3(-1.6f, 0.9f, 0), new Vector3(0.1f, 1.8f, 0.1f), new Color(0.4f, 0.3f, 0.2f))
            .Box(new Vector3(-1.6f, 1.7f, 0.06f), new Vector3(1.3f, 0.7f, 0.04f), new Color(0.95f, 0.92f, 0.82f)).Instance();
        AddChild(post);
        _label = new Label3D
        {
            Text = def.Name, Position = new Vector3(0, 3.6f, 0), FontSize = 48, PixelSize = 0.006f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = new Color(1f, 0.93f, 0.7f), OutlineSize = 10, OutlineModulate = new Color(0.1f, 0.08f, 0.04f),
        };
        AddChild(_label);
        // the thing being built: a frame that grows with the work (a real model later)
        _mat = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.6f, 0.62f) };
        _frame = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.6f, 1f, 1.2f) }, MaterialOverride = _mat };
        AddChild(_frame);
        // where you work
        Spot = new Interactable { Position = new Vector3(0, 1.0f, 1.0f), CollisionLayer = 2, CollisionMask = 0 };
        Spot.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.8f, 2.0f, 1.0f) } });
        AddChild(Spot);
        Visible = false;
    }

    /// <param name="open">Open to you (or under way, or built).</param>
    /// <param name="fraction">How much of it is done, 0..1.</param>
    public void Sync(bool open, float fraction, bool built, string line)
    {
        Visible = open;
        float h = Mathf.Lerp(0.15f, 2.6f, Mathf.Clamp(fraction, 0, 1));
        _frame.Scale = new Vector3(1, h, 1);
        _frame.Position = new Vector3(0, h / 2, 0);
        _mat.AlbedoColor = built ? new Color(0.35f, 0.62f, 0.4f) : fraction > 0 ? new Color(0.85f, 0.7f, 0.3f) : new Color(0.55f, 0.6f, 0.62f);
        _label.Text = line;
        Spot.CollisionLayer = open && !built ? 2u : 0u;
    }
}
