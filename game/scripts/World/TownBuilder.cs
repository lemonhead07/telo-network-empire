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
    public required Node3D Body { get; init; }
    /// <summary>Houses you can walk into have a door that opens and places to do an install. Null for other buildings.</summary>
    public InstallSite? Install { get; init; }
    public Action<bool>? SetDoor { get; init; }
    public bool DoorOpen { get; set; }
    /// <summary>Warm light in the windows: someone is in and it is dark out.</summary>
    public Node3D? Lights { get; init; }
    /// <summary>The household's car. Gone when they are out at work.</summary>
    public Node3D? Car { get; init; }
    /// <summary>Whoever lives or works here, standing indoors (by the front window; Sol behind his counter). Shown while they are in.</summary>
    public Node3D? Resident { get; init; }
    /// <summary>Where a visiting van parks: on the driveway (or the shop's forecourt, the farmyard), off the street.</summary>
    public Vector3? Park { get; init; }
    /// <summary>Where the driveway meets the middle of Main Street. Null for places not on Main Street.</summary>
    public Vector3? DriveEntry { get; init; }
    /// <summary>The Yard's loading bay and lorry (EA2b): built with the Yard, shown once its lorry has big parts to deliver.</summary>
    public Node3D? Bay { get; init; }
}

/// <summary>What the rest of the game needs to know about the settlements.</summary>
public sealed class Town
{
    public required Node3D Root { get; init; }
    public required Node3D Hq { get; init; }
    public required Transform3D PlayerSpawn { get; init; }
    public required Transform3D VanSpawn { get; init; }
    public required Vector3 Centre { get; init; }
    /// <summary>The van's own spot beside your room, and where that driveway meets Main Street.</summary>
    public required Vector3 HqPark { get; init; }
    public required Vector3 HqEntry { get; init; }
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
        if (Props.HasModel("room.hq")) hq.AddChild(Props.Make("room.hq"));   // the outside of the room: walls, roof, sign board
        if (Props.HasModel("room.clutter")) hq.AddChild(Props.Make("room.clutter")); // the rug, the kettle, the coat by the door

        var town = new Town
        {
            Root = root, Hq = hq,
            PlayerSpawn = hq.Transform * new Transform3D(Basis.Identity, new Vector3(0.5f, 0.1f, 1.2f)),
            VanSpawn = Place(gen, 8, 10.5f, -90, 0.3f),
            Centre = hq.Transform.Origin,
            HqPark = Place(gen, HqDriveEast, 9f, 0, 0.3f).Origin,
            HqEntry = Place(gen, HqDriveEast, StreetSouth, 0, 0.3f).Origin,
        };
        // your own driveway: the van lives here, off the street
        hq.AddChild(new LowPoly().Box(new Vector3(HqDriveEast, -0.07f, (3f + StreetSouth - StreetHalfWidth) / 2), new Vector3(3.0f, 0.06f, StreetSouth - StreetHalfWidth - 3f), Concrete).Instance("Driveway"));

        // --- buildings from content ---------------------------------------------------------------
        var palette = new[] { new Color(0.80f, 0.72f, 0.58f), new Color(0.70f, 0.76f, 0.78f), new Color(0.82f, 0.62f, 0.52f), new Color(0.74f, 0.78f, 0.64f), new Color(0.86f, 0.82f, 0.72f) };
        var roofs = new[] { new Color(0.50f, 0.22f, 0.18f), new Color(0.28f, 0.30f, 0.34f), new Color(0.38f, 0.28f, 0.20f) };
        int i = 0;
        // far towns after the rest, so adding one never changes the look (model, colours) of the houses already built (EA3a)
        foreach (var lot in content.Lots.Values.OrderBy(l => Telo.Sim.Town.TownRules.FarTowns.Contains(l.Site)).ThenBy(l => l.Id))
        {
            if (gen.SiteById(lot.Site) is not { } site) continue;
            var xf = Place(gen, site, (float)lot.East, (float)lot.South, (float)lot.Yaw);
            float wallH = 3f + (i % 2) * 0.5f;
            var solids = new List<(Vector3 Pos, Vector3 Size)>();
            // a house's look comes from one of six models (house.a-f) when they exist; its walls still come from HouseMesh
            string? model = lot.Kind switch
            {
                LotKind.House => $"house.{(char)('a' + i % 6)}",
                LotKind.Shop => "shop.sols",
                LotKind.Depot => "depot.yard",
                LotKind.Farm => "farm.hollow",
                LotKind.Yard => "yard.counter",
                _ => null,
            };
            Mesh mesh = lot.Kind switch
            {
                LotKind.Shop => ShopMesh(),
                LotKind.Farm => FarmMesh(solids),
                LotKind.Depot => DepotMesh(),
                LotKind.Yard => YardMesh(solids),
                _ => HouseMesh(palette[i % palette.Length], roofs[i % roofs.Length], wallH, solids),
            };
            i++;
            // how far the street's pole line is from this front door (the drop wire comes from there)
            var street = StreetOf(lot);
            float poleDist = Mathf.Abs((float)lot.South - (street is null ? PoleLineSouth : street.PoleLine));
            var look = model is not null && Props.HasModel(model) ? Props.Make(model) : null;
            town.Lots.Add(Building(root, lot, xf, mesh, solids, wallH, poleDist, palette[(i + 2) % palette.Length], street, look));
        }
        TownDressing.Gardens(root, gen, town);

        // --- the streets: Main Street, Church Lane and Mill Row in town, one through each village, Market Street in Millbrook
        var road = new LowPoly().Box(Vector3.Zero, new Vector3(6.3f, 0.08f, StreetHalfWidth * 2), new Color(0.27f, 0.27f, 0.29f))
            .Box(new Vector3(0, 0.045f, 0), new Vector3(2.2f, 0.01f, 0.16f), new Color(0.85f, 0.8f, 0.6f)).Build();
        var pole = PoleMesh();
        foreach (var st in Streets)
        {
            if (gen.SiteById(st.Site) is not { } site) continue;
            for (float x = st.From; x <= st.To + 0.01f; x += 6)
                root.AddChild(new MeshInstance3D
                {
                    Mesh = road, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    Transform = st.NorthSouth ? Place(gen, site, st.At, x, 90, 0.05f) : Place(gen, site, x, st.At, 0, 0.05f),
                });
            // telephone poles: the incumbent's network, already here before you
            if (!st.NorthSouth)
                foreach (float x in st.Poles())
                    root.AddChild(new MeshInstance3D { Mesh = pole, Transform = Place(gen, site, x, st.PoleLine) });
        }

        return town;
    }

    /// <summary>
    /// A street: east-west (at a distance south of its site's centre) or north-south (at a distance east), between two
    /// offsets. East-west streets have the incumbent's pole line along their north side and houses either side.
    /// </summary>
    public sealed record Street(string Site, bool NorthSouth, float At, float From, float To, string Name)
    {
        public float PoleLine => At - 5f;
        public IEnumerable<float> Poles() { for (float x = Mathf.Ceil(From / 30f) * 30f; x <= To; x += 30) yield return x; }
    }

    /// <summary>Main Street: its centre line is this far south of the town centre, it is this wide, and runs this far east and west.</summary>
    public const float StreetSouth = 16.5f, StreetHalfWidth = 3.25f, StreetEnd = 112f;

    public static readonly Street[] Streets =
    {
        new("town-start", false, StreetSouth, -StreetEnd, StreetEnd, "Main Street"),
        new("town-start", false, -40f, -86f, 84f, "Church Lane"),          // its east end meets the road out to the farm
        new("town-start", true, -86f, -40f, StreetSouth, "Mill Row"),           // joins Church Lane to Main Street
        new("eastgate", false, 8f, -64f, 48f, "Eastgate High Street"),  // west end reaches the two houses moved off the town road (2026-11-19)
        new("wrenfold", false, 8f, -48f, 48f, "Wrenfold Green"),            // EA2f: the first Act 1 village, opened by your own poles
        new("kettle-cross", false, 8f, -48f, 48f, "Kettle Cross Row"),       // EA2f: the second Act 1 village, by the shore
        new("highmoor", false, 8f, -60f, 60f, "Highmoor Street"),            // EA2f: the Act 2 village, a long drive up the hill
        new("millbrook", false, 8f, -56f, 56f, "Market Street"),            // EA3a: Millbrook, the market town on past the farm
        new("millbrook", true, -41f, -36f, 8f, "Mill Lane"),                  // up from Market Street to where the farm road comes in
        new("farland", false, 8f, -52f, 52f, "Quay Street"),                 // EA3b: Farland's harbour town, across the water
        new("farland", true, -5f, -56f, 8f, "Harbour Lane"),                  // up from Quay Street, through the gap in the north row, to the ferry road
    };

    /// <summary>The east-west street a building fronts on: the nearest one on its site that runs past it. Null for the farm.</summary>
    public static Street? StreetOf(LotDef lot) =>
        lot.Kind == LotKind.Farm ? null : Streets.Where(s => !s.NorthSouth && s.Site == lot.Site && lot.East >= s.From - 10 && lot.East <= s.To + 10)
            .OrderBy(s => Mathf.Abs(s.At - (float)lot.South)).FirstOrDefault();

    private const float HqDriveEast = 8f;
    internal const float DriveOffset = 5.7f;
    private static readonly Color Concrete = new(0.52f, 0.51f, 0.48f);

    /// <summary>Which side of a house its driveway goes: the right (+1) unless that would run through a street pole.</summary>
    internal static int DriveSide(LotDef lot, bool facingSouth, Street street)
    {
        if (!facingSouth) return 1; // the pole line is on the north side of the street only
        float east = (float)lot.East + DriveOffset;
        return street.Poles().Any(p => Mathf.Abs(p - east) < 3f) ? -1 : 1;
    }

    private static Mesh? _pole;
    /// <summary>A telephone pole with a crossarm (along its local Z) and two insulators. Shared by the town and by lines the player builds.</summary>
    public static Mesh PoleMesh()
    {
        var brown = new Color(0.33f, 0.24f, 0.16f);
        return _pole ??= Props.MeshOf("world.line-pole") ?? new LowPoly().Cylinder(Vector3.Zero, 0.13f, 0.09f, 7.2f, 6, brown)
            .Box(new Vector3(0, 6.6f, 0), new Vector3(0.12f, 0.12f, 1.8f), brown)
            .Box(new Vector3(0, 6.75f, -0.7f), new Vector3(0.08f, 0.18f, 0.08f), new Color(0.75f, 0.8f, 0.78f))
            .Box(new Vector3(0, 6.75f, 0.7f), new Vector3(0.08f, 0.18f, 0.08f), new Color(0.75f, 0.8f, 0.78f)).Build();
    }

    public const float PoleLineSouth = 11.5f;
    /// <summary>Height of a house's floor above its lot origin (keeps the lawn from poking through the carpet).</summary>
    public const float HouseFloor = 0.15f;

    private static LotView Building(Node3D root, LotDef lot, Transform3D xf, Mesh mesh, List<(Vector3 Pos, Vector3 Size)> solids, float wallH, float poleDist, Color carColour, Street? street, Node3D? look = null)
    {
        bool shop = lot.Kind == LotKind.Shop, house = lot.Kind == LotKind.House, yard = lot.Kind == LotKind.Yard;
        float w = shop ? 10 : yard ? 16 : 8, d = shop ? 8 : yard ? 10 : 7, front = d / 2;
        var body = new StaticBody3D { Name = lot.Id, Transform = xf };
        body.AddChild(look ?? new MeshInstance3D { Mesh = mesh });
        if (!house) body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(w, 4, d) }, Position = new Vector3(0, 1.5f, 0) });
        foreach (var (pos, size) in solids) body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = pos });
        root.AddChild(body);

        // A driveway from Main Street: the household's car lives on it, and a visiting van parks on it instead of in the road.
        // Houses get one beside the house; the shop a forecourt; the depot one up to its roller door. The farm has a yard.
        Vector3? park = null, entry = null;
        float driveX = 0;
        if (street is not null)
        {
            bool facingSouth = Mathf.Abs(Mathf.Wrap((float)lot.Yaw, -180f, 180f)) < 90;
            float zStreet = facingSouth ? street.At - (float)lot.South : (float)lot.South - street.At;
            driveX = lot.Kind switch { LotKind.Shop => -3f, LotKind.Depot => -1.9f, LotKind.Yard => YardDriveX, _ => DriveSide(lot, facingSouth, street) * DriveOffset };
            float start = house ? -2.6f : front, end = zStreet - StreetHalfWidth;
            if (end > start + 2)
            {
                body.AddChild(new LowPoly().Box(new Vector3(driveX, 0.03f, (start + end) / 2), new Vector3(3.0f, 0.06f, end - start), Concrete).Instance("Driveway"));
                park = xf * new Vector3(driveX, 0.3f, front + 4.2f);
                entry = xf * new Vector3(driveX, 0.3f, zStreet);
            }
        }
        else if (lot.Kind == LotKind.Farm) park = xf * new Vector3(-7f, 0.3f, 8f);

        InstallSite? install = null;
        Action<bool>? setDoor = null;
        Node3D? lights = null, car = null;
        if (house)
        {
            // a ramp over the doorstep, a door that can stand open, a ceiling light, and the install points
            var ramp = new StaticBody3D { Position = new Vector3(0, HouseFloor - 0.115f, front + 0.75f), RotationDegrees = new Vector3(6.5f, 0, 0) };
            ramp.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.5f, 0.1f, 1.6f) } });
            ramp.AddChild(new LowPoly().Box(Vector3.Zero, new Vector3(1.5f, 0.1f, 1.6f), new Color(0.6f, 0.58f, 0.55f)).Instance());
            body.AddChild(ramp);

            var brown = new Color(0.34f, 0.22f, 0.15f);
            var closed = new StaticBody3D { Position = new Vector3(0, HouseFloor + 1.02f, front - 0.1f) };
            var shape = new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.1f, 2.05f, 0.2f) } };
            closed.AddChild(shape);
            closed.AddChild(new LowPoly().Box(Vector3.Zero, new Vector3(1.08f, 2.04f, 0.1f), brown)
                .Box(new Vector3(0.38f, 0, 0.07f), new Vector3(0.06f, 0.06f, 0.06f), new Color(0.8f, 0.7f, 0.3f)).Instance());
            body.AddChild(closed);
            var open = new LowPoly().Box(new Vector3(-0.52f, HouseFloor + 1.02f, front - 0.7f), new Vector3(0.08f, 2.04f, 1.04f), brown).Instance();
            open.Visible = false;
            body.AddChild(open);
            setDoor = isOpen => { closed.Visible = !isOpen; open.Visible = isOpen; shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, isOpen); };

            body.AddChild(new OmniLight3D { Position = new Vector3(0, wallH - 0.6f, 0), LightColor = new Color(1f, 0.86f, 0.68f), LightEnergy = 1.0f, OmniRange = 7 });
            install = new InstallSite(wallH, HouseFloor, poleDist);
            body.AddChild(install);

            // who is in: warm windows after dark, and (for households that go out to work) a car on the drive
            lights = new Node3D { Visible = false };
            var warm = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.82f, 0.5f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
            foreach (float x in new[] { -2.5f, 2.5f })
                foreach (float z in new[] { 3.585f, -3.585f })
                    lights.AddChild(new MeshInstance3D { Position = new Vector3(x, 1.7f, z), Mesh = new BoxMesh { Size = new Vector3(1.3f, 1.0f, 0.02f) }, MaterialOverride = warm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            body.AddChild(lights);
            if (Telo.Sim.Town.HomeRules.HasCar(lot))
            {
                var dark = new Color(0.1f, 0.1f, 0.11f);
                var lp = new LowPoly().Box(new Vector3(0, 0.55f, 0), new Vector3(1.7f, 0.6f, 3.9f), carColour).Box(new Vector3(0, 1.05f, 0.2f), new Vector3(1.5f, 0.5f, 2.0f), carColour * 0.9f)
                    .Box(new Vector3(0, 1.07f, -0.82f), new Vector3(1.4f, 0.4f, 0.04f), new Color(0.3f, 0.42f, 0.52f));
                foreach (var (wx, wz) in new[] { (-0.85f, -1.25f), (0.85f, -1.25f), (-0.85f, 1.25f), (0.85f, 1.25f) }) lp.Box(new Vector3(wx, 0.3f, wz), new Vector3(0.22f, 0.6f, 0.6f), dark);
                var carModel = $"car.{new[] { "saloon", "estate", "hatch", "coupe" }[(lot.Id.Sum(ch => ch) + lot.Resident.Length) % 4]}";
                car = Props.HasModel(carModel) ? Props.Make(carModel) : lp.Instance("Car");
                car.Position = new Vector3(park is null ? DriveOffset : driveX, 0.06f, 1.2f); // at the top of the driveway, by the house
                body.AddChild(car);
            }
        }

        // the person who lives or works here, when there's a model for them
        Node3D? resident = null;
        string? who = house ? $"person.res-{lot.Archetype}" : shop ? "person.sol" : yard ? "person.engineer" : null;
        if (who is not null && Props.HasModel(who))
        {
            resident = Props.Make(who);
            resident.Name = "Resident";
            // in the front room by the left window, turned a little towards the door; Sol behind the counter hatch
            resident.Position = house ? new Vector3(-2.3f, HouseFloor, 2.2f) : yard ? new Vector3(-0.3f, 0.02f, 5.35f) : new Vector3(3.0f, 0.02f, 2.75f);
            resident.RotationDegrees = new Vector3(0, house ? 25 : 0, 0);
            body.AddChild(resident);
        }

        Interactable Hotspot(Vector3 pos, Vector3 size)
        {
            var it = new Interactable { Position = pos };
            it.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
            body.AddChild(it);
            return it;
        }
        // the Yard's "door" is the front edge of its trade counter (the counter itself is solid, waist high)
        var door = yard ? Hotspot(new Vector3(0, 1.1f, front + 1.25f), new Vector3(2.8f, 0.8f, 0.3f))
            : Hotspot(new Vector3(0, 1.05f, front + 0.15f), new Vector3(1.3f, 2.1f, 0.4f));
        if (house || yard) { door.CollisionLayer = 2; door.CollisionMask = 0; } // you can look at it and use it; it doesn't stop you walking
        var counter = shop ? Hotspot(new Vector3(3f, 1.4f, front + 0.15f), new Vector3(2.6f, 1.6f, 0.4f)) : null;

        // the Yard's loading bay and its lorry, round the east side: shown once there are big parts to deliver (D43: the Yard grows)
        Node3D? bay = null;
        if (yard)
        {
            bay = Props.Make("yard.bay", () => YardBay().Instance("YardBay"));
            bay.Position = new Vector3(-w / 2 - 5.5f, 0, -0.5f);
            bay.Visible = false;
            body.AddChild(bay);
            body.AddChild(new Label3D
            {
                Text = "TRADE COUNTER", Position = new Vector3(0, 3.0f, front + 1.42f), FontSize = 64, PixelSize = 0.22f / 64f,
                Modulate = new Color(0.95f, 0.93f, 0.85f), OutlineSize = 0,
            });
        }

        var name = new Label3D
        {
            Text = yard ? $"{lot.Resident.ToUpperInvariant()}\nbuilders' and line merchants" : lot.Resident,
            Position = new Vector3(0, shop ? 3.25f : yard ? 4.75f : lot.Kind == LotKind.Depot ? 2.95f : 2.45f, front + 0.12f), FontSize = 64,
            PixelSize = (shop ? 0.5f : yard ? 0.4f : lot.Kind == LotKind.Depot ? 0.36f : 0.16f) / 64f,
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
        return new LotView { Lot = lot, Door = door, Marker = marker, Counter = counter, Position = xf.Origin, Body = body, Install = install, SetDoor = setDoor, Lights = lights, Car = car, Park = park, DriveEntry = entry, Resident = resident, Bay = bay };
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
                bool skip = gen.Sites.Any(s => (PlanetBuilder.ToGodot(s.Dir) - c).Length() * R < s.RadiusM * 0.75f); // the drawn road starts where the streets end
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

    /// <summary>A hollow house you can walk into: thin walls around a doorway, a floor, a ceiling and a few sticks of furniture.</summary>
    private static Mesh HouseMesh(Color wall, Color roof, float wallH, List<(Vector3 Pos, Vector3 Size)> solids)
    {
        const float F = HouseFloor, T = 0.2f;
        var glass = new Color(0.30f, 0.42f, 0.52f); var trim = new Color(0.93f, 0.9f, 0.84f);
        var wood = new Color(0.42f, 0.28f, 0.16f); var fabric = new Color(0.36f, 0.42f, 0.5f);
        var lp = new LowPoly();
        void Solid(Vector3 c, Vector3 s, Color col) { lp.Box(c, s, col); solids.Add((c, s)); }

        Solid(new Vector3(0, -0.3f, 0), new Vector3(8, 0.6f, 7), wall);                                  // foundation
        Solid(new Vector3(0, F / 2, 0), new Vector3(8 - 2 * T, F, 7 - 2 * T), new Color(0.5f, 0.36f, 0.24f)); // floorboards
        Solid(new Vector3(0, F / 2, 3.5f - T / 2), new Vector3(1.1f, F, T), new Color(0.6f, 0.58f, 0.55f));    // threshold
        Solid(new Vector3(0, wallH / 2, -3.5f + T / 2), new Vector3(8, wallH, T), wall);
        Solid(new Vector3(-4 + T / 2, wallH / 2, 0), new Vector3(T, wallH, 7 - 2 * T), wall);
        Solid(new Vector3(4 - T / 2, wallH / 2, 0), new Vector3(T, wallH, 7 - 2 * T), wall);
        foreach (int side in new[] { -1, 1 })                                                            // front wall, either side of the door
            Solid(new Vector3(side * 2.275f, wallH / 2, 3.5f - T / 2), new Vector3(3.45f, wallH, T), wall);
        float lintel = F + 2.05f;
        Solid(new Vector3(0, (lintel + wallH) / 2, 3.5f - T / 2), new Vector3(1.1f, wallH - lintel, T), wall);
        Solid(new Vector3(0, wallH - 0.05f, 0), new Vector3(8 - 2 * T, 0.1f, 7 - 2 * T), new Color(0.9f, 0.87f, 0.8f)); // ceiling

        lp.GableRoof(new Vector3(0, wallH, 0), 8.8f, 7.8f, 2.3f, roof)
          .Box(new Vector3(2.6f, wallH + 1.6f, -1.2f), new Vector3(0.7f, 1.8f, 0.7f), new Color(0.55f, 0.36f, 0.3f));
        foreach (float x in new[] { -2.5f, 2.5f })
        {
            lp.Box(new Vector3(x, 1.7f, 3.52f), new Vector3(1.5f, 1.2f, 0.06f), trim);
            lp.Box(new Vector3(x, 1.7f, 3.55f), new Vector3(1.3f, 1.0f, 0.06f), glass);
            lp.Box(new Vector3(x, 1.7f, -3.55f), new Vector3(1.3f, 1.0f, 0.06f), glass);
            lp.Box(new Vector3(x, 1.7f, 3.28f), new Vector3(1.5f, 1.2f, 0.04f), trim);       // the same windows seen from indoors
            lp.Box(new Vector3(x, 1.7f, 3.26f), new Vector3(1.3f, 1.0f, 0.04f), new Color(0.62f, 0.78f, 0.9f));
            lp.Box(new Vector3(x, 1.7f, -3.28f), new Vector3(1.5f, 1.2f, 0.04f), trim);
            lp.Box(new Vector3(x, 1.7f, -3.26f), new Vector3(1.3f, 1.0f, 0.04f), new Color(0.62f, 0.78f, 0.9f));
        }

        // placeholder furniture: hall table (the telephone lives here), sofa, rug, television
        Solid(new Vector3(-3.35f, F + 0.35f, -0.5f), new Vector3(0.7f, 0.7f, 0.9f), wood);
        Solid(new Vector3(1.6f, F + 0.25f, -2.7f), new Vector3(2.2f, 0.5f, 0.9f), fabric);
        lp.Box(new Vector3(1.6f, F + 0.6f, -3.05f), new Vector3(2.2f, 0.7f, 0.2f), fabric)
          .Box(new Vector3(1.5f, F + 0.01f, -0.9f), new Vector3(2.6f, 0.02f, 1.8f), new Color(0.55f, 0.25f, 0.22f));
        Solid(new Vector3(3.4f, F + 0.3f, 0.8f), new Vector3(0.6f, 0.6f, 1.0f), wood);
        lp.Box(new Vector3(3.35f, F + 0.85f, 0.8f), new Vector3(0.5f, 0.5f, 0.6f), new Color(0.15f, 0.15f, 0.16f))
          .Box(new Vector3(3.09f, F + 0.87f, 0.8f), new Vector3(0.02f, 0.36f, 0.46f), new Color(0.3f, 0.4f, 0.42f));
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

    /// <summary>1 Main Street: a small yard and workshop, to let.</summary>
    /// <summary>Where a visiting van parks at the Yard: the forecourt on its east side, clear of the stock.</summary>
    internal const float YardDriveX = -5.5f;

    /// <summary>
    /// The Yard (EA2b, prop yard.counter): a tall corrugated trade shed, its counter hatch under a canopy in the middle of the
    /// front (where you buy), sliding doors either side, and stock out in the open on the west side: poles on a rack, cable
    /// drums, green street cabinets on pallets. Local +Z faces the street; with the lot's yaw of 180, local +X is west.
    /// </summary>
    private static Mesh YardMesh(List<(Vector3 Pos, Vector3 Size)> solids)
    {
        var wall = new Color(0.44f, 0.52f, 0.47f); var roof = new Color(0.33f, 0.35f, 0.36f); var dark = new Color(0.16f, 0.17f, 0.18f);
        var wood = new Color(0.56f, 0.42f, 0.26f); var pole = new Color(0.33f, 0.24f, 0.16f); var cab = new Color(0.2f, 0.42f, 0.28f);
        var lp = new LowPoly()
            .Box(new Vector3(0, 2.75f, 0), new Vector3(16, 5.5f, 10), wall)
            .GableRoof(new Vector3(0, 5.5f, 0), 16.6f, 10.6f, 1.4f, roof)
            .Box(new Vector3(0, 4.75f, 5.03f), new Vector3(9.5f, 1.0f, 0.06f), new Color(0.93f, 0.9f, 0.8f))     // name board
            .Box(new Vector3(-5.2f, 2.1f, 5.03f), new Vector3(3.6f, 4.2f, 0.08f), new Color(0.36f, 0.42f, 0.4f)) // sliding doors
            .Box(new Vector3(5.2f, 2.1f, 5.03f), new Vector3(3.6f, 4.2f, 0.08f), new Color(0.36f, 0.42f, 0.4f))
            // the trade counter: a hatch with a worn top, a bell and a canopy over it
            .Box(new Vector3(0, 1.75f, 5.02f), new Vector3(2.6f, 1.3f, 0.06f), dark)
            .Box(new Vector3(0, 1.08f, 5.95f), new Vector3(2.8f, 0.08f, 0.6f), wood)
            .Box(new Vector3(0, 0.52f, 5.95f), new Vector3(2.8f, 1.04f, 0.5f), new Color(0.62f, 0.6f, 0.55f))
            .Cylinder(new Vector3(0.9f, 1.12f, 6.0f), 0.07f, 0.03f, 0.07f, 6, new Color(0.85f, 0.75f, 0.35f))
            .Box(new Vector3(0, 2.75f, 5.7f), new Vector3(3.8f, 0.1f, 1.4f), new Color(0.72f, 0.2f, 0.16f))
            .Box(new Vector3(0, 3.0f, 6.38f), new Vector3(3.8f, 0.4f, 0.04f), new Color(0.72f, 0.2f, 0.16f))
            // stock out the west side: a pole rack, cable drums, cabinets on pallets
            .Box(new Vector3(10.0f, 0.6f, -1f), new Vector3(0.2f, 1.2f, 0.2f), wood).Box(new Vector3(13.0f, 0.6f, -1f), new Vector3(0.2f, 1.2f, 0.2f), wood)
            .Box(new Vector3(10.0f, 0.6f, -6f), new Vector3(0.2f, 1.2f, 0.2f), wood).Box(new Vector3(13.0f, 0.6f, -6f), new Vector3(0.2f, 1.2f, 0.2f), wood);
        for (int i = 0; i < 6; i++)   // poles lying on the rack, a pyramid of six
        {
            int row = i < 3 ? 0 : i < 5 ? 1 : 2, k = i < 3 ? i : i < 5 ? i - 3 : 0;
            lp.Box(new Vector3(10.9f + k * 0.55f + row * 0.27f, 1.32f + row * 0.26f, -3.5f), new Vector3(0.26f, 0.26f, 7.4f), pole);
        }
        foreach (var (x, z, h) in new[] { (10.4f, 2.6f, 0f), (12.4f, 2.4f, 0f), (11.4f, 2.5f, 0.72f) })   // drums lying on their faces
            lp.Cylinder(new Vector3(x, h, z), 0.85f, 0.85f, 0.1f, 10, wood).Cylinder(new Vector3(x, h + 0.1f, z), 0.45f, 0.45f, 0.52f, 10, dark)
              .Cylinder(new Vector3(x, h + 0.62f, z), 0.85f, 0.85f, 0.1f, 10, wood);
        foreach (var x in new[] { 9.9f, 11.4f, 12.9f })   // street cabinets, still in their wrapping
            lp.Box(new Vector3(x, 0.08f, 5.4f), new Vector3(1.2f, 0.16f, 0.95f), wood).Box(new Vector3(x, 0.83f, 5.4f), new Vector3(0.95f, 1.35f, 0.5f), cab);
        solids.Add((new Vector3(0, 0.55f, 5.95f), new Vector3(2.8f, 1.1f, 0.5f)));      // the counter: you stand in front of it, Gorse behind
        solids.Add((new Vector3(11.5f, 0.9f, -3.5f), new Vector3(3.6f, 1.8f, 7.4f)));   // you walk round the stock, not through it
        solids.Add((new Vector3(11.4f, 0.75f, 2.5f), new Vector3(4.0f, 1.5f, 2.0f)));
        return lp.Build();
    }

    /// <summary>The Yard's loading bay (prop yard.bay): a raised concrete dock and the Yard's green flatbed lorry backed up to it.</summary>
    private static LowPoly YardBay()
    {
        var concrete = new Color(0.6f, 0.59f, 0.56f); var green = new Color(0.22f, 0.42f, 0.3f); var cream = new Color(0.9f, 0.87f, 0.76f);
        var dark = new Color(0.1f, 0.1f, 0.11f); var glass = new Color(0.3f, 0.42f, 0.52f);
        var lp = new LowPoly()
            .Box(new Vector3(1.2f, 0.55f, -1.5f), new Vector3(3.2f, 1.1f, 7f), concrete)                    // the dock
            .Box(new Vector3(-0.3f, 1.16f, -1.5f), new Vector3(0.2f, 0.12f, 7f), new Color(0.85f, 0.7f, 0.2f)) // its yellow edge
            .Box(new Vector3(-3.0f, 0.03f, 0f), new Vector3(5f, 0.06f, 12f), concrete * 0.92f)                  // apron
            // the lorry: cab at the street end, flatbed towards the dock
            .Box(new Vector3(-2.6f, 1.55f, 4.0f), new Vector3(2.3f, 2.1f, 1.9f), cream)
            .Box(new Vector3(-2.6f, 2.0f, 4.97f), new Vector3(2.0f, 0.8f, 0.04f), glass)
            .Box(new Vector3(-2.6f, 0.75f, 4.6f), new Vector3(2.35f, 0.5f, 1.0f), green)
            .Box(new Vector3(-2.6f, 1.05f, -0.4f), new Vector3(2.3f, 0.25f, 6.6f), green)                       // flatbed
            .Box(new Vector3(-2.6f, 1.6f, 2.95f), new Vector3(2.3f, 0.9f, 0.1f), green)                         // headboard
            .Box(new Vector3(-2.6f, 1.55f, -1.2f), new Vector3(1.3f, 0.75f, 1.1f), new Color(0.72f, 0.58f, 0.36f)); // a crate on the bed
        foreach (var (x, z) in new[] { (-3.65f, 4.0f), (-1.55f, 4.0f), (-3.65f, -1.6f), (-1.55f, -1.6f), (-3.65f, -2.9f), (-1.55f, -2.9f) })
            lp.Box(new Vector3(x, 0.45f, z), new Vector3(0.3f, 0.9f, 0.9f), dark);
        return lp;
    }

    private static Mesh DepotMesh() => new LowPoly()
        .Box(new Vector3(0, 1.6f, 0), new Vector3(8, 3.8f, 7), new Color(0.6f, 0.38f, 0.3f))
        .Box(new Vector3(0, 3.6f, 0), new Vector3(8.4f, 0.25f, 7.4f), new Color(0.28f, 0.28f, 0.3f))
        .Box(new Vector3(-1.9f, 1.3f, 3.53f), new Vector3(3.2f, 2.6f, 0.08f), new Color(0.45f, 0.5f, 0.52f))      // roller door
        .Box(new Vector3(0, 1.0f, 3.53f), new Vector3(1.0f, 2.0f, 0.1f), new Color(0.2f, 0.28f, 0.22f))           // door
        .Box(new Vector3(2.6f, 1.7f, 3.53f), new Vector3(1.4f, 1.0f, 0.06f), new Color(0.3f, 0.42f, 0.52f))
        .Box(new Vector3(0, 2.95f, 3.55f), new Vector3(5.0f, 0.55f, 0.06f), new Color(0.93f, 0.9f, 0.8f))          // sign board
        .Build();

    private static Mesh FarmMesh(List<(Vector3 Pos, Vector3 Size)> solids)
    {
        var red = new Color(0.58f, 0.2f, 0.16f);
        solids.Add((new Vector3(20, 2.6f, -6), new Vector3(11, 5.2f, 15)));   // the barn and silo are solid (you used to drive through them)
        solids.Add((new Vector3(29, 4.5f, -10), new Vector3(4.2f, 9f, 4.2f)));
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
