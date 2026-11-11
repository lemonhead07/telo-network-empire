using Godot;
using Telo.Sim.Core;
using Telo.Sim.Planet;

namespace Telo.Game.World;

/// <summary>
/// Turns the sim's PlanetGen (a pure height/biome function) into a low-poly world:
/// cube-sphere terrain chunks with collision, an ocean, and scattered trees and rocks.
/// </summary>
public static class PlanetBuilder
{
    public const int ChunksPerEdge = 12, QuadsPerChunk = 20;
    public const int Edge = ChunksPerEdge * QuadsPerChunk;
    public const double SkipBelowM = -8; // seabed deeper than this is never seen or touched

    private static readonly (Vector3 N, Vector3 U, Vector3 V)[] Faces =
    {
        (Vector3.Right, Vector3.Forward, Vector3.Up), (Vector3.Left, Vector3.Back, Vector3.Up),
        (Vector3.Up, Vector3.Right, Vector3.Back), (Vector3.Down, Vector3.Right, Vector3.Forward),
        (Vector3.Back, Vector3.Right, Vector3.Up), (Vector3.Forward, Vector3.Left, Vector3.Up),
    };

    public static V3 ToSim(Vector3 v) => new(v.X, v.Y, v.Z);
    public static Vector3 ToGodot(V3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    private static Vector3 FaceDir(int face, float u, float v)
    {
        var f = Faces[face];
        // tan-warp keeps cells nearly the same size across the face
        return (f.N + f.U * Mathf.Tan(u * Mathf.Pi / 4) + f.V * Mathf.Tan(v * Mathf.Pi / 4)).Normalized();
    }

    public static Color BiomeColor(Biome b) => b switch
    {
        Biome.Seabed => new Color(0.62f, 0.60f, 0.46f),
        Biome.Beach => new Color(0.88f, 0.81f, 0.58f),
        Biome.Grassland => new Color(0.47f, 0.64f, 0.29f),
        Biome.Forest => new Color(0.29f, 0.48f, 0.24f),
        Biome.Desert => new Color(0.86f, 0.69f, 0.41f),
        Biome.Tundra => new Color(0.58f, 0.61f, 0.50f),
        Biome.Snow => new Color(0.94f, 0.96f, 0.98f),
        Biome.Rock => new Color(0.50f, 0.47f, 0.45f),
        _ => new Color(0.52f, 0.60f, 0.36f), // town: mown verges and trodden grass
    };

    public static Node3D Build(Node3D parent, PlanetGen gen)
    {
        var root = new Node3D { Name = "Planet" };
        parent.AddChild(root);
        var body = new StaticBody3D { Name = "Terrain" };
        root.AddChild(body);
        float R = (float)gen.RadiusM;

        for (int face = 0; face < 6; face++)
        {
            int n = Edge + 1;
            var pos = new Vector3[n * n];
            var height = new float[n * n];
            var biome = new Biome[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    var dir = FaceDir(face, i * 2f / Edge - 1, j * 2f / Edge - 1);
                    var sd = ToSim(dir);
                    float h = (float)gen.Height(sd);
                    height[j * n + i] = h;
                    biome[j * n + i] = gen.BiomeAt(sd);
                    pos[j * n + i] = dir * (R + h);
                }

            for (int cj = 0; cj < ChunksPerEdge; cj++)
                for (int ci = 0; ci < ChunksPerEdge; ci++)
                {
                    var lp = new LowPoly();
                    for (int qj = 0; qj < QuadsPerChunk; qj++)
                        for (int qi = 0; qi < QuadsPerChunk; qi++)
                        {
                            int i = ci * QuadsPerChunk + qi, j = cj * QuadsPerChunk + qj;
                            int a = j * n + i, b = a + 1, c = a + n + 1, d = a + n;
                            // alternate the diagonal so the facets don't all lean one way
                            if (((i + j) & 1) == 0) { AddTri(lp, pos, height, biome, a, b, c, i, j); AddTri(lp, pos, height, biome, a, c, d, i, j + 7); }
                            else { AddTri(lp, pos, height, biome, a, b, d, i, j); AddTri(lp, pos, height, biome, b, c, d, i, j + 7); }
                        }
                    if (lp.TriangleCount == 0) continue;
                    root.AddChild(lp.Instance($"T{face}_{ci}_{cj}"));
                    body.AddChild(new CollisionShape3D { Shape = new ConcavePolygonShape3D { Data = lp.Faces() } });
                }
        }

        root.AddChild(new MeshInstance3D
        {
            Name = "Ocean",
            Mesh = new SphereMesh { Radius = R, Height = R * 2, RadialSegments = 192, Rings = 96 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.10f, 0.34f, 0.52f), Roughness = 0.18f, Metallic = 0.15f },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        Scatter(root, gen);
        return root;
    }

    private static void AddTri(LowPoly lp, Vector3[] pos, float[] h, Biome[] biome, int a, int b, int c, int qi, int qj)
    {
        if (h[a] < SkipBelowM && h[b] < SkipBelowM && h[c] < SkipBelowM) return;
        var centroid = (pos[a] + pos[b] + pos[c]) / 3;
        var up = centroid.Normalized();
        var normal = (pos[b] - pos[a]).Cross(pos[c] - pos[a]).Normalized();
        float slope = Mathf.Abs(normal.Dot(up));
        float hAvg = (h[a] + h[b] + h[c]) / 3;

        var bio = biome[a];
        if (hAvg < 0) bio = Biome.Seabed;
        var col = BiomeColor(bio);
        if (bio is not (Biome.Seabed or Biome.Beach or Biome.Town))
        {
            if (slope < 0.74f) col = BiomeColor(Biome.Rock) * (bio == Biome.Snow ? 1.25f : 1f);
            else if (slope < 0.84f) col = col.Lerp(BiomeColor(Biome.Rock), 0.5f);
        }
        // small per-facet variation: "low poly, but minorly detailed"
        uint k = (uint)(qi * 73856093) ^ (uint)(qj * 19349663) ^ (uint)(a * 83492791);
        float jitter = 0.93f + (k % 1000) / 1000f * 0.14f;
        col = col * jitter * (1f + Mathf.Clamp(hAvg, 0, 150) / 900f);
        col.A = 1;
        lp.Tri(pos[a], pos[b], pos[c], col, up);
    }

    // ---- props -------------------------------------------------------------------------------
    private static void Scatter(Node3D root, PlanetGen gen)
    {
        var pine = new LowPoly()
            .Cylinder(Vector3.Zero, 0.2f, 0.14f, 1.3f, 5, new Color(0.36f, 0.25f, 0.16f))
            .Cylinder(new Vector3(0, 1.0f, 0), 1.5f, 0, 2.6f, 6, new Color(0.20f, 0.40f, 0.22f))
            .Cylinder(new Vector3(0, 2.4f, 0), 1.1f, 0, 2.2f, 6, new Color(0.23f, 0.45f, 0.25f))
            .Cylinder(new Vector3(0, 3.7f, 0), 0.7f, 0, 1.7f, 6, new Color(0.27f, 0.50f, 0.28f)).Build();
        var oak = new LowPoly()
            .Cylinder(Vector3.Zero, 0.25f, 0.18f, 1.8f, 5, new Color(0.38f, 0.27f, 0.17f))
            .Gem(new Vector3(0, 3.0f, 0), 1.7f, 1.6f, 1.3f, 7, new Color(0.36f, 0.58f, 0.24f))
            .Gem(new Vector3(0.7f, 2.5f, 0.4f), 1.0f, 0.9f, 0.7f, 6, new Color(0.32f, 0.53f, 0.22f)).Build();
        var cactus = new LowPoly()
            .Cylinder(Vector3.Zero, 0.28f, 0.22f, 2.4f, 6, new Color(0.33f, 0.52f, 0.30f))
            .Box(new Vector3(0.5f, 1.3f, 0), new Vector3(0.6f, 0.2f, 0.2f), new Color(0.31f, 0.50f, 0.29f))
            .Box(new Vector3(0.72f, 1.65f, 0), new Vector3(0.2f, 0.7f, 0.2f), new Color(0.33f, 0.52f, 0.30f)).Build();
        var rock = new LowPoly()
            .Gem(new Vector3(0, 0.25f, 0), 0.9f, 0.6f, 0.4f, 5, new Color(0.52f, 0.50f, 0.48f))
            .Gem(new Vector3(0.6f, 0.1f, 0.3f), 0.5f, 0.35f, 0.3f, 4, new Color(0.46f, 0.44f, 0.43f)).Build();

        // Bucket props by region so distant ones can be culled (VisibilityRangeEnd).
        var lists = new Dictionary<(Mesh, int), List<Transform3D>>();
        const int Regions = 5;
        var rng = new DetRng(DetRng.Derive(gen.Seed, "scatter"));
        float R = (float)gen.RadiusM;
        var town = ToGodot(gen.Town.Dir);

        for (int face = 0; face < 6; face++)
            for (int k = 0; k < 45000; k++)
            {
                float fu = rng.Next(20001) / 10000f - 1, fv = rng.Next(20001) / 10000f - 1;
                var dir = FaceDir(face, fu, fv);
                int region = face * 100 + Math.Min(Regions - 1, (int)((fu + 1) / 2 * Regions)) * 10 + Math.Min(Regions - 1, (int)((fv + 1) / 2 * Regions));
                int roll = rng.Next(1000);
                float scale = 0.75f + rng.Next(1000) / 1000f * 0.9f;
                int spin = rng.Next(628);
                if ((dir - town).Length() * R < gen.Town.RadiusM * 1.25f) continue;
                var sd = ToSim(dir);
                Mesh? pick = gen.BiomeAt(sd) switch
                {
                    Biome.Forest => roll < 620 ? (roll % 4 == 0 ? oak : pine) : roll > 985 ? rock : null,
                    Biome.Grassland => roll < 45 ? oak : roll > 988 ? rock : null,
                    Biome.Tundra => roll < 70 ? pine : roll > 940 ? rock : null,
                    Biome.Desert => roll < 40 ? cactus : roll > 955 ? rock : null,
                    Biome.Rock or Biome.Snow => roll > 800 ? rock : null,
                    Biome.Beach => roll > 985 ? rock : null,
                    _ => null,
                };
                if (pick is null) continue;
                if (pick == rock) scale *= 1.4f;
                var t = dir.Cross(Mathf.Abs(dir.Y) < 0.9f ? Vector3.Up : Vector3.Right).Normalized();
                var basis = new Basis(t, dir, t.Cross(dir)).Rotated(dir, spin / 100f).Scaled(Vector3.One * scale);
                if (!lists.TryGetValue((pick, region), out var list)) lists[(pick, region)] = list = new();
                list.Add(new Transform3D(basis, dir * (R + (float)gen.Height(sd) - 0.15f)));
            }

        foreach (var ((mesh, _), xforms) in lists)
        {
            // Instances are stored relative to the region centre so the node origin is meaningful for distance culling.
            var centre = Vector3.Zero;
            foreach (var x in xforms) centre += x.Origin;
            centre /= xforms.Count;
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = xforms.Count };
            for (int i = 0; i < xforms.Count; i++) mm.SetInstanceTransform(i, new Transform3D(xforms[i].Basis, xforms[i].Origin - centre));
            root.AddChild(new MultiMeshInstance3D { Multimesh = mm, Position = centre, VisibilityRangeEnd = 1800, VisibilityRangeEndMargin = 150, VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Disabled });
        }
    }
}
