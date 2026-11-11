using Godot;

namespace Telo.Game.World;

/// <summary>
/// Code-built, flat-shaded, vertex-coloured meshes. This is the project's "modelling tool":
/// every prop is a few coloured primitives, so the whole game shares one material and one look.
/// </summary>
public sealed class LowPoly
{
    private readonly List<Vector3> _v = new(), _n = new();
    private readonly List<Color> _c = new();

    private static StandardMaterial3D? _mat;
    public static StandardMaterial3D Material => _mat ??= new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.92f };

    public int TriangleCount => _v.Count / 3;

    /// <summary>Add a triangle whose visible side faces <paramref name="outward"/>.</summary>
    public LowPoly Tri(Vector3 a, Vector3 b, Vector3 c, Color col, Vector3 outward)
    {
        var n = (c - a).Cross(b - a); // front normal for Godot's clockwise winding
        if (n.Dot(outward) < 0) { (b, c) = (c, b); n = -n; }
        n = n.Normalized();
        _v.Add(a); _v.Add(b); _v.Add(c);
        _n.Add(n); _n.Add(n); _n.Add(n);
        _c.Add(col); _c.Add(col); _c.Add(col);
        return this;
    }

    public LowPoly Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, Vector3 outward) =>
        Tri(a, b, c, col, outward).Tri(a, c, d, col, outward);

    public LowPoly Box(Vector3 center, Vector3 size, Color col)
    {
        var h = size / 2;
        Vector3 P(int x, int y, int z) => center + new Vector3(h.X * x, h.Y * y, h.Z * z);
        Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), col, Vector3.Back);
        Quad(P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), col * 0.92f, Vector3.Forward);
        Quad(P(1, -1, -1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), col * 0.85f, Vector3.Right);
        Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), col * 0.88f, Vector3.Left);
        Quad(P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), col * 1.05f, Vector3.Up);
        Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), col * 0.7f, Vector3.Down);
        return this;
    }

    /// <summary>Tapered cylinder along +Y. topRadius 0 makes a cone. Shaded slightly per side for a faceted look.</summary>
    public LowPoly Cylinder(Vector3 baseCenter, float radius, float topRadius, float height, int sides, Color col)
    {
        var top = baseCenter + Vector3.Up * height;
        for (int i = 0; i < sides; i++)
        {
            float a0 = Mathf.Tau * i / sides, a1 = Mathf.Tau * (i + 1) / sides;
            var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            var outward = d0 + d1 + Vector3.Up * 0.3f;
            var shade = col * (0.86f + 0.14f * Mathf.Abs(Mathf.Sin(a0 * 1.7f + 0.6f)));
            shade.A = 1;
            Tri(baseCenter + d0 * radius, baseCenter + d1 * radius, top + d1 * topRadius, shade, outward);
            if (topRadius > 0.001f)
            {
                Tri(baseCenter + d0 * radius, top + d1 * topRadius, top + d0 * topRadius, shade, outward);
                Tri(top, top + d0 * topRadius, top + d1 * topRadius, col * 1.05f, Vector3.Up);
            }
            Tri(baseCenter, baseCenter + d0 * radius, baseCenter + d1 * radius, col * 0.7f, Vector3.Down);
        }
        return this;
    }

    /// <summary>Faceted blob (two cones base to base) — foliage, rocks.</summary>
    public LowPoly Gem(Vector3 center, float radius, float up, float down, int sides, Color col)
    {
        for (int i = 0; i < sides; i++)
        {
            float a0 = Mathf.Tau * i / sides, a1 = Mathf.Tau * (i + 1) / sides;
            var p0 = center + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius;
            var p1 = center + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius;
            var mid = (p0 + p1) / 2 - center;
            var shade = col * (0.84f + 0.16f * Mathf.Abs(Mathf.Sin(a0 * 2.3f + 1f)));
            shade.A = 1;
            Tri(p0, p1, center + Vector3.Up * up, shade, mid + Vector3.Up);
            Tri(p0, p1, center + Vector3.Down * down, shade * 0.8f, mid + Vector3.Down);
        }
        return this;
    }

    /// <summary>Gable roof over a rectangle centred at <paramref name="center"/> (ridge runs along X).</summary>
    public LowPoly GableRoof(Vector3 center, float width, float depth, float rise, Color col)
    {
        float w = width / 2, d = depth / 2;
        Vector3 a = center + new Vector3(-w, 0, d), b = center + new Vector3(w, 0, d), c = center + new Vector3(w, 0, -d), e = center + new Vector3(-w, 0, -d);
        Vector3 r0 = center + new Vector3(-w, rise, 0), r1 = center + new Vector3(w, rise, 0);
        Quad(a, b, r1, r0, col, Vector3.Back + Vector3.Up);
        Quad(e, c, r1, r0, col * 0.85f, Vector3.Forward + Vector3.Up);
        Tri(a, e, r0, col * 0.75f, Vector3.Left);
        Tri(b, c, r1, col * 0.75f, Vector3.Right);
        return this;
    }

    public ArrayMesh Build()
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = _v.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = _n.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = _c.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, Material);
        return mesh;
    }

    public Vector3[] Faces() => _v.ToArray();

    public MeshInstance3D Instance(string name = "Mesh") => new() { Name = name, Mesh = Build() };
}
