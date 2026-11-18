using Godot;
using Telo.Sim.Content;
using Telo.Sim.Planet;

namespace Telo.Game.World;

/// <summary>
/// Set dressing round where people live: gardens (lawn, fences, hedges, flower beds, a tree, a shed, a washing line),
/// street lamps and a phone box, and a patchwork of fields with hedgerows outside each settlement.
/// Looks only: nothing here has collision or decides anything. Seeded from lot ids and the planet seed, so the same
/// world always looks the same. Placeholder shapes (D15) until models.
/// </summary>
public static class TownDressing
{
    // ---- colours -------------------------------------------------------------------------------
    private static readonly Color Hedge = new(0.24f, 0.42f, 0.20f), Wood = new(0.48f, 0.38f, 0.27f), Post = new(0.38f, 0.29f, 0.20f);
    private static readonly Color Picket = new(0.92f, 0.90f, 0.84f), Brick = new(0.62f, 0.36f, 0.28f), Slab = new(0.70f, 0.68f, 0.62f);
    private static readonly Color Soil = new(0.36f, 0.26f, 0.17f), Leaf = new(0.36f, 0.58f, 0.24f), Trunk = new(0.38f, 0.27f, 0.17f);
    private static readonly Color[] Flowers = { new(0.85f, 0.20f, 0.20f), new(0.95f, 0.82f, 0.25f), new(0.95f, 0.95f, 0.92f), new(0.62f, 0.38f, 0.75f), new(0.95f, 0.55f, 0.65f) };
    private static readonly Color[] Washing = { new(0.85f, 0.3f, 0.3f), new(0.3f, 0.5f, 0.85f), new(0.95f, 0.95f, 0.9f), new(0.95f, 0.8f, 0.3f) };

    /// <summary>Back gardens run at most this far behind a house's centre (less where they meet the gardens of the next street);
    /// front gardens stop this far short of the street's centre line; a plot is at most this wide either side of the house.</summary>
    private const float MaxBack = 14f, FrontFromStreet = 6.5f, MaxHalfPlot = 11f;

    /// <summary>A stable hash (string.GetHashCode changes between runs).</summary>
    private static uint Hash(string s, uint salt = 0)
    {
        uint h = 2166136261u ^ salt;
        foreach (char ch in s) { h ^= ch; h *= 16777619u; }
        h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12;
        return h;
    }

    private static float Frac(uint h, int shift) => ((h >> shift) & 0xFF) / 255f;

    // =============================================================================================
    //  Gardens and street furniture (built with the town)
    // =============================================================================================

    public static void Gardens(Node3D root, PlanetGen gen, Town town)
    {
        var lots = town.Lots.Where(l => l.Lot.Kind != LotKind.Farm).ToList();
        foreach (var view in lots)
        {
            if (view.Lot.Kind == LotKind.Shop)
            {
                var box = Props.Make("street.phone-box", () => PhoneBox(Vector3.Zero));
                box.Position = new Vector3(6.4f, 0, 5.6f);
                view.Body.AddChild(box);
                continue;
            }
            if (view.Lot.Kind != LotKind.House || TownBuilder.StreetOf(view.Lot) is not { } street) continue;
            var garden = Garden(view.Lot, street, lots, view.Body);
            if (garden.TriangleCount > 0) view.Body.AddChild(garden.Instance("Garden"));
        }
        StreetLamps(root, gen, lots);
    }

    /// <summary>World east of a point at local x in a lot (yaw 0 faces south, so +x is east; yaw 180 turns it round).</summary>
    private static int EastSign(LotDef lot) => Mathf.Abs(Mathf.Wrap((float)lot.Yaw, -180f, 180f)) < 90 ? 1 : -1;

    private static LowPoly Garden(LotDef lot, TownBuilder.Street street, List<LotView> all, Node3D body)
    {
        var lp = new LowPoly();
        uint h = Hash(lot.Id);
        int es = EastSign(lot);
        bool facingSouth = es == 1;
        float zStreet = facingSouth ? street.At - (float)lot.South : (float)lot.South - street.At;
        float zf = zStreet - FrontFromStreet;
        float driveX = TownBuilder.DriveSide(lot, facingSouth, street) * TownBuilder.DriveOffset;

        // how far the plot runs either side: halfway to the next building in the row, at most 11 m
        LotView? Neighbour(int side) => all.Where(o => o.Lot != lot && o.Lot.Site == lot.Site && Mathf.Abs((float)(o.Lot.South - lot.South)) < 3
                && Mathf.Sign((float)(o.Lot.East - lot.East)) == side * es && Mathf.Abs((float)(o.Lot.East - lot.East)) < 2 * MaxHalfPlot + 4)
            .OrderBy(o => Mathf.Abs((float)(o.Lot.East - lot.East))).FirstOrDefault();
        var left = Neighbour(-1); var right = Neighbour(1);
        float hwL = left is null ? MaxHalfPlot : Mathf.Min(MaxHalfPlot, Mathf.Abs((float)(left.Lot.East - lot.East)) / 2);
        float hwR = right is null ? MaxHalfPlot : Mathf.Min(MaxHalfPlot, Mathf.Abs((float)(right.Lot.East - lot.East)) / 2);

        // back to back with the next street's gardens: meet halfway, and only one of the two builds the shared fence
        var behind = all.Where(o => o.Lot != lot && o.Lot.Site == lot.Site && Mathf.Abs((float)(o.Lot.East - lot.East)) < 2 * MaxHalfPlot
                && Mathf.Sign((float)(lot.South - o.Lot.South)) == es && Mathf.Abs((float)(o.Lot.South - lot.South)) < 2 * MaxBack + 2)
            .OrderBy(o => Mathf.Abs((float)(o.Lot.South - lot.South))).FirstOrDefault();
        float BackZ = behind is null ? -MaxBack : -Mathf.Abs((float)(behind.Lot.South - lot.South)) / 2;
        bool backFence = behind is null || behind.Lot.Kind != LotKind.House || facingSouth;

        // --- lawn: mown stripes front to back; scruffier for students and gamers -------------------
        bool tidy = lot.Archetype is not ("student" or "gamer");
        var g1 = tidy ? new Color(0.47f, 0.62f, 0.31f) : new Color(0.53f, 0.59f, 0.34f);
        var g2 = tidy ? new Color(0.43f, 0.57f, 0.28f) : new Color(0.51f, 0.57f, 0.33f);
        const float Stripe = 1.6f;
        int k = 0;
        for (float x = -hwL; x < hwR - 0.01f; x += Stripe, k++)
        {
            float x1 = Mathf.Min(x + Stripe, hwR);
            var c = k % 2 == 0 ? g1 : g2;
            lp.Quad(new Vector3(x, 0.015f, BackZ), new Vector3(x1, 0.015f, BackZ), new Vector3(x1, 0.015f, zf), new Vector3(x, 0.015f, zf), c, Vector3.Up);
        }

        // --- fences: close-board down the sides and across the back ---------------------------------
        void BoardFence(Vector3 a, Vector3 b)
        {
            var mid = (a + b) / 2; var d = b - a; float len = d.Length();
            bool alongX = Mathf.Abs(d.X) > Mathf.Abs(d.Z);
            lp.Box(mid + new Vector3(0, 0.6f, 0), alongX ? new Vector3(len, 1.15f, 0.05f) : new Vector3(0.05f, 1.15f, len), Wood);
            int posts = Mathf.Max(1, Mathf.RoundToInt(len / 2.4f));
            for (int i = 0; i <= posts; i++) lp.Box(a + d * (i / (float)posts) + new Vector3(0, 0.65f, 0), new Vector3(0.11f, 1.3f, 0.11f), Post);
        }
        if (backFence) BoardFence(new Vector3(-hwL, 0, BackZ), new Vector3(hwR, 0, BackZ));
        BoardFence(new Vector3(-hwL, 0, BackZ), new Vector3(-hwL, 0, 3.5f));
        if (right is null || right.Lot.Kind != LotKind.House) BoardFence(new Vector3(hwR, 0, BackZ), new Vector3(hwR, 0, 3.5f));

        // --- the front boundary: hedge, picket fence, low wall, or open lawn; gaps for the path and drive
        int front = (int)(h % 4);
        var gaps = new List<(float A, float B)> { (-0.9f, 0.9f), (driveX - 1.9f, driveX + 1.9f) };
        var runs = new List<(float A, float B)>();
        float cur = -hwL;
        foreach (var (a, b) in gaps.OrderBy(g => g.A)) { if (a > cur + 0.3f) runs.Add((cur, Mathf.Min(a, hwR))); cur = Mathf.Max(cur, b); }
        if (hwR > cur + 0.3f) runs.Add((cur, hwR));
        foreach (var (a, b) in runs)
        {
            float mid = (a + b) / 2, len = b - a;
            switch (front)
            {
                case 0: lp.Box(new Vector3(mid, 0.45f, zf), new Vector3(len, 0.9f, 0.7f), Hedge * (0.95f + Frac(h, 8) * 0.12f)); break;
                case 1:
                    lp.Box(new Vector3(mid, 0.45f, zf), new Vector3(len, 0.06f, 0.04f), Picket);
                    for (float x = a + 0.12f; x < b; x += 0.32f) lp.Box(new Vector3(x, 0.42f, zf), new Vector3(0.09f, 0.84f, 0.03f), Picket);
                    break;
                case 2:
                    lp.Box(new Vector3(mid, 0.3f, zf), new Vector3(len, 0.6f, 0.3f), Brick).Box(new Vector3(mid, 0.63f, zf), new Vector3(len + 0.04f, 0.06f, 0.36f), Slab);
                    break;
                default: break; // open-plan: the lawn runs to the pavement
            }
        }

        // a path of stepping stones to the front door, and the letterbox at the end of it
        for (float z = 5.4f; z < zf - 0.2f; z += 0.95f) lp.Box(new Vector3(0, 0.03f, z), new Vector3(0.95f, 0.04f, 0.6f), Slab);
        var mail = (h >> 4) % 3 == 0 ? new Color(0.25f, 0.4f, 0.3f) : new Color(0.7f, 0.2f, 0.16f);
        float mz = zf + 0.7f;
        lp.Box(new Vector3(1.35f, 0.5f, mz), new Vector3(0.08f, 1.0f, 0.08f), Post).Box(new Vector3(1.35f, 1.1f, mz), new Vector3(0.3f, 0.26f, 0.46f), mail);

        // flower beds under the front windows (kept low: the junction box goes on the wall above the left one)
        foreach (float x in new[] { -2.5f, 2.5f })
        {
            lp.Box(new Vector3(x, 0.06f, 3.95f), new Vector3(1.7f, 0.12f, 0.65f), Soil);
            for (int i = 0; i < 5; i++)
            {
                uint fh = Hash(lot.Id, (uint)(i * 31 + (x > 0 ? 7 : 0)));
                var col = Flowers[(fh + (h >> 3)) % (uint)Flowers.Length];
                float fx = x - 0.65f + i * 0.33f, fz = 3.8f + Frac(fh, 8) * 0.3f, fy = 0.2f + Frac(fh, 16) * 0.12f;
                lp.Box(new Vector3(fx, fy / 2 + 0.05f, fz), new Vector3(0.04f, fy, 0.04f), Leaf * 0.8f).Box(new Vector3(fx, fy + 0.08f, fz), new Vector3(0.16f, 0.12f, 0.16f), col);
            }
        }
        // a shrub by each back corner of the house
        lp.Gem(new Vector3(-4.8f, 0.55f, -2.6f), 0.7f, 0.55f, 0.5f, 6, Hedge * 1.1f).Gem(new Vector3(4.8f, 0.5f, -2.8f), 0.6f, 0.5f, 0.45f, 6, Hedge);

        // --- the back garden: a tree, then some of a shed, a vegetable patch, a washing line ---------
        int side = (h >> 9) % 2 == 0 ? 1 : -1;
        float treeX = side * Mathf.Min(6.5f + Frac(h, 12) * 2f, (side > 0 ? hwR : hwL) - 2f);
        var treeAt = new Vector3(treeX, 0, Mathf.Min(-6.5f, BackZ + 4.5f + Frac(h, 20) * 2f));
        float treeScale = 0.85f + Frac(h, 6) * 0.45f;
        string treeId = (h >> 25) % 3 == 0 ? "nature.oak" : "nature.fruit-tree";   // most gardens have an apple tree
        if (Props.HasModel(treeId))
        {
            var tree = Props.Make(treeId);
            tree.Position = treeAt;
            tree.Scale = Vector3.One * (treeId == "nature.oak" ? treeScale : treeScale * 1.2f);
            tree.RotationDegrees = new Vector3(0, Frac(h, 14) * 360f, 0);
            body.AddChild(tree);
        }
        else Tree(lp, treeAt, treeScale);
        if ((h >> 13) % 5 < 3)
        {
            float sx = -side * ((side > 0 ? hwL : hwR) - 1.7f);
            var shedCol = new Color(0.45f + Frac(h, 2) * 0.15f, 0.42f, 0.32f);
            lp.Box(new Vector3(sx, 0.95f, BackZ + 1.7f), new Vector3(2.0f, 1.9f, 1.8f), shedCol)
              .GableRoof(new Vector3(sx, 1.9f, BackZ + 1.7f), 2.3f, 2.1f, 0.6f, new Color(0.3f, 0.3f, 0.32f))
              .Box(new Vector3(sx, 0.85f, BackZ + 2.62f), new Vector3(0.7f, 1.6f, 0.04f), shedCol * 0.7f);
        }
        if ((h >> 17) % 2 == 0)
        {
            var c = new Vector3(-side * 3.2f, 0, -8.5f);
            lp.Box(c + new Vector3(0, 0.06f, 0), new Vector3(3.6f, 0.12f, 2.6f), Soil);
            for (int r = 0; r < 4; r++)
                for (int i = 0; i < 5; i++)
                    lp.Gem(c + new Vector3(-1.4f + i * 0.7f, 0.2f, -0.95f + r * 0.63f), 0.17f, 0.12f, 0.05f, 4, Leaf * (r % 2 == 0 ? 1f : 0.85f));
        }
        if ((h >> 21) % 3 != 0)
        {
            // a rotary washing line with the week's washing on it
            var c = new Vector3(side * 1.2f, 0, -7f);
            lp.Box(c + new Vector3(0, 1.0f, 0), new Vector3(0.06f, 2.0f, 0.06f), new Color(0.75f, 0.75f, 0.72f))
              .Box(c + new Vector3(0, 1.95f, 0), new Vector3(2.6f, 0.03f, 0.03f), new Color(0.75f, 0.75f, 0.72f))
              .Box(c + new Vector3(0, 1.95f, 0), new Vector3(0.03f, 0.03f, 2.6f), new Color(0.75f, 0.75f, 0.72f));
            for (int i = 0; i < 4; i++)
            {
                var off = i switch { 0 => new Vector3(0.9f, 0, 0), 1 => new Vector3(-0.8f, 0, 0), 2 => new Vector3(0, 0, 0.9f), _ => new Vector3(0, 0, -0.7f) };
                bool alongX = i >= 2;
                lp.Box(c + off + new Vector3(0, 1.62f, 0), alongX ? new Vector3(0.55f, 0.6f, 0.02f) : new Vector3(0.02f, 0.6f, 0.55f), Washing[(i + (int)(h % 4)) % Washing.Length]);
            }
        }
        return lp;
    }

    private static void Tree(LowPoly lp, Vector3 at, float s)
    {
        lp.Cylinder(at, 0.2f * s, 0.15f * s, 1.9f * s, 5, Trunk)
          .Gem(at + new Vector3(0, 3.0f * s, 0), 1.8f * s, 1.5f * s, 1.3f * s, 7, Leaf)
          .Gem(at + new Vector3(0.8f * s, 2.5f * s, 0.4f * s), 1.1f * s, 0.9f * s, 0.7f * s, 6, Leaf * 0.9f);
    }

    /// <summary>The incumbent's phone box outside the shop: every call from it is one you are not getting.</summary>
    private static MeshInstance3D PhoneBox(Vector3 at)
    {
        var red = new Color(0.72f, 0.14f, 0.12f); var glass = new Color(0.55f, 0.68f, 0.75f);
        var lp = new LowPoly()
            .Box(at + new Vector3(0, 1.25f, 0), new Vector3(1.0f, 2.5f, 1.0f), red)
            .Box(at + new Vector3(0, 2.6f, 0), new Vector3(1.1f, 0.2f, 1.1f), red * 0.9f)
            .Box(at + new Vector3(0, 2.35f, 0.51f), new Vector3(0.8f, 0.2f, 0.02f), new Color(0.95f, 0.93f, 0.85f));
        foreach (var (n, size) in new[] { (Vector3.Back, new Vector3(0.7f, 1.5f, 0.02f)), (Vector3.Forward, new Vector3(0.7f, 1.5f, 0.02f)), (Vector3.Left, new Vector3(0.02f, 1.5f, 0.7f)), (Vector3.Right, new Vector3(0.02f, 1.5f, 0.7f)) })
            lp.Box(at + new Vector3(0, 1.35f, 0) + n * 0.51f, size, glass);
        var mi = lp.Instance("PhoneBox");
        mi.AddChild(new Label3D { Text = "TELEPHONE", Position = at + new Vector3(0, 2.35f, 0.53f), FontSize = 32, PixelSize = 0.004f, Modulate = new Color(0.1f, 0.1f, 0.1f), OutlineSize = 0 });
        return mi;
    }

    /// <summary>Street lamps on the side of each street away from the pole line, between the driveways.</summary>
    private static void StreetLamps(Node3D root, PlanetGen gen, List<LotView> lots)
    {
        var grey = new Color(0.42f, 0.43f, 0.45f);
        var lamp = Props.MeshOf("street.lamp") ?? new LowPoly().Cylinder(Vector3.Zero, 0.09f, 0.06f, 5.2f, 6, grey)
            .Box(new Vector3(0, 5.15f, -0.7f), new Vector3(0.08f, 0.08f, 1.5f), grey)
            .Box(new Vector3(0, 5.05f, -1.35f), new Vector3(0.3f, 0.14f, 0.55f), grey * 0.8f)
            .Box(new Vector3(0, 4.96f, -1.35f), new Vector3(0.22f, 0.04f, 0.42f), new Color(1f, 0.9f, 0.62f)).Build();
        foreach (var st in TownBuilder.Streets.Where(s => !s.NorthSouth))
        {
            if (gen.SiteById(st.Site) is not { } site) continue;
            float lampSouth = st.At + 4.7f;
            // where something crosses the south verge: paths and driveways of the buildings on that side
            var blocked = new List<float>();
            foreach (var v in lots.Where(l => l.Lot.Site == st.Site && TownBuilder.StreetOf(l.Lot) == st && l.Lot.South > st.At))
            {
                int es = EastSign(v.Lot);
                blocked.Add((float)v.Lot.East);
                float dx = v.Lot.Kind switch { LotKind.Shop => -3f, LotKind.Depot => -1.9f, _ => TownBuilder.DriveSide(v.Lot, es == 1, st) * TownBuilder.DriveOffset };
                blocked.Add((float)v.Lot.East + es * dx);
            }
            for (float x = Mathf.Ceil(st.From / 30f) * 30f - 15f; x <= st.To; x += 30)
            {
                if (x < st.From + 2) continue;
                float px = x;
                for (int tries = 0; tries < 4 && blocked.Any(b => Mathf.Abs(b - px) < 2.8f); tries++) px += 3f;
                if (blocked.Any(b => Mathf.Abs(b - px) < 2.8f)) continue;
                root.AddChild(new MeshInstance3D { Name = "Lamp", Mesh = lamp, Transform = TownBuilder.Place(gen, site, px, lampSouth) });
            }
        }
    }

    // =============================================================================================
    //  Fields round the settlements (laid on the terrain once physics is ready)
    // =============================================================================================

    private enum Crop { Wheat, Ploughed, Greens, Rapeseed, Pasture }

    /// <summary>A rectangular field aligned with its settlement's streets: centre and size in metres east/south of the site.</summary>
    private sealed record Field(Site Site, float East, float South, float W, float L, Crop Crop, bool StripesAcross, uint Seed);

    private static readonly Dictionary<PlanetGen, List<Field>> _fields = new();

    /// <summary>Unit direction of a point east/south of a site (the frame the town is built in).</summary>
    private static Vector3 Dir(PlanetGen gen, Site site, float east, float south) => TownBuilder.Place(gen, site, east, south).Origin.Normalized();

    private static (Vector3 C, Vector3 E, Vector3 N) Frame(Site site)
    {
        var c = PlanetBuilder.ToGodot(site.Dir).Normalized();
        var e = Vector3.Up.Cross(c).Normalized();
        return (c, e, c.Cross(e));
    }

    private static List<Field> FieldsOf(PlanetGen gen)
    {
        if (_fields.TryGetValue(gen, out var list)) return list;
        list = new List<Field>();
        foreach (var site in gen.Sites)
        {
            float inner = (float)site.RadiusM * 1.15f + 12f, outer = inner + 150f;
            const float CellW = 50f, CellL = 66f;
            int n = (int)(outer / CellW) + 1;
            for (int i = -n; i <= n; i++)
                for (int j = -n; j <= n; j++)
                {
                    uint h = Hash(site.Id, (uint)((i + 50) * 131 + (j + 50)) ^ (uint)gen.Seed);
                    float e0 = i * CellW, s0 = j * CellL;
                    float d = Mathf.Sqrt(e0 * e0 + s0 * s0);
                    if (d < inner || d > outer) continue;
                    if (h % 100 < 18) continue; // gaps: rough ground, a copse, nobody's land
                    float w = CellW - 7f - Frac(h, 8) * 8f, l = CellL - 7f - Frac(h, 16) * 10f;
                    if (!Farmable(gen, site, e0, s0, w + 16, l + 16))
                    {
                        // a smaller field in the better half of the plot, if that half will take one
                        float hs = (h >> 3) % 2 == 0 ? -1 : 1;
                        e0 += hs * CellW / 4; w = w / 2 - 2;
                        if (!Farmable(gen, site, e0, s0, w + 12, l + 12)) continue;
                    }
                    var crop = (Crop)((h >> 24) % 5);
                    list.Add(new Field(site, e0, s0, w, l, crop, (h >> 5) % 2 == 0, h));
                }
        }
        _fields[gen] = list;
        return list;
    }

    /// <summary>Gentle land, clear of the sea, the roads and every settlement.</summary>
    private static bool Farmable(PlanetGen gen, Site site, float east, float south, float w, float l)
    {
        float lo = float.MaxValue, hi = float.MinValue;
        for (int a = 0; a <= 4; a++)
            for (int b = 0; b <= 4; b++)
            {
                var dir = Dir(gen, site, east + (a / 4f - 0.5f) * w, south + (b / 4f - 0.5f) * l);
                var sd = PlanetBuilder.ToSim(dir);
                foreach (var s in gen.Sites)
                    if ((PlanetBuilder.ToGodot(s.Dir).Normalized() - dir).Length() * (float)gen.RadiusM < s.RadiusM * 1.12f) return false;
                if (gen.IsPaved(sd)) return false;
                if (gen.BiomeAt(sd) is not (Biome.Grassland or Biome.Forest or Biome.Desert or Biome.Tundra)) return false;
                float ht = (float)gen.Height(sd);
                lo = Mathf.Min(lo, ht); hi = Mathf.Max(hi, ht);
            }
        return lo > 3.5f && hi - lo < 10f;
    }

    /// <summary>True inside a field (so the scattered trees leave the crops alone).</summary>
    public static bool InField(PlanetGen gen, Vector3 dir)
    {
        float R = (float)gen.RadiusM;
        foreach (var site in gen.Sites)
        {
            var (c, e, n) = Frame(site);
            var d = (dir.Normalized() - c) * R;
            float east = d.Dot(e), south = -d.Dot(n);
            if (east * east + south * south > 400f * 400f) continue;
            foreach (var f in FieldsOf(gen))
                if (f.Site == site && Mathf.Abs(east - f.East) < f.W / 2 + 1 && Mathf.Abs(south - f.South) < f.L / 2 + 1) return true;
        }
        return false;
    }

    public static void Fields(Node3D root, PlanetGen gen, PhysicsDirectSpaceState3D space)
    {
        float R = (float)gen.RadiusM;
        Vector3? Ground(Vector3 dir, float lift)
        {
            var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(dir * (R + 400), dir * (R - 100), 1));
            return hit.Count > 0 ? (Vector3)hit["position"] + dir * lift : null;
        }

        var hedge = new LowPoly().Box(new Vector3(0, 0.6f, 0), new Vector3(1.0f, 1.2f, 3.3f), Hedge)
            .Gem(new Vector3(0, 1.15f, 0.6f), 0.6f, 0.35f, 0.2f, 5, Hedge * 1.08f).Build();
        var oak = new LowPoly(); Tree(oak, Vector3.Zero, 1.3f);
        var oakMesh = Props.MeshOf("nature.oak") ?? oak.Build();
        var bale = new LowPoly().Cylinder(Vector3.Zero, 0.65f, 0.65f, 1.2f, 8, new Color(0.85f, 0.74f, 0.42f)).Build();
        var sheep = new LowPoly().Box(new Vector3(0, 0.55f, 0), new Vector3(0.6f, 0.5f, 1.0f), new Color(0.93f, 0.92f, 0.86f))
            .Box(new Vector3(0, 0.7f, 0.6f), new Vector3(0.28f, 0.3f, 0.32f), new Color(0.15f, 0.14f, 0.13f))
            .Box(new Vector3(-0.18f, 0.15f, 0.3f), new Vector3(0.08f, 0.3f, 0.08f), new Color(0.15f, 0.14f, 0.13f))
            .Box(new Vector3(0.18f, 0.15f, -0.3f), new Vector3(0.08f, 0.3f, 0.08f), new Color(0.15f, 0.14f, 0.13f)).Build();
        var props = new Dictionary<Mesh, List<Transform3D>> { [hedge] = new(), [oakMesh] = new(), [bale] = new(), [sheep] = new() };

        // something standing on the ground at (east, south), turned to face along `along` (site frame), optionally lying on its side
        void Stand(Mesh mesh, Site site, float east, float south, Vector3 along, float scale = 1f, bool onSide = false, float sink = 0.05f)
        {
            var dir = Dir(gen, site, east, south);
            if (Ground(dir, -sink) is not { } p) return;
            var (_, e, n) = Frame(site);
            var fwd = (e * along.X - n * along.Z).Normalized();
            fwd = (fwd - dir * fwd.Dot(dir)).Normalized();
            var basis = new Basis(dir.Cross(fwd), dir, fwd);   // up is away from the planet, +Z runs along `along`
            if (onSide) basis = basis * new Basis(Vector3.Right, Mathf.Pi / 2);
            props[mesh].Add(new Transform3D(basis.Scaled(Vector3.One * scale), p));
        }

        foreach (var site in gen.Sites)
        {
            var lp = new LowPoly();
            foreach (var f in FieldsOf(gen).Where(f => f.Site == site))
            {
                var (ca, cb) = f.Crop switch
                {
                    Crop.Wheat => (new Color(0.84f, 0.72f, 0.38f), new Color(0.77f, 0.64f, 0.32f)),
                    Crop.Ploughed => (new Color(0.46f, 0.34f, 0.23f), new Color(0.38f, 0.27f, 0.18f)),
                    Crop.Greens => (new Color(0.40f, 0.58f, 0.25f), new Color(0.32f, 0.49f, 0.21f)),
                    Crop.Rapeseed => (new Color(0.92f, 0.84f, 0.30f), new Color(0.85f, 0.76f, 0.26f)),
                    _ => (new Color(0.52f, 0.68f, 0.33f), new Color(0.50f, 0.66f, 0.32f)),
                };
                // a grid of quads draped on the terrain: 3 m furrows one way, 4 m the other
                float stepA = 3f, stepB = 4f;
                int na = Mathf.CeilToInt((f.StripesAcross ? f.L : f.W) / stepA), nb = Mathf.CeilToInt((f.StripesAcross ? f.W : f.L) / stepB);
                var grid = new Vector3?[na + 1, nb + 1];
                for (int a = 0; a <= na; a++)
                    for (int b = 0; b <= nb; b++)
                    {
                        float ua = a / (float)na - 0.5f, ub = b / (float)nb - 0.5f;
                        float east = f.East + (f.StripesAcross ? ub * f.W : ua * f.W), south = f.South + (f.StripesAcross ? ua * f.L : ub * f.L);
                        grid[a, b] = Ground(Dir(gen, site, east, south), 0.3f);
                    }
                for (int a = 0; a < na; a++)
                    for (int b = 0; b < nb; b++)
                        if (grid[a, b] is { } p00 && grid[a + 1, b] is { } p10 && grid[a + 1, b + 1] is { } p11 && grid[a, b + 1] is { } p01)
                        {
                            var col = a % 2 == 0 ? ca : cb;
                            col *= 0.97f + Frac(f.Seed ^ (uint)(a * 7 + b * 13), 4) * 0.06f; col.A = 1;
                            lp.Quad(p00, p10, p11, p01, col, p00.Normalized());
                        }

                // hedgerows round the edge, with a gate gap; an oak in the hedge now and then
                float hw = f.W / 2 + 1.5f, hl = f.L / 2 + 1.5f;
                int gateEdge = (int)(f.Seed % 4);
                var edges = new[]
                {
                    (new Vector2(-hw, -hl), new Vector2(hw, -hl)), (new Vector2(hw, -hl), new Vector2(hw, hl)),
                    (new Vector2(hw, hl), new Vector2(-hw, hl)), (new Vector2(-hw, hl), new Vector2(-hw, -hl)),
                };
                for (int ei = 0; ei < 4; ei++)
                {
                    var (a, b) = edges[ei];
                    float len = (b - a).Length();
                    int units = Mathf.Max(1, Mathf.RoundToInt(len / 3f));
                    var along = new Vector3(b.X - a.X, 0, b.Y - a.Y).Normalized();
                    for (int u = 0; u < units; u++)
                    {
                        if (ei == gateEdge && Mathf.Abs(u - units / 2) <= 1) continue;
                        var pt = a + (b - a) * ((u + 0.5f) / units);
                        uint uh = f.Seed ^ (uint)(ei * 977 + u * 131);
                        if ((uh >> 3) % 23 == 0) continue; // a gap where the hedge has thinned
                        Stand(hedge, site, f.East + pt.X, f.South + pt.Y, along, 0.85f + Frac(uh, 10) * 0.35f);
                        if ((uh >> 7) % 11 == 0) Stand(oakMesh, site, f.East + pt.X, f.South + pt.Y, along, 0.8f + Frac(uh, 14) * 0.5f);
                    }
                }

                // pasture has hay bales or sheep
                if (f.Crop == Crop.Pasture)
                {
                    bool flock = f.Seed % 2 == 0;
                    int count = flock ? 7 : 5;
                    for (int q = 0; q < count; q++)
                    {
                        uint qh = f.Seed ^ (uint)(q * 7919);
                        float ex = f.East + (Frac(qh, 0) - 0.5f) * f.W * 0.8f, sx = f.South + (Frac(qh, 8) - 0.5f) * f.L * 0.8f;
                        var along = new Vector3(Frac(qh, 16) - 0.5f, 0, Frac(qh, 24) - 0.5f);
                        if (along.LengthSquared() < 0.01f) along = Vector3.Forward;
                        if (flock) Stand(sheep, site, ex, sx, along);
                        else Stand(bale, site, ex, sx, along, 1f, onSide: true, sink: -0.6f);
                    }
                }
            }
            if (lp.TriangleCount > 0)
            {
                var mi = lp.Instance("Fields-" + site.Id);
                mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                root.AddChild(mi);
            }
        }

        foreach (var (mesh, xforms) in props)
        {
            if (xforms.Count == 0) continue;
            var centre = Vector3.Zero;
            foreach (var x in xforms) centre += x.Origin;
            centre /= xforms.Count;
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = xforms.Count };
            for (int i = 0; i < xforms.Count; i++) mm.SetInstanceTransform(i, new Transform3D(xforms[i].Basis, xforms[i].Origin - centre));
            root.AddChild(new MultiMeshInstance3D { Multimesh = mm, Position = centre });
        }
    }
}
