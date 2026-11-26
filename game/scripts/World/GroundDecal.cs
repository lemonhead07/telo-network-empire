using Godot;

namespace Telo.Game.World;

/// <summary>
/// Bare earth laid on the ground where people and tractors wear it (LOOK1): strips along the road verges and under
/// hedgerows, drawn with game/shaders/earth.gdshader. Looks only: no collision, no shadows. Each corner's alpha says
/// how much earth shows there; the shader fades and breaks it up with the ground's own noise.
/// </summary>
public sealed class GroundDecal
{
    private static ShaderMaterial? _mat;
    public static ShaderMaterial Material
    {
        get
        {
            if (_mat is not null) return _mat;
            _mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/earth.gdshader") };
            _mat.SetShaderParameter("noise3d", PlanetBuilder.Noise);
            return _mat;
        }
    }

    private readonly List<Vector3> _v = new(), _n = new();
    private readonly List<Color> _c = new();
    public int Count => _v.Count / 3;

    /// <summary>A quad from a-b (inner edge, earth `inner` strong) to d-c (outer edge, `outer`), facing `up`.</summary>
    public GroundDecal Strip(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float inner, float outer, Vector3 up)
    {
        Tri(a, b, c, inner, inner, outer, up);
        Tri(a, c, d, inner, outer, outer, up);
        return this;
    }

    private void Tri(Vector3 a, Vector3 b, Vector3 c, float aa, float ab, float ac, Vector3 up)
    {
        var n = (c - a).Cross(b - a);   // Godot's clockwise front face
        if (n.Dot(up) < 0) { (b, c) = (c, b); (ab, ac) = (ac, ab); }
        _v.Add(a); _v.Add(b); _v.Add(c);
        var u = up.Normalized();
        _n.Add(u); _n.Add(u); _n.Add(u);
        _c.Add(new Color(1, 1, 1, aa)); _c.Add(new Color(1, 1, 1, ab)); _c.Add(new Color(1, 1, 1, ac));
    }

    public ArrayMesh Build()
    {
        var mesh = new ArrayMesh();
        if (_v.Count == 0) return mesh;
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = _v.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = _n.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = _c.ToArray();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, Material);
        return mesh;
    }

    public MeshInstance3D Instance(string name) =>
        new() { Name = name, Mesh = Build(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
}
