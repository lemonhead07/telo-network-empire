using Godot;
using Telo.Sim.Planet;

namespace Telo.Game.World;

/// <summary>What the rest of the game needs to know about the starting town.</summary>
public sealed class Town
{
    public required Node3D Hq { get; init; }
    public required Transform3D PlayerSpawn { get; init; }
    public required Transform3D VanSpawn { get; init; }
    public required Vector3 Centre { get; init; }
}

/// <summary>
/// Lays out the starting town on the planet's flattened town site. Every building is placed
/// individually on the sphere (offsets are metres east / south of the town centre), so streets follow the curve of the world.
/// </summary>
public static class TownBuilder
{
    /// <summary>Transform for something standing upright at an offset from the town centre. +Z of the result points south.</summary>
    public static Transform3D Place(PlanetGen gen, float east, float south, float yawDeg = 0, float lift = 0)
    {
        var c = PlanetBuilder.ToGodot(gen.Town.Dir);
        var e = Vector3.Up.Cross(c).Normalized();
        var n = c.Cross(e);
        float r = (float)(gen.RadiusM + gen.Town.HeightM);
        var dir = (c * r + e * east - n * south).Normalized();
        var bx = (e - dir * e.Dot(dir)).Normalized();
        var basis = new Basis(bx, dir, bx.Cross(dir)).Rotated(dir, Mathf.DegToRad(yawDeg));
        return new Transform3D(basis, dir * (r + lift));
    }

    public static Town Build(Node3D parent, PlanetGen gen)
    {
        var root = new Node3D { Name = "Town" };
        parent.AddChild(root);

        // --- your rented room -----------------------------------------------------------------
        var hq = new Node3D { Name = "HQ", Transform = Place(gen, 0, 0, 0, 0.1f) };
        root.AddChild(hq);
        RoomBuilder.BuildShell(hq);

        // --- houses (future customers) ---------------------------------------------------------
        var palette = new[] { new Color(0.80f, 0.72f, 0.58f), new Color(0.70f, 0.76f, 0.78f), new Color(0.82f, 0.62f, 0.52f), new Color(0.74f, 0.78f, 0.64f), new Color(0.86f, 0.82f, 0.72f) };
        var roofs = new[] { new Color(0.50f, 0.22f, 0.18f), new Color(0.28f, 0.30f, 0.34f), new Color(0.38f, 0.28f, 0.20f) };
        var meshes = new List<Mesh>();
        for (int i = 0; i < 6; i++) meshes.Add(HouseMesh(palette[i % palette.Length], roofs[i % roofs.Length], 3f + (i % 2) * 0.5f));

        int h = 0;
        foreach (float x in new[] { -52f, -36f, -20f, 20f, 36f, 52f }) House(root, gen, meshes[h++ % 6], x, 0, 0);
        foreach (float x in new[] { -46f, -30f, -14f, 4f, 22f, 40f }) House(root, gen, meshes[h++ % 6], x, 33, 180);

        // --- Main Street ---------------------------------------------------------------------
        var road = new LowPoly().Box(Vector3.Zero, new Vector3(6.3f, 0.08f, 6.5f), new Color(0.27f, 0.27f, 0.29f))
            .Box(new Vector3(0, 0.045f, 0), new Vector3(2.2f, 0.01f, 0.16f), new Color(0.85f, 0.8f, 0.6f)).Build();
        for (float x = -96; x <= 96; x += 6)
            root.AddChild(new MeshInstance3D { Mesh = road, Transform = Place(gen, x, 16.5f, 0, 0.05f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });

        // --- telephone poles: the incumbent's network, already here before you -------------------
        var brown = new Color(0.33f, 0.24f, 0.16f);
        var pole = new LowPoly().Cylinder(Vector3.Zero, 0.13f, 0.09f, 7.2f, 6, brown)
            .Box(new Vector3(0, 6.6f, 0), new Vector3(0.12f, 0.12f, 1.8f), brown)
            .Box(new Vector3(0, 6.75f, -0.7f), new Vector3(0.08f, 0.18f, 0.08f), new Color(0.75f, 0.8f, 0.78f))
            .Box(new Vector3(0, 6.75f, 0.7f), new Vector3(0.08f, 0.18f, 0.08f), new Color(0.75f, 0.8f, 0.78f)).Build();
        for (float x = -90; x <= 90; x += 30)
            root.AddChild(new MeshInstance3D { Mesh = pole, Transform = Place(gen, x, 11.5f) });

        var spawnLocal = new Transform3D(Basis.Identity, new Vector3(0.5f, 0.1f, 1.2f));
        return new Town
        {
            Hq = hq,
            PlayerSpawn = hq.Transform * spawnLocal,
            VanSpawn = Place(gen, 8, 10.5f, -90, 0.3f),
            Centre = hq.Transform.Origin,
        };
    }

    private static void House(Node3D root, PlanetGen gen, Mesh mesh, float east, float south, float yaw)
    {
        var body = new StaticBody3D { Transform = Place(gen, east, south, yaw) };
        body.AddChild(new MeshInstance3D { Mesh = mesh });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(8, 4, 7) }, Position = new Vector3(0, 1.5f, 0) });
        root.AddChild(body);
    }

    private static Mesh HouseMesh(Color wall, Color roof, float wallH)
    {
        var glass = new Color(0.30f, 0.42f, 0.52f); var trim = new Color(0.93f, 0.9f, 0.84f);
        var lp = new LowPoly()
            .Box(new Vector3(0, wallH / 2 - 0.3f, 0), new Vector3(8, wallH + 0.6f, 7), wall)
            .GableRoof(new Vector3(0, wallH, 0), 8.8f, 7.8f, 2.3f, roof)
            .Box(new Vector3(0, 1.0f, 3.52f), new Vector3(1.0f, 2.0f, 0.08f), new Color(0.34f, 0.22f, 0.15f))
            .Box(new Vector3(0, 0.05f, 4.0f), new Vector3(1.6f, 0.12f, 0.9f), new Color(0.6f, 0.58f, 0.55f))
            .Box(new Vector3(2.6f, wallH + 1.6f, -1.2f), new Vector3(0.7f, 1.8f, 0.7f), new Color(0.55f, 0.36f, 0.3f));
        foreach (float x in new[] { -2.5f, 2.5f })
        {
            lp.Box(new Vector3(x, 1.7f, 3.52f), new Vector3(1.5f, 1.2f, 0.06f), trim);
            lp.Box(new Vector3(x, 1.7f, 3.55f), new Vector3(1.3f, 1.0f, 0.06f), glass);
            lp.Box(new Vector3(x, 1.7f, -3.55f), new Vector3(1.3f, 1.0f, 0.06f), glass);
        }
        return lp.Build();
    }
}
