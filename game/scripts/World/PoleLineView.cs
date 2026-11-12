using Godot;
using Telo.Sim.Planet;

namespace Telo.Game.World;

/// <summary>
/// A pole line being built by hand along a road: the planted poles, the strung spans, a marker where the next pole goes
/// and one where the next span is strung. It only shows progress; the rules are in the sim (Build/LineRules).
/// </summary>
public partial class PoleLineView : Node3D
{
    public const float PoleTop = 6.85f;

    private readonly List<Transform3D> _poles = new();
    private readonly Vector3 _buildingEnd;
    private readonly List<Node3D> _poleNodes = new(), _spanNodes = new();
    private readonly Node3D _plantMark, _stringMark;
    public Interactable PlantSpot { get; }
    public Interactable StringSpot { get; }
    public int Count => _poles.Count;
    public Vector3 PolePosition(int i) => _poles[Math.Clamp(i, 0, _poles.Count - 1)].Origin;

    public PoleLineView(PlanetGen gen, IReadOnlyList<V3> sites, Vector3 buildingEnd, PhysicsDirectSpaceState3D space)
    {
        Name = "PoleLine";
        _buildingEnd = buildingEnd;
        float R = (float)gen.RadiusM;
        var ground = sites.Select(s =>
        {
            var dir = PlanetBuilder.ToGodot(s);
            var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(dir * (R + 400), dir * (R - 100), 1));
            return hit.Count > 0 ? (Vector3)hit["position"] : dir * (R + (float)gen.Height(s));
        }).ToList();
        for (int i = 0; i < ground.Count; i++)
        {
            var up = ground[i].Normalized();
            var along = ground[Math.Min(i + 1, ground.Count - 1)] - ground[Math.Max(i - 1, 0)];
            var side = along.Cross(up).Normalized();           // the crossarm sits square to the line
            _poles.Add(new Transform3D(new Basis(up.Cross(side), up, side), ground[i]));
        }
        (_plantMark, PlantSpot) = Marker(new Color(1f, 0.82f, 0.15f));
        (_stringMark, StringSpot) = Marker(new Color(0.4f, 0.9f, 1f));
    }

    private (Node3D, Interactable) Marker(Color col)
    {
        var mark = new Node3D { Visible = false };
        mark.AddChild(new MeshInstance3D
        {
            Position = new Vector3(0, 0.6f, 0), Mesh = new BoxMesh { Size = new Vector3(0.25f, 1.2f, 0.25f) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = col, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        mark.AddChild(new Label3D
        {
            Text = "▼", Position = new Vector3(0, 9f, 0), FontSize = 96, PixelSize = 0.0016f, FixedSize = true, NoDepthTest = true,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Modulate = col, OutlineSize = 16, OutlineModulate = new Color(0.1f, 0.08f, 0.02f),
        });
        var hot = new Interactable { CollisionLayer = 0, CollisionMask = 0, Position = new Vector3(0, 1f, 0) };
        hot.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.2f, 2.2f, 1.2f) } });
        mark.AddChild(hot);
        AddChild(mark);
        return (mark, hot);
    }

    /// <summary>Show the line as far as it has got.</summary>
    /// <param name="beacons">Show the tall ▼ markers (only once the line is the player's business).</param>
    public void Sync(int planted, int strung, bool beacons)
    {
        foreach (var mark in new[] { _plantMark, _stringMark }) mark.GetChild<Label3D>(1).Visible = beacons;
        int n = _poles.Count;
        for (int i = _poleNodes.Count; i < Math.Min(planted, n); i++)
        {
            var pole = new MeshInstance3D { Mesh = TownBuilder.PoleMesh(), Transform = _poles[i] };
            AddChild(pole);
            _poleNodes.Add(pole);
        }
        for (int i = _spanNodes.Count; i < Math.Min(strung, n); i++)
        {
            var a = _poles[i].Origin + _poles[i].Basis.Y * PoleTop;
            var b = i + 1 < n ? _poles[i + 1].Origin + _poles[i + 1].Basis.Y * PoleTop : _buildingEnd;
            var span = Wire(a, b);
            AddChild(span);
            _spanNodes.Add(span);
        }
        Place(_plantMark, PlantSpot, planted < n ? _poles[planted] : null);
        // the next span is strung from its far end: the next pole along, or the last pole for the run to the building
        int at = strung < planted - 1 ? strung + 1 : planted == n && strung == n - 1 ? n - 1 : -1;
        Place(_stringMark, StringSpot, at >= 0 ? _poles[at] : null);
    }

    private static void Place(Node3D mark, Interactable hot, Transform3D? at)
    {
        mark.Visible = at is not null;
        hot.CollisionLayer = at is not null ? 2u : 0u;
        if (at is { } t) mark.Transform = t;
    }

    private static MeshInstance3D Wire(Vector3 a, Vector3 b)
    {
        var d = b - a; float len = d.Length();
        var mi = new LowPoly().Box(Vector3.Zero, new Vector3(0.05f, 0.05f, len), new Color(0.06f, 0.06f, 0.07f)).Instance();
        var z = d / len;
        var x = a.Normalized().Cross(z).Normalized();
        mi.Transform = new Transform3D(new Basis(x, z.Cross(x), z), (a + b) / 2);
        mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return mi;
    }
}
