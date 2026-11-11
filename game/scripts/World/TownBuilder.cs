using Godot;
using Telo.Sim.Content;
using Telo.Sim.Planet;

namespace Telo.Game.World;

/// <summary>A building someone lives or works in, as it exists in the world.</summary>
public sealed class LotView
{
    public required LotDef Lot { get; init; }
    public required Interactable Door { get; init; }
    public required Label3D Marker { get; init; }
    public Interactable? Counter { get; init; }
    public Vector3 Position { get; init; }
}

/// <summary>What the rest of the game needs to know about the settlements.</summary>
public sealed class Town
{
    public required Node3D Root { get; init; }
    public required Node3D Hq { get; init; }
    public required Transform3D PlayerSpawn { get; init; }
    public required Transform3D VanSpawn { get; init; }
    public required Vector3 Centre { get; init; }
    public List<LotView> Lots { get; } = new();
}

/// <summary>
/// Lays out settlements on the planet's flattened sites from content (lots.json). Every building is placed
/// individually on the sphere (offsets are metres east / south of the site centre), so streets follow the curve of the world.
/// All models here are placeholders built from primitives.
/// </summary>
public static class TownBuilder
{
    /// <summary>Transform for something standing upright at an offset from a site centre. +Z of the result points south.</summary>
    public static Transform3D Place(PlanetGen gen, Site site, float east, float south, float yawDeg = 0, float lift = 0)
    {
        var c = PlanetBuilder.ToGodot(site.Dir);
        var e = Vector3.Up.Cross(c).Normalized();
        var n = c.Cross(e);
        float r = (float)(gen.RadiusM + site.HeightM);
        var dir = (c * r + e * east - n * south).Normalized();
        var bx = (e - dir * e.Dot(dir)).Normalized();
        var basis = new Basis(bx, dir, bx.Cross(dir)).Rotated(dir, Mathf.DegToRad(yawDeg));
        return new Transform3D(basis, dir * (r + lift));
    }

    public static Transform3D Place(PlanetGen gen, float east, float south, float yawDeg = 0, float lift = 0) =>
        Place(gen, gen.Town, east, south, yawDeg, lift);

    public static Town Build(Node3D parent, PlanetGen gen, ContentDb content)
    {
        var root = new Node3D { Name = "Town" };
        parent.AddChild(root);

        // --- your rented room -----------------------------------------------------------------
        var hq = new Node3D { Name = "HQ", Transform = Place(gen, 0, 0, 0, 0.1f) };
        root.AddChild(hq);
        RoomBuilder.BuildShell(hq);

        var town = new Town
        {
            Root = root, Hq = hq,
            PlayerSpawn = hq.Transform * new Transform3D(Basis.Identity, new Vector3(0.5f, 0.1f, 1.2f)),
            VanSpawn = Place(gen, 8, 10.5f, -90, 0.3f),
            Centre = hq.Transform.Origin,
        };

        // --- buildings from content ---------------------------------------------------------------
        var palette = new[] { new Color(0.80f, 0.72f, 0.58f), new Color(0.70f, 0.76f, 0.78f), new Color(0.82f, 0.62f, 0.52f), new Color(0.74f, 0.78f, 0.64f), new Color(0.86f, 0.82f, 0.72f) };
        var roofs = new[] { new Color(0.50f, 0.22f, 0.18f), new Color(0.28f, 0.30f, 0.34f), new Color(0.38f, 0.28f, 0.20f) };
        int i = 0;
        foreach (var lot in content.Lots.Values.OrderBy(l => l.Id))
        {
            if (gen.SiteById(lot.Site) is not { } site) continue;
            var xf = Place(gen, site, (float)lot.East, (float)lot.South, (float)lot.Yaw);
            Mesh mesh = lot.Kind switch
            {
                LotKind.Shop => ShopMesh(),
                LotKind.Farm => FarmMesh(),
                _ => HouseMesh(palette[i % palette.Length], roofs[i % roofs.Length], 3f + (i % 2) * 0.5f),
            };
            i++;
            town.Lots.Add(Building(root, lot, xf, mesh));
        }

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

        return town;
    }

    private static LotView Building(Node3D root, LotDef lot, Transform3D xf, Mesh mesh)
    {
        bool shop = lot.Kind == LotKind.Shop;
        float w = shop ? 10 : 8, d = shop ? 8 : 7, front = d / 2;
        var body = new StaticBody3D { Name = lot.Id, Transform = xf };
        body.AddChild(new MeshInstance3D { Mesh = mesh });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(w, 4, d) }, Position = new Vector3(0, 1.5f, 0) });
        root.AddChild(body);

        Interactable Hotspot(Vector3 pos, Vector3 size)
        {
            var it = new Interactable { Position = pos };
            it.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
            body.AddChild(it);
            return it;
        }
        var door = Hotspot(new Vector3(0, 1.05f, front + 0.15f), new Vector3(1.3f, 2.1f, 0.4f));
        var counter = shop ? Hotspot(new Vector3(3f, 1.4f, front + 0.15f), new Vector3(2.6f, 1.6f, 0.4f)) : null;

        var name = new Label3D
        {
            Text = lot.Resident, Position = new Vector3(0, shop ? 3.25f : 2.45f, front + 0.12f), FontSize = 64, PixelSize = (shop ? 0.5f : 0.16f) / 64f,
            Modulate = shop ? new Color(0.95f, 0.9f, 0.7f) : new Color(0.12f, 0.1f, 0.08f), OutlineSize = 0,
        };
        body.AddChild(name);

        // A marker that floats above the roof when there is something to do here. Placeholder for a proper map/pager.
        var marker = new Label3D
        {
            Text = "!", Position = new Vector3(0, 9f, 0), FontSize = 96, PixelSize = 0.0016f, FixedSize = true, NoDepthTest = true,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Modulate = new Color(1f, 0.85f, 0.2f), OutlineSize = 16, OutlineModulate = new Color(0.1f, 0.08f, 0.02f),
            Visible = false,
        };
        body.AddChild(marker);
        return new LotView { Lot = lot, Door = door, Marker = marker, Counter = counter, Position = xf.Origin };
    }

    /// <summary>The road surface, laid onto the real terrain by ray-casting down. Call once physics has the terrain.</summary>
    public static void BuildRoads(Node3D root, PlanetGen gen, PhysicsDirectSpaceState3D space)
    {
        float R = (float)gen.RadiusM;
        var asphalt = new Color(0.27f, 0.27f, 0.29f); var paint = new Color(0.85f, 0.8f, 0.6f);
        foreach (var road in gen.Roads)
        {
            var lp = new LowPoly();
            var centre = new List<Vector3>();
            for (int i = 0; i < road.Points.Count - 1; i++)
                for (int k = 0; k < 4; k++)
                    centre.Add((PlanetBuilder.ToGodot(road.Points[i]).Lerp(PlanetBuilder.ToGodot(road.Points[i + 1]), k / 4f)).Normalized());
            centre.Add(PlanetBuilder.ToGodot(road.Points[^1]));

            Vector3? Ground(Vector3 dir)
            {
                var q = PhysicsRayQueryParameters3D.Create(dir * (R + 400), dir * (R - 100), 1);
                var hit = space.IntersectRay(q);
                return hit.Count > 0 ? (Vector3)hit["position"] + dir * 0.09f : null;
            }

            Vector3? pl = null, pr = null, pc = null;
            for (int i = 0; i < centre.Count; i++)
            {
                var c = centre[i];
                var along = (centre[Math.Min(i + 1, centre.Count - 1)] - centre[Math.Max(i - 1, 0)]).Normalized();
                var side = along.Cross(c).Normalized();
                Vector3? l = Ground((c * R - side * 3.2f).Normalized()), r = Ground((c * R + side * 3.2f).Normalized()), m = Ground(c);
                bool skip = gen.Sites.Any(s => (PlanetBuilder.ToGodot(s.Dir) - c).Length() * R < s.RadiusM * 0.5f);
                if (!skip && l is { } L && r is { } Rr && m is { } M && pl is { } PL && pr is { } PR && pc is { } PC)
                {
                    lp.Quad(PL, PR, Rr, L, asphalt, c);
                    if (i % 2 == 0) lp.Quad(PC - side * 0.09f + c * 0.03f, PC + side * 0.09f + c * 0.03f, M + side * 0.09f + c * 0.03f, M - side * 0.09f + c * 0.03f, paint, c);
                }
                (pl, pr, pc) = (l, r, m);
            }
            if (lp.TriangleCount > 0)
            {
                var mi = lp.Instance(road.Id);
                mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                root.AddChild(mi);
            }
        }
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

    private static Mesh ShopMesh()
    {
        var wall = new Color(0.72f, 0.66f, 0.56f);
        return new LowPoly()
            .Box(new Vector3(0, 1.7f, 0), new Vector3(10, 4.0f, 8), wall)
            .Box(new Vector3(0, 3.85f, 0), new Vector3(10.4f, 0.3f, 8.4f), new Color(0.3f, 0.3f, 0.33f))
            .Box(new Vector3(0, 3.25f, 4.03f), new Vector3(9.0f, 0.8f, 0.08f), new Color(0.16f, 0.2f, 0.36f))        // sign board
            .Box(new Vector3(0, 1.0f, 4.03f), new Vector3(1.1f, 2.0f, 0.08f), new Color(0.3f, 0.2f, 0.14f))            // door
            .Box(new Vector3(3f, 1.5f, 4.03f), new Vector3(2.6f, 1.5f, 0.08f), new Color(0.32f, 0.46f, 0.56f))         // counter window
            .Box(new Vector3(3f, 0.7f, 4.25f), new Vector3(2.8f, 0.1f, 0.5f), new Color(0.45f, 0.32f, 0.2f))           // counter shelf
            .Box(new Vector3(-3f, 1.5f, 4.03f), new Vector3(2.6f, 1.5f, 0.08f), new Color(0.32f, 0.46f, 0.56f))
            .Box(new Vector3(-3f, 1.1f, 3.8f), new Vector3(0.6f, 0.5f, 0.4f), new Color(0.15f, 0.15f, 0.16f))          // a television in the window
            .Build();
    }

    private static Mesh FarmMesh()
    {
        var red = new Color(0.58f, 0.2f, 0.16f);
        return new LowPoly()
            // farmhouse
            .Box(new Vector3(0, 1.4f, 0), new Vector3(8, 3.4f, 7), new Color(0.88f, 0.86f, 0.78f))
            .GableRoof(new Vector3(0, 3.1f, 0), 8.8f, 7.8f, 2.4f, new Color(0.3f, 0.3f, 0.32f))
            .Box(new Vector3(0, 1.0f, 3.52f), new Vector3(1.0f, 2.0f, 0.08f), new Color(0.24f, 0.34f, 0.22f))
            .Box(new Vector3(-2.5f, 1.7f, 3.53f), new Vector3(1.3f, 1.0f, 0.06f), new Color(0.30f, 0.42f, 0.52f))
            .Box(new Vector3(2.5f, 1.7f, 3.53f), new Vector3(1.3f, 1.0f, 0.06f), new Color(0.30f, 0.42f, 0.52f))
            // barn and silo
            .Box(new Vector3(20, 2.6f, -6), new Vector3(11, 5.2f, 15), red)
            .GableRoof(new Vector3(20, 5.2f, -6), 11.8f, 15.8f, 3.2f, new Color(0.25f, 0.24f, 0.24f))
            .Box(new Vector3(20, 1.8f, 1.53f), new Vector3(4.2f, 3.6f, 0.08f), new Color(0.9f, 0.88f, 0.82f))
            .Cylinder(new Vector3(29, 0, -10), 2.2f, 2.2f, 9f, 8, new Color(0.72f, 0.74f, 0.76f))
            .Cylinder(new Vector3(29, 9f, -10), 2.3f, 0, 1.8f, 8, new Color(0.5f, 0.52f, 0.55f))
            // a few fence posts toward the road
            .Box(new Vector3(-8, 0.6f, 12), new Vector3(0.15f, 1.2f, 0.15f), new Color(0.4f, 0.3f, 0.2f))
            .Box(new Vector3(-4, 0.6f, 12), new Vector3(0.15f, 1.2f, 0.15f), new Color(0.4f, 0.3f, 0.2f))
            .Box(new Vector3(4, 0.6f, 12), new Vector3(0.15f, 1.2f, 0.15f), new Color(0.4f, 0.3f, 0.2f))
            .Box(new Vector3(8, 0.6f, 12), new Vector3(0.15f, 1.2f, 0.15f), new Color(0.4f, 0.3f, 0.2f))
            .Box(new Vector3(0, 0.9f, 12), new Vector3(16.2f, 0.1f, 0.08f), new Color(0.4f, 0.3f, 0.2f))
            .Build();
    }
}
