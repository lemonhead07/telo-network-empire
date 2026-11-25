using Godot;
using Telo.Sim.Build;
using Telo.Sim.Planet;
using Telo.Sim.Town;
using Telo.Sim.World;

namespace Telo.Game.World;

/// <summary>
/// The data centre on Halden Flats (DC2, DATA_CENTRE_DESIGN.md §2), grown from the Act 5 projects the sim says are built:
/// pegs, then the slab and the portacabin, a steel frame, the hall with five rooms you walk into (Planning, Meet-Me, the Hall,
/// Plant, the loading bay), then what each project puts in them. When the board is lit (DC9) the lights come on, the roof
/// has a view, and the early-access card is on the night-shift desk. Looks and walls only: the sim decides what is built.
/// Building-local metres: x along the front (east), z out of the front door (south), y up from the floor (= the ground).
/// Models by prop id (dc.*), each falling back to a labelled box.
/// </summary>
public partial class DataCentre : Node3D
{
    /// <summary>The building faces north, towards the road in from town: building-local x is site west, z site north.
    /// The Halden projects' east/south in projects.json are building-local (x, z) turned round: (-x, -z).</summary>
    public const float Yaw = 180;
    /// <summary>The hall's models stand on a 0.3 m slab: sunk so its floor is the ground you walk on.</summary>
    public const float Sink = 0.32f;
    public const float Top = 7f - Sink;           // underside of the roof
    public const float Roof = 7.2f - Sink;        // the roof deck you stand on
    public const float PartitionH = 3.2f;
    public const float InX = 15.7f, InZ = 11.7f;  // the inside faces of the outer walls

    public enum Room { Outside, Planning, Bay, MeetMe, Hall, Plant }

    /// <summary>The five rooms (x, z, width, depth).</summary>
    public static readonly (Room Room, string Name, Rect2 Area)[] Rooms =
    {
        (Room.Planning, "PLANNING ROOM", new Rect2(-InX, 4f, InX + 2f, InZ - 4f)),
        (Room.Bay, "LOADING BAY", new Rect2(2f, 4f, InX - 2f, InZ - 4f)),
        (Room.MeetMe, "MEET-ME ROOM", new Rect2(-InX, -InZ, InX - 7f, InZ + 4f)),
        (Room.Hall, "THE HALL", new Rect2(-7f, -InZ, 14f, InZ + 4f)),
        (Room.Plant, "PLANT ROOM", new Rect2(7f, -InZ, InX - 7f, InZ + 4f)),
    };

    /// <summary>Doorways: the rooms either side, the middle of the gap (x, z), whether you cross it along z (else along x), its width.</summary>
    public static readonly (Room A, Room B, Vector2 At, bool CrossZ, float Width)[] Doors =
    {
        (Room.Outside, Room.Planning, new(-6f, 11.85f), true, 1.0f),   // the people door (the model's)
        (Room.Outside, Room.Bay, new(9.75f, 11.85f), true, 5.0f),      // under the roller door, half up
        (Room.Planning, Room.MeetMe, new(-14.8f, 4f), true, 1.4f),
        (Room.Planning, Room.Hall, new(-3f, 4f), true, 1.4f),
        (Room.Bay, Room.Hall, new(4.5f, 4f), true, 3.0f),
        (Room.Bay, Room.Plant, new(11.2f, 4f), true, 2.0f),
        (Room.MeetMe, Room.Hall, new(-7f, -2.6f), false, 1.4f),
        (Room.Hall, Room.Plant, new(7f, -2.6f), false, 1.4f),
        (Room.Planning, Room.Bay, new(2f, 8.2f), false, 1.4f),
    };

    /// <summary>Where the van parks: in front, off to the left of the people door.</summary>
    public static readonly Vector3 ParkLocal = new(-22f, 0.3f, 16f);

    public Interactable LadderUp { get; }
    public Interactable LadderDown { get; }
    /// <summary>The early-access card on the night-shift desk (DC9): the screen goes on <see cref="CardAnchor"/>.</summary>
    public Interactable Card { get; }
    public Node3D CardAnchor { get; }
    private readonly Node3D _pegs, _slab, _frame, _hall, _inside, _plant, _cooling, _meetme, _core, _racks, _cage, _litOnly;
    private readonly List<Node3D> _cabin = new(), _columns = new(), _rafters = new();
    private readonly StaticBody3D _walls, _cabinBody;
    private readonly StandardMaterial3D _strip, _leds, _ledsAmber, _outside;
    private readonly List<(OmniLight3D Light, Room Room)> _lights = new();
    private MultiMeshInstance3D? _routes;
    private static readonly Color Plaster = new(0.86f, 0.85f, 0.8f), Tape = new(0.95f, 0.78f, 0.12f), Steel = new(0.42f, 0.47f, 0.52f);

    public DataCentre()
    {
        Name = "DataCentre";
        _strip = Glow(new Color(0.45f, 0.45f, 0.42f));
        _leds = Glow(new Color(0.35f, 1f, 0.45f));
        _ledsAmber = Glow(new Color(1f, 0.7f, 0.2f));
        _outside = Glow(new Color(1f, 0.85f, 0.55f));

        // 1. pegs and string while the land is bought and the shuttering goes down
        _pegs = Group("Pegs");
        var pegs = new LowPoly();
        for (int i = 0; i < 4; i++)
        {
            float x = i % 2 == 0 ? -17f : 17f, z = i < 2 ? -13f : 13f;
            pegs.Box(new Vector3(x, 0.35f, z), new Vector3(0.06f, 0.7f, 0.06f), new Color(0.55f, 0.4f, 0.22f));
        }
        foreach (var (a, b) in new[] { (new Vector3(-17, 0.6f, -13), new Vector3(17, 0.6f, -13)), (new Vector3(-17, 0.6f, 13), new Vector3(17, 0.6f, 13)) })
            pegs.Box((a + b) / 2, new Vector3(34f, 0.01f, 0.01f), new Color(0.95f, 0.3f, 0.6f));
        foreach (var x in new[] { -17f, 17f })
            pegs.Box(new Vector3(x, 0.6f, 0), new Vector3(0.01f, 0.01f, 26f), new Color(0.95f, 0.3f, 0.6f));
        _pegs.AddChild(pegs.Instance("PegsMesh"));

        // 2. the slab and the portacabin (the model's cabin parts come with the finished slab)
        _slab = Group("Slab");
        var slab = Props.Make("dc.slab-portacabin", () => new LowPoly().Box(new Vector3(0, 0.15f, 0), new Vector3(34, 0.3f, 26), new Color(0.6f, 0.6f, 0.58f))
            .Box(new Vector3(-11, 1.9f, 10), new Vector3(9.6f, 2.6f, 3f), new Color(0.88f, 0.86f, 0.78f)).Instance());
        slab.Position = new Vector3(0, -0.29f, 0);
        _slab.AddChild(slab);
        foreach (var prefix in new[] { "cabin", "block", "step", "cwin", "ac-box" }) _cabin.AddRange(Props.Parts(slab, prefix));
        _cabinBody = new StaticBody3D { Name = "CabinBody", CollisionLayer = 0 };   // solid only while it stands (Sync)
        Solid(_cabinBody, new Vector3(-11f, 1.6f, 10f), new Vector3(9.6f, 3.2f, 3f));
        _slab.AddChild(_cabinBody);

        // 3. the steel frame, going up while the hall is raised
        _frame = Group("Frame");
        for (float x = -16; x <= 16.01f; x += 4)
            foreach (var z in new[] { -12f, 12f }) _columns.Add(Column(x, z));
        for (float z = -8; z <= 8.01f; z += 4)
            foreach (var x in new[] { -16f, 16f }) _columns.Add(Column(x, z));
        for (float x = -16; x <= 16.01f; x += 4)
        {
            var r = new LowPoly().Box(new Vector3(x, Top - 0.2f, 0), new Vector3(0.2f, 0.4f, 24f), Steel).Instance("Rafter");
            _frame.AddChild(r); _rafters.Add(r);
        }

        // 4. the hall: the shell model, its walls and roof (solid), five rooms inside
        _hall = Group("Hall");
        var shell = Props.Make("dc.hall-shell", () => new LowPoly().Box(new Vector3(0, 3.5f, -11.85f), new Vector3(32, 7, 0.3f), new Color(0.55f, 0.58f, 0.6f))
            .Box(new Vector3(0, 7.1f, 0), new Vector3(32, 0.2f, 24), new Color(0.45f, 0.47f, 0.5f)).Instance());
        shell.Position = new Vector3(0, -Sink, 0);
        _hall.AddChild(shell);
        foreach (var door in Props.Parts(shell, "door")) if (door.Name == "door") door.Visible = false;   // the people door stands open
        _walls = new StaticBody3D { Name = "Walls", CollisionLayer = 0 };   // solid once the hall is built (Sync)
        _hall.AddChild(_walls);
        BuildShellWalls();
        _inside = Group("Inside", _hall);
        BuildPartitions();
        BuildLights();
        BuildPlanningRoom();
        BuildLoadingBay();

        // what each project puts in its room
        _plant = Group("Plant", _hall);
        Place(_plant, "dc.generator", new Vector3(11.4f, 0, -9.6f));
        Place(_plant, "dc.ups-batteries", new Vector3(15.2f, 0, -5.2f), -90);
        Place(_plant, "dc.ups-batteries", new Vector3(15.2f, 0, -3.3f), -90);
        Place(_plant, "dc.load-dial", new Vector3(7.16f, 1.7f, -7f), 90);
        var feed = new LowPoly().Box(new Vector3(15.3f, 1.1f, -9.4f), new Vector3(0.5f, 2.2f, 1.2f), new Color(0.3f, 0.33f, 0.36f))
            .Box(new Vector3(15.04f, 1.6f, -9.4f), new Vector3(0.02f, 0.3f, 0.5f), new Color(0.95f, 0.8f, 0.1f));
        _plant.AddChild(feed.Instance("GridFeed"));
        Sign(_plant, "GRID FEED  ·  A + B", new Vector3(15.0f, 2.5f, -9.4f), -90, 0.003f);

        _cooling = Group("Cooling", _hall);
        foreach (var x in new[] { -4.5f, 0f, 4.5f }) Place(_cooling, "dc.crac-unit", new Vector3(x, 0, -11.1f));
        var floor = new LowPoly().Box(new Vector3(0, 0.015f, -3.2f), new Vector3(13.8f, 0.03f, 14.6f), new Color(0.62f, 0.64f, 0.66f));
        for (float x = -6.3f; x < 7f; x += 0.6f) floor.Box(new Vector3(x, 0.031f, -3.2f), new Vector3(0.015f, 0.002f, 14.6f), new Color(0.5f, 0.52f, 0.55f));
        foreach (var z in new[] { -5.7f, -2.3f }) floor.Box(new Vector3(0, 0.033f, z), new Vector3(11f, 0.003f, 0.08f), Tape);
        _cooling.AddChild(floor.Instance("RaisedFloor"));
        Sign(_cooling, "COLD AISLE", new Vector3(0, 0.04f, -4f), 0, 0.006f, flat: true);

        _meetme = Group("MeetMe", _hall);
        Place(_meetme, "dc.odf-wall", new Vector3(-15.46f, 0, -8.5f), 90);
        Place(_meetme, "dc.odf-wall", new Vector3(-15.46f, 0, -6.0f), 90);
        var ducts = new LowPoly();
        for (int i = 0; i < 4; i++)
        {
            var at = new Vector3(-13.6f, 0, -10.6f + i * 1.3f);
            ducts.Box(at + new Vector3(0, 0.03f, 0), new Vector3(0.7f, 0.06f, 0.7f), new Color(0.12f, 0.12f, 0.13f));
            for (int c = 0; c < 3; c++)
            {
                var col = c == 0 ? new Color(0.95f, 0.85f, 0.15f) : c == 1 ? new Color(0.2f, 0.4f, 0.9f) : new Color(0.95f, 0.85f, 0.15f);
                ducts.Box(at + new Vector3(-0.2f + c * 0.2f, 0.4f, 0), new Vector3(0.04f, 0.8f, 0.04f), col);
                ducts.Box(at + new Vector3(-0.2f + c * 0.2f + 0.04f, 0.62f, 0), new Vector3(0.05f, 0.07f, 0.005f), new Color(0.85f, 0.72f, 0.5f));   // luggage tag
            }
        }
        _meetme.AddChild(ducts.Instance("Ducts"));

        _core = Group("Core", _hall);
        Place(_core, "dc.rack-42u", new Vector3(0, 0, -6.2f));
        Leds(_core, new Vector3(0, 0, -6.2f), true, 14, _leds);
        Sign(_core, "CORE", new Vector3(0, 2.4f, -5.69f), 0, 0.004f);

        _racks = Group("Racks", _hall);    // migration night: every hosting customer's servers, in a row facing the core across the cold aisle
        for (int i = 0; i < 7; i++)
        {
            var at = new Vector3(-1.8f + i * 0.61f, 0, -1.8f);
            Place(_racks, "dc.rack-42u", at, 180);
            Leds(_racks, at, false, 6 + i % 3 * 3, i % 2 == 0 ? _leds : _ledsAmber);
        }
        Sign(_racks, "CUSTOMERS  ·  from the cold store", new Vector3(0, 2.4f, -2.29f), 180, 0.004f);

        _cage = Group("Cage", _hall);      // NezTel rents a cage in your Meet-Me Room (optional peering)
        Place(_cage, "dc.cage", new Vector3(-11f, 0, 1.8f), 180);
        Sign(_cage, "NEZTEL  ·  AUTHORISED PERSONNEL", new Vector3(-11f, 2.1f, 0.2f), 180, 0.003f);

        // DC9: lit only once the board is lit
        _litOnly = Group("Lit", _hall);
        var beacon = new LowPoly().Gem(new Vector3(-14f, Roof + 1.2f, -10f), 0.25f, 0.25f, 0.1f, 6, Colors.White).Instance("Beacon");
        beacon.MaterialOverride = Glow(new Color(1f, 0.25f, 0.2f), 4f);
        _litOnly.AddChild(beacon);
        foreach (var (x, y) in new[] { (-6f, 2.6f), (9.75f, 5.6f), (-13f, 5.6f), (3f, 5.6f) })
        {
            var lamp = new LowPoly().Box(new Vector3(x, y, 12.15f), new Vector3(0.5f, 0.12f, 0.3f), Colors.White).Instance("Flood");
            lamp.MaterialOverride = _outside;
            _litOnly.AddChild(lamp);
            var l = new OmniLight3D { Position = new Vector3(x, y - 0.4f, 13.2f), LightColor = new Color(1f, 0.85f, 0.6f), LightEnergy = 2.2f, OmniRange = 14f };
            _litOnly.AddChild(l);
        }
        _litOnly.AddChild(new OmniLight3D { Position = new Vector3(-14f, Roof + 1.6f, -10f), LightColor = new Color(1f, 0.3f, 0.25f), LightEnergy = 3f, OmniRange = 10f });

        // the roof: the ladder up the east side, and back down
        LadderUp = Spot(new Vector3(16.6f, 1.2f, 6f), new Vector3(0.9f, 2.4f, 1.2f));
        LadderDown = Spot(new Vector3(15.3f, Roof + 0.8f, 6f), new Vector3(0.8f, 1.4f, 1.2f));
        _hall.AddChild(LadderUp); _hall.AddChild(LadderDown);

        // the card on the night-shift desk (the desk stands in the Planning Room)
        // propped against the biscuit tin, leaning back
        CardAnchor = new Node3D { Name = "CardAnchor", Position = new Vector3(-11f, 0.91f, 7.05f), RotationDegrees = new Vector3(-18, 0, 0) };
        _litOnly.AddChild(CardAnchor);
        Card = Spot(new Vector3(-11f, 0.9f, 7.12f), new Vector3(0.5f, 0.35f, 0.3f));
        _litOnly.AddChild(Card);
    }

    /// <summary>Show what the sim says is built (and what is under way).</summary>
    public void Sync(GameState s)
    {
        bool Done(string id) => ProjectRules.Done(s, id);
        bool Started(string id) => ProjectRules.Started(s, id) || Done(id);
        int StepOf(string id) => ProjectRules.ProgressOf(s, id).Step;

        bool slabDone = Done("proj-dc-slab"), shellDone = Done("proj-dc-shell"), lit = Done("proj-dc-lit");
        _pegs.Visible = Started("proj-dc-slab") && !slabDone && StepOf("proj-dc-slab") < 2;
        _slab.Visible = !shellDone && (slabDone || Started("proj-dc-slab") && StepOf("proj-dc-slab") >= 2);
        foreach (var c in _cabin) c.Visible = slabDone;
        _cabinBody.CollisionLayer = _slab.Visible && slabDone ? 1u : 0u;

        // the frame grows with the shell's steps: columns first, then the rafters
        bool raising = Started("proj-dc-shell") && !shellDone;
        _frame.Visible = raising;
        if (raising)
        {
            var prog = ProjectRules.ProgressOf(s, "proj-dc-shell");
            float f = Mathf.Clamp((prog.Step + prog.Click * 0.25f) / 3f, 0.08f, 1f);
            foreach (var c in _columns) { c.Scale = new Vector3(1, Mathf.Clamp(f * 2f, 0.1f, 1f), 1); }
            foreach (var r in _rafters) r.Visible = f >= 0.5f;
        }

        _hall.Visible = shellDone;
        _walls.CollisionLayer = shellDone ? 1u : 0u;
        LadderUp.CollisionLayer = LadderDown.CollisionLayer = shellDone ? 2u : 0u;
        _plant.Visible = Done("proj-dc-power");
        _cooling.Visible = Done("proj-dc-cooling");
        _meetme.Visible = Done("proj-dc-meetme");
        _core.Visible = Done("proj-dc-core");
        _racks.Visible = Done("proj-migration");
        _cage.Visible = Done("proj-peering");
        _litOnly.Visible = lit;
        Card.CollisionLayer = lit ? 2u : 0u;

        // light: work lamps while you build, the full lights once the board is lit
        _strip.AlbedoColor = lit ? new Color(0.95f, 0.97f, 1f) : new Color(1f, 0.88f, 0.7f);
        _strip.Emission = _strip.AlbedoColor;
        _strip.EmissionEnergyMultiplier = lit ? 2.2f : 0.6f;
        foreach (var (l, room) in _lights)
        {
            l.LightColor = lit && room != Room.Planning ? new Color(0.88f, 0.94f, 1f) : new Color(1f, 0.86f, 0.68f);
            l.LightEnergy = lit ? 1.5f : 0.75f;
        }
        _leds.EmissionEnergyMultiplier = _ledsAmber.EmissionEnergyMultiplier = lit ? 3f : 1.2f;
        if (_routes is not null) _routes.Visible = lit && Clock.Dark(s);
    }

    /// <summary>
    /// DC9: the cable routes home, as a row of small lights across the land from the hall to each place brought home
    /// (DATA_CENTRE_DESIGN §7: "your cable routes glow faintly"). Shown only at night once the board is lit. Call once in the tree.
    /// </summary>
    public void BuildRoutes(PlanetGen gen, IEnumerable<Vector3> ends)
    {
        var points = new List<Vector3>();
        var a = GlobalPosition.Normalized();
        float r = (float)gen.RadiusM;
        foreach (var end in ends)
        {
            var b = end.Normalized();
            float len = a.AngleTo(b) * r;
            int n = Mathf.Max(2, (int)(len / 11f));
            for (int i = 1; i <= n; i++)
            {
                if (i * len / n < 24f) continue;   // starts outside the building
                var d = a.Slerp(b, i / (float)n).Normalized();
                float h = Mathf.Max((float)gen.Height(PlanetBuilder.ToSim(d)), 0f);
                points.Add(d * (r + h + 1.3f));
            }
        }
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new BoxMesh { Size = new Vector3(0.3f, 0.3f, 0.3f) }, InstanceCount = points.Count };
        for (int i = 0; i < points.Count; i++) mm.SetInstanceTransform(i, new Transform3D(Basis.Identity, ToLocal(points[i])));
        _routes = new MultiMeshInstance3D { Name = "Routes", Multimesh = mm, MaterialOverride = Glow(new Color(1f, 0.78f, 0.35f), 4f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
        AddChild(_routes);
    }

    /// <summary>The room a point is in (Outside when outside the hall or before it is built).</summary>
    public Room RoomAt(Vector3 global)
    {
        if (!_hall.Visible) return Room.Outside;
        var p = ToLocal(global);
        if (p.Y > Top) return Room.Outside;
        foreach (var (room, _, area) in Rooms)
            if (area.HasPoint(new Vector2(p.X, p.Z))) return room;
        return Room.Outside;
    }

    /// <summary>Waypoints (global) through the doorways from one point to another (empty when they're in the same room).</summary>
    public List<Vector3> Route(Vector3 from, Vector3 to)
    {
        var path = new List<Vector3>();
        Room a = RoomAt(from), b = RoomAt(to);
        if (a == b) return path;
        // breadth first over the rooms
        var prev = new Dictionary<Room, (Room From, int Door)> { [a] = (a, -1) };
        var queue = new Queue<Room>(); queue.Enqueue(a);
        while (queue.Count > 0 && !prev.ContainsKey(b))
        {
            var r = queue.Dequeue();
            for (int i = 0; i < Doors.Length; i++)
            {
                var d = Doors[i];
                var next = d.A == r ? d.B : d.B == r ? d.A : (Room?)null;
                if (next is { } n && !prev.ContainsKey(n)) { prev[n] = (r, i); queue.Enqueue(n); }
            }
        }
        if (!prev.ContainsKey(b)) return path;
        var doors = new List<(int Door, Room From)>();
        for (var r = b; r != a; r = prev[r].From) doors.Add((prev[r].Door, prev[r].From));
        doors.Reverse();
        foreach (var (i, fromRoom) in doors)
        {
            var d = Doors[i];
            var c = new Vector3(d.At.X, 0.2f, d.At.Y);
            var across = d.CrossZ ? new Vector3(0, 0, 1) : new Vector3(1, 0, 0);
            // which side the room we're leaving is on
            var probe = c - across * 1.2f;
            bool fromIsMinus = fromRoom == Room.Outside ? !InsideXZ(probe) : RoomOf(probe) == fromRoom;
            var step = fromIsMinus ? across : -across;
            path.Add(ToGlobal(c - step * 1.1f));
            path.Add(ToGlobal(c + step * 1.1f));
        }
        return path;
    }

    /// <summary>On the building's plot (with its car park), once the hall stands.</summary>
    public bool Near(Vector3 global)
    {
        if (!_hall.Visible) return false;
        var p = ToLocal(global);
        return Mathf.Abs(p.X) < 28f && Mathf.Abs(p.Z) < 26f && p.Y < Roof + 3f;
    }

    private static bool InsideXZ(Vector3 p) => Mathf.Abs(p.X) < InX && Mathf.Abs(p.Z) < InZ;

    private static Room RoomOf(Vector3 local)
    {
        foreach (var (room, _, area) in Rooms)
            if (area.HasPoint(new Vector2(local.X, local.Z))) return room;
        return Room.Outside;
    }

    // ------------------------------------------------------------------------------------------------ building it

    private void BuildShellWalls()
    {
        float h = Top + 0.2f, t = 0.3f, zf = 11.85f;
        Solid(_walls, new Vector3(0, h / 2, -zf), new Vector3(32f, h, t));
        foreach (var sx in new[] { -1f, 1f }) Solid(_walls, new Vector3(sx * 15.85f, h / 2, 0), new Vector3(t, h, 24f));
        void Front(float x0, float x1, float y0 = 0) => Solid(_walls, new Vector3((x0 + x1) / 2, (y0 + h) / 2, zf), new Vector3(x1 - x0, h - y0, t));
        Front(-16f, -6.5f); Front(-6.5f, -5.5f, 2.3f - Sink); Front(-5.5f, 7.25f); Front(7.25f, 12.25f, 2.5f - Sink); Front(12.25f, 16f);
        // the roof deck, its parapet and the fan housings: you can stand up there
        Solid(_walls, new Vector3(0, Roof - 0.1f, 0), new Vector3(32f, 0.2f, 24f));
        foreach (var s in new[] { -1f, 1f })
        {
            Solid(_walls, new Vector3(0, Roof + 0.35f, s * 11.9f), new Vector3(32f, 0.7f, 0.2f));
            Solid(_walls, new Vector3(s * 15.9f, Roof + 0.35f, 0), new Vector3(0.2f, 0.7f, 24f));
        }
        foreach (var x in new[] { -8f, -2f, 4f, 10f }) Solid(_walls, new Vector3(x, Roof + 0.5f, -4f), new Vector3(2.6f, 1.0f, 2.6f));
    }

    private void BuildPartitions()
    {
        var mesh = new LowPoly();
        void Wall(bool alongX, float at, float from, float to, params (float A, float B)[] gaps)
        {
            float cur = from;
            foreach (var (ga, gb) in gaps.OrderBy(g => g.A).Append((to, to)))
            {
                if (ga - cur > 0.01f)
                {
                    float mid = (cur + ga) / 2, len = ga - cur;
                    var c = alongX ? new Vector3(mid, PartitionH / 2, at) : new Vector3(at, PartitionH / 2, mid);
                    var size = alongX ? new Vector3(len, PartitionH, 0.2f) : new Vector3(0.2f, PartitionH, len);
                    mesh.Box(c, size, Plaster);
                    Solid(_walls, c, size);
                }
                // the lintel over each doorway
                if (gb > ga && gb < to)
                {
                    float mid = (ga + gb) / 2, len = gb - ga;
                    var c = alongX ? new Vector3(mid, 2.75f, at) : new Vector3(at, 2.75f, mid);
                    mesh.Box(c, alongX ? new Vector3(len, 0.9f, 0.2f) : new Vector3(0.2f, 0.9f, len), Plaster);
                }
                cur = Mathf.Max(cur, gb);
            }
        }
        (float, float) Gap(Room a, Room b)
        {
            var d = Doors.First(x => x.A == a && x.B == b);
            float c = d.CrossZ ? d.At.X : d.At.Y;
            return (c - d.Width / 2, c + d.Width / 2);
        }
        Wall(true, 4f, -InX, InX, Gap(Room.Planning, Room.MeetMe), Gap(Room.Planning, Room.Hall), Gap(Room.Bay, Room.Hall), Gap(Room.Bay, Room.Plant));
        Wall(false, -7f, -InZ, 4f, Gap(Room.MeetMe, Room.Hall));
        Wall(false, 7f, -InZ, 4f, Gap(Room.Hall, Room.Plant));
        Wall(false, 2f, 4f, InZ, Gap(Room.Planning, Room.Bay));
        _inside.AddChild(mesh.Instance("Partitions"));

        // a name over every doorway, on each side: the room you're walking into
        string NameOf(Room r) => r == Room.Outside ? "WAY OUT" : Rooms.First(x => x.Room == r).Name;
        foreach (var d in Doors)
        {
            if (d.A == Room.Outside) continue;
            var c = new Vector3(d.At.X, 2.75f, d.At.Y);
            var n = d.CrossZ ? new Vector3(0, 0, 1) : new Vector3(1, 0, 0);
            foreach (var side in new[] { -1f, 1f })
            {
                var at = c + n * side * 0.12f;
                var into = RoomOf(c - n * side * 1.0f);     // the room on the far side from this face
                float yaw = d.CrossZ ? (side > 0 ? 0 : 180) : (side > 0 ? 90 : -90);
                Sign(_inside, NameOf(into), at, yaw, 0.004f);
            }
        }
        // and the name painted on the outside by the door
        Sign(_inside, "RECEPTION  ·  PLEASE WIPE YOUR FEET", new Vector3(-6f, 2.55f, 12.03f), 0, 0.0035f);
    }

    private void BuildLights()
    {
        var mesh = new LowPoly();
        foreach (var (room, _, area) in Rooms)
        {
            var c = area.GetCenter();
            // strip lights hung under the roof, a row down each room
            for (float x = area.Position.X + 1.5f; x < area.End.X - 1f; x += 3f)
                mesh.Box(new Vector3(x, PartitionH + 0.35f, c.Y), new Vector3(0.18f, 0.06f, Mathf.Min(area.Size.Y - 2f, 10f)), Colors.White);
            float range = Mathf.Max(area.Size.X, area.Size.Y) * 0.8f;
            foreach (var z in area.Size.Y > 10 ? new[] { c.Y - area.Size.Y / 4, c.Y + area.Size.Y / 4 } : new[] { c.Y })
            {
                var l = new OmniLight3D { Position = new Vector3(c.X, PartitionH - 0.3f, z), OmniRange = range, OmniAttenuation = 0.8f };
                _inside.AddChild(l); _lights.Add((l, room));
            }
        }
        var strips = mesh.Instance("StripLights");
        strips.MaterialOverride = _strip;
        strips.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _inside.AddChild(strips);
    }

    private void BuildPlanningRoom()
    {
        Place(_inside, "dc.pinboard", new Vector3(-11f, 1.55f, 4.1f));
        Place(_inside, "dc.clip-rail", new Vector3(-7.4f, 1.45f, 4.11f));
        Place(_inside, "dc.noc-desk", new Vector3(-11f, 0, 6.9f));
        // a plan chest and a kettle on it: an office, not yet a control room
        var chest = new LowPoly().Box(new Vector3(-0.2f, 0.45f, 10.9f), new Vector3(1.6f, 0.9f, 0.9f), new Color(0.35f, 0.42f, 0.36f))
            .Box(new Vector3(0.2f, 1.0f, 10.9f), new Vector3(0.2f, 0.2f, 0.15f), new Color(0.85f, 0.85f, 0.82f));
        _inside.AddChild(chest.Instance("PlanChest"));
    }

    private void BuildLoadingBay()
    {
        Place(_inside, "dc.trolley", new Vector3(4f, 0, 6f), 30);
        var pallets = new LowPoly();
        foreach (var (x, z, n) in new[] { (13.5f, 6.0f, 2), (13.5f, 8.4f, 1), (5.0f, 9.5f, 3) })
        {
            pallets.Box(new Vector3(x, 0.07f, z), new Vector3(1.2f, 0.14f, 1.0f), new Color(0.66f, 0.52f, 0.32f));
            for (int i = 0; i < n; i++)
                pallets.Box(new Vector3(x + (i % 2) * 0.1f - 0.05f, 0.14f + 0.3f + i * 0.6f, z), new Vector3(1.0f, 0.6f, 0.8f), new Color(0.74f, 0.6f, 0.38f));
            pallets.Box(new Vector3(x, 0.6f, z + 0.41f), new Vector3(0.21f, 0.3f, 0.005f), Colors.White);   // the delivery note
        }
        _inside.AddChild(pallets.Instance("Pallets"));
    }

    // ------------------------------------------------------------------------------------------------ helpers

    /// <summary>A part of the building that Sync shows or hides (top-level parts start hidden).</summary>
    private Node3D Group(string name, Node3D? parent = null)
    {
        var g = new Node3D { Name = name, Visible = parent is not null };
        (parent ?? this).AddChild(g);
        return g;
    }

    private Node3D Column(float x, float z)
    {
        var c = new LowPoly().Box(new Vector3(0, Top / 2, 0), new Vector3(0.3f, Top, 0.3f), Steel).Instance("Column");
        var holder = new Node3D { Name = "ColumnAt", Position = new Vector3(x, 0, z) };
        holder.AddChild(c);
        _frame.AddChild(holder);
        return holder;
    }

    private static void Place(Node3D parent, string prop, Vector3 at, float yawDeg = 0)
    {
        var p = Props.Make(prop);
        p.Position = at; p.RotationDegrees = new Vector3(0, yawDeg, 0);
        parent.AddChild(p);
    }

    /// <summary>Small lamps down the front of a rack (front +Z, or -Z when turned round).</summary>
    private static void Leds(Node3D parent, Vector3 rack, bool facesPlusZ, int count, StandardMaterial3D mat)
    {
        var m = new LowPoly();
        float z = rack.Z + (facesPlusZ ? 0.52f : -0.52f);
        for (int i = 0; i < count; i++)
            m.Box(new Vector3(rack.X + (i % 2 == 0 ? -0.17f : 0.17f), 0.35f + i * 0.11f, z), new Vector3(0.035f, 0.02f, 0.01f), Colors.White);
        var mi = m.Instance("Leds");
        mi.MaterialOverride = mat;
        mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        parent.AddChild(mi);
    }

    private static void Sign(Node3D parent, string text, Vector3 at, float yawDeg, float pixel, bool flat = false) =>
        parent.AddChild(new Label3D
        {
            Text = text, Position = at, RotationDegrees = flat ? new Vector3(-90, yawDeg, 0) : new Vector3(0, yawDeg, 0), PixelSize = pixel, FontSize = 64,
            DoubleSided = false, Modulate = flat ? new Color(0.95f, 0.8f, 0.2f) : new Color(0.12f, 0.16f, 0.3f), OutlineSize = flat ? 0 : 8,
            OutlineModulate = new Color(0.96f, 0.95f, 0.9f),
        });

    private static void Solid(StaticBody3D body, Vector3 center, Vector3 size) =>
        body.AddChild(new CollisionShape3D { Position = center, Shape = new BoxShape3D { Size = size } });

    private static Interactable Spot(Vector3 at, Vector3 size)
    {
        var spot = new Interactable { Position = at, CollisionLayer = 0, CollisionMask = 0 };
        spot.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        return spot;
    }

    private static StandardMaterial3D Glow(Color c, float energy = 1f) => new()
    {
        AlbedoColor = c, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = energy,
    };
}
