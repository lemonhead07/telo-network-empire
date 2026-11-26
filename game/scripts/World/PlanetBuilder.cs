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

    /// <summary>The ground's material (game/shaders/terrain.gdshader), shared by every chunk. Settings change its detail.</summary>
    public static ShaderMaterial? TerrainMaterial { get; private set; }

    /// <summary>How much detail the ground's shader draws: 0 Low, 1 Medium, 2 High (GameSettings.Apply).</summary>
    public static void SetDetail(int level) => TerrainMaterial?.SetShaderParameter("detail", level);

    private static NoiseTexture3D? _noise;
    /// <summary>One small tiling 3D noise, made in code (no image files). The ground and the earth along verges sample it at several sizes.</summary>
    public static NoiseTexture3D Noise => _noise ??= new NoiseTexture3D
    {
        Width = 64, Height = 64, Depth = 64, Seamless = true, Normalize = true,
        Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Seed = 1983, Frequency = 0.07f, FractalOctaves = 3, FractalGain = 0.5f },
    };

    private static ShaderMaterial MakeTerrainMaterial()
    {
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/terrain.gdshader") };
        mat.SetShaderParameter("noise3d", Noise);
        mat.SetShaderParameter("detail", (int)GameSettings.Graphics);
        return mat;
    }

    /// <summary>Corners whose smooth normal leans further than this from their facet's keep the facet's: cliffs and cuttings stay crisp.</summary>
    private const float CreaseCos = 0.82f;   // about 35 degrees

    public static Node3D Build(Node3D parent, PlanetGen gen)
    {
        var root = new Node3D { Name = "Planet" };
        parent.AddChild(root);
        var body = new StaticBody3D { Name = "Terrain" };
        root.AddChild(body);
        float R = (float)gen.RadiusM;
        TerrainMaterial = MakeTerrainMaterial();
        SetBiomes(TerrainMaterial, R);

        for (int face = 0; face < 6; face++)
        {
            // the grid has a one-vertex border reaching into the next face, so normals at the edges match across the seam
            int n = Edge + 3;
            var pos = new Vector3[n * n];
            var height = new float[n * n];
            var biome = new Biome[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    var dir = FaceDir(face, (i - 1) * 2f / Edge - 1, (j - 1) * 2f / Edge - 1);
                    var sd = ToSim(dir);
                    float h = (float)gen.Height(sd);
                    height[j * n + i] = h;
                    pos[j * n + i] = dir * (R + h);
                    if (i > 0 && j > 0 && i < n - 1 && j < n - 1) biome[j * n + i] = gen.BiomeAt(sd);
                }
            // smooth normals from the neighbours either side (central differences), facing out
            var norm = new Vector3[n * n];
            for (int j = 1; j < n - 1; j++)
                for (int i = 1; i < n - 1; i++)
                {
                    int k = j * n + i;
                    var nn = (pos[k + 1] - pos[k - 1]).Cross(pos[k + n] - pos[k - n]).Normalized();
                    norm[k] = nn.Dot(pos[k]) < 0 ? -nn : nn;
                }

            for (int cj = 0; cj < ChunksPerEdge; cj++)
                for (int ci = 0; ci < ChunksPerEdge; ci++)
                {
                    var t = new TerrainChunk();
                    for (int qj = 0; qj < QuadsPerChunk; qj++)
                        for (int qi = 0; qi < QuadsPerChunk; qi++)
                        {
                            int i = ci * QuadsPerChunk + qi + 1, j = cj * QuadsPerChunk + qj + 1;
                            int a = j * n + i, b = a + 1, c = a + n + 1, d = a + n;
                            // alternate the diagonal so the facets don't all lean one way
                            if (((i + j) & 1) == 0) { t.Tri(pos, norm, height, biome, a, b, c); t.Tri(pos, norm, height, biome, a, c, d); }
                            else { t.Tri(pos, norm, height, biome, a, b, d); t.Tri(pos, norm, height, biome, b, c, d); }
                        }
                    if (t.Count == 0) continue;
                    root.AddChild(new MeshInstance3D { Name = $"T{face}_{ci}_{cj}", Mesh = t.Build(TerrainMaterial) });
                    body.AddChild(new CollisionShape3D { Shape = new ConcavePolygonShape3D { Data = t.Faces() } });
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

    /// <summary>
    /// One chunk of ground. Each corner has its position and normal (smooth, or the facet's at a crease); each triangle
    /// also carries its three corners' biomes (CUSTOM0, one byte each) and each corner its place in the triangle (COLOR,
    /// barycentric), so terrain.gdshader can give every spot the nearest corner's ground with a wobbly edge: borders
    /// between sand and grass stay crisp instead of smearing across the 16 m grid.
    /// </summary>
    private sealed class TerrainChunk
    {
        private readonly List<Vector3> _v = new(), _n = new();
        private readonly List<Color> _bary = new();
        private readonly List<byte> _biomes = new();
        public int Count => _v.Count / 3;
        private static readonly Color[] Corner = { new(1, 0, 0), new(0, 1, 0), new(0, 0, 1) };

        public void Tri(Vector3[] pos, Vector3[] norm, float[] h, Biome[] biome, int a, int b, int c)
        {
            if (h[a] < SkipBelowM && h[b] < SkipBelowM && h[c] < SkipBelowM) return;
            var outward = (pos[a] + pos[b] + pos[c]).Normalized();
            var fn = (pos[c] - pos[a]).Cross(pos[b] - pos[a]);   // front normal for Godot's clockwise winding
            if (fn.Dot(outward) < 0) { (b, c) = (c, b); fn = -fn; }
            fn = fn.Normalized();
            byte B(int k) => (byte)(h[k] < 0 ? Biome.Seabed : biome[k]);
            byte ba = B(a), bb = B(b), bc = B(c);
            int corner = 0;
            foreach (int k in new[] { a, b, c })
            {
                _v.Add(pos[k]);
                _n.Add(norm[k].Dot(fn) < CreaseCos ? fn : norm[k]);
                _bary.Add(Corner[corner++]);
                _biomes.Add(ba); _biomes.Add(bb); _biomes.Add(bc); _biomes.Add(0);
            }
        }

        public ArrayMesh Build(Material mat)
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = _v.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = _n.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = _bary.ToArray();
            arrays[(int)Mesh.ArrayType.Custom0] = _biomes.ToArray();
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays,
                flags: (Mesh.ArrayFormat)((int)Mesh.ArrayCustomFormat.Rgba8Unorm << (int)Mesh.ArrayFormat.FormatCustom0Shift));
            mesh.SurfaceSetMaterial(0, mat);
            return mesh;
        }

        public Vector3[] Faces() => _v.ToArray();
    }

    /// <summary>What each biome's ground is, for the shader: its colour (linear), and how grassy and how rocky-when-steep it is.</summary>
    private static void SetBiomes(ShaderMaterial mat, float radius)
    {
        var all = Enum.GetValues<Biome>();
        var cols = new Vector3[all.Length]; var kinds = new Vector2[all.Length];
        foreach (var bio in all)
        {
            var c = BiomeColor(bio).SrgbToLinear();
            cols[(int)bio] = new Vector3(c.R, c.G, c.B);
            kinds[(int)bio] = bio switch
            {
                Biome.Grassland or Biome.Forest => new Vector2(1, 1),
                Biome.Town => new Vector2(1, 0),
                Biome.Tundra => new Vector2(0.6f, 1),
                Biome.Desert or Biome.Rock or Biome.Snow => new Vector2(0, 1),
                _ => Vector2.Zero,   // beach and seabed: sand all the way
            };
        }
        mat.SetShaderParameter("biome_col", cols);
        mat.SetShaderParameter("biome_kind", kinds);
        mat.SetShaderParameter("planet_r", radius);
    }

    // ---- props -------------------------------------------------------------------------------
    private static void Scatter(Node3D root, PlanetGen gen)
    {
        var pine = Props.MeshOf("nature.pine") ?? new LowPoly()
            .Cylinder(Vector3.Zero, 0.2f, 0.14f, 1.3f, 5, new Color(0.36f, 0.25f, 0.16f))
            .Cylinder(new Vector3(0, 1.0f, 0), 1.5f, 0, 2.6f, 6, new Color(0.20f, 0.40f, 0.22f))
            .Cylinder(new Vector3(0, 2.4f, 0), 1.1f, 0, 2.2f, 6, new Color(0.23f, 0.45f, 0.25f))
            .Cylinder(new Vector3(0, 3.7f, 0), 0.7f, 0, 1.7f, 6, new Color(0.27f, 0.50f, 0.28f)).Build();
        var oak = Props.MeshOf("nature.oak") ?? new LowPoly()
            .Cylinder(Vector3.Zero, 0.25f, 0.18f, 1.8f, 5, new Color(0.38f, 0.27f, 0.17f))
            .Gem(new Vector3(0, 3.0f, 0), 1.7f, 1.6f, 1.3f, 7, new Color(0.36f, 0.58f, 0.24f))
            .Gem(new Vector3(0.7f, 2.5f, 0.4f), 1.0f, 0.9f, 0.7f, 6, new Color(0.32f, 0.53f, 0.22f)).Build();
        var cactus = Props.MeshOf("nature.cactus") ?? new LowPoly()
            .Cylinder(Vector3.Zero, 0.28f, 0.22f, 2.4f, 6, new Color(0.33f, 0.52f, 0.30f))
            .Box(new Vector3(0.5f, 1.3f, 0), new Vector3(0.6f, 0.2f, 0.2f), new Color(0.31f, 0.50f, 0.29f))
            .Box(new Vector3(0.72f, 1.65f, 0), new Vector3(0.2f, 0.7f, 0.2f), new Color(0.33f, 0.52f, 0.30f)).Build();
        var rock = Props.MeshOf("nature.rock") ?? new LowPoly()
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
                if (TownDressing.InField(gen, dir)) continue;  // no trees in the middle of a ploughed field
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
            var far = new MultiMeshInstance3D { Multimesh = mm, Position = centre, VisibilityRangeEndMargin = 150, VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Disabled };
            GameSettings.ApplyFarProps(far);   // 1800 m at Medium; nearer at Low, further at High
            root.AddChild(far);
        }
    }
}
