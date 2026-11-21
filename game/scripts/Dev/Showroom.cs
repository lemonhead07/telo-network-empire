using Godot;
using Telo.Game.World;

namespace Telo.Game.Dev;

/// <summary>
/// Every model in game/assets/models on a lawn, under the game's light: project sites along the back, the things you carry and
/// fit in front. A dev tool for checking models in the real renderer (D15).
///   godot --path game res://scenes/Showroom.tscn                          (look around: orbit with the mouse, scroll to zoom)
///   godot --path game res://scenes/Showroom.tscn -- --shot=/abs/path/x.png (saves x-dc, x-dc-rack, x-ops, x-sites-a, x-sites-b, x-kit1-3, then quits)
/// </summary>
public partial class Showroom : Node3D
{
    private Camera3D _cam = null!;
    private float _yaw = 0.3f, _pitch = -0.35f, _dist = 22f;
    private Vector3 _focus = new(0, 2, 0);

    public override async void _Ready()
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = new ProceduralSkyMaterial { SkyTopColor = new Color(0.45f, 0.65f, 1.3f), GroundBottomColor = new Color(0.3f, 0.35f, 0.25f) } },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky, TonemapMode = Godot.Environment.ToneMapper.Filmic,
        };
        AddChild(new WorldEnvironment { Environment = env });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, -35, 0), ShadowEnabled = true, LightEnergy = 1.1f });
        AddChild(new LowPoly().Box(new Vector3(0, -0.05f, 0), new Vector3(120, 0.1f, 60), new Color(0.48f, 0.62f, 0.34f)).Instance("Lawn"));

        var ids = DirAccess.GetFilesAt("res://assets/models").Where(f => f.EndsWith(".glb")).Select(f => f[..^4]).OrderBy(f => f).ToList();
        // back row: project sites, side by side; front row: everything small, on a bench
        float x = 0;
        var sites = ids.Where(i => i.StartsWith("site.")).ToList();
        var placed = new List<(string Id, Vector3 At, Aabb Box)>();
        foreach (var id in sites)
        {
            var n = Props.Make(id); AddChild(n);
            var b = Props.Bounds(n);
            n.Position = new Vector3(x - b.Position.X, 0, -b.End.Z);
            placed.Add((id, n.Position, b));
            Label(id, n.Position + new Vector3(b.GetCenter().X, 0.05f, b.End.Z + 0.6f), 0.007f);
            x += b.Size.X + 2.5f;
        }
        float half = x / 2;
        foreach (var c in GetChildren().OfType<Node3D>().Where(c => sites.Contains(c.Name.ToString().Replace('_', '.')))) c.Position -= new Vector3(half, 0, 0);
        foreach (var l in GetChildren().OfType<Label3D>()) l.Position -= new Vector3(half, 0, 0);

        var small = ids.Where(i => !i.StartsWith("site.") && i != "vehicle.van" && i != "world.line-pole").ToList();
        var bench = new LowPoly().Box(new Vector3(0, 0.4f, 6), new Vector3(small.Count * 1.3f + 1, 0.8f, 1.4f), new Color(0.55f, 0.42f, 0.28f)).Instance("Bench");
        AddChild(bench);
        float sx = -small.Count * 1.3f / 2 + 0.45f;
        foreach (var id in small)
        {
            var n = Props.Make(id); AddChild(n);
            var b = Props.Bounds(n);
            float fit = Mathf.Min(1f, 0.8f / Mathf.Max(b.Size.X, Mathf.Max(b.Size.Y, b.Size.Z)));   // the ladder and pole lie down small
            n.Scale = Vector3.One * fit;
            n.Position = new Vector3(sx - b.GetCenter().X * fit, 0.8f - b.Position.Y * fit, 6 - b.GetCenter().Z * fit);
            Label(id, new Vector3(sx, 0.81f, 6.55f), 0.0016f, flat: true);
            sx += 1.3f;
        }
        var van = Props.Make("vehicle.van"); AddChild(van); van.Position = new Vector3(-small.Count * 0.65f - 4, 0, 6); van.RotationDegrees = new Vector3(0, -60, 0);
        var pole = new MeshInstance3D { Mesh = Props.MeshOf("world.line-pole") }; AddChild(pole); pole.Position = new Vector3(small.Count * 0.65f + 3, 0, 5);

        Vignettes();

        _cam = new Camera3D { Fov = 55, Far = 500, Current = true };
        AddChild(_cam);
        Orbit();

        var args = OS.GetCmdlineUserArgs();
        if (args.FirstOrDefault(a => a.StartsWith("--shot=")) is { } shot)
        {
            string path = shot["--shot=".Length..];
            async Task Shot(string suffix, Vector3 from, Vector3 at)
            {
                _cam.LookAtFromPosition(from, at, Vector3.Up);
                for (int i = 0; i < 12; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                GetViewport().GetTexture().GetImage().SavePng(path.Replace(".png", suffix + ".png"));
            }
            for (int i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await Shot("-dc", new Vector3(3.2f, 2.0f, 23.5f), new Vector3(-0.6f, 1.1f, 17.5f));
            await Shot("-dc-rack", new Vector3(-0.45f, 1.45f, 19.3f), new Vector3(0f, 1.25f, 17.4f));
            await Shot("-phones", new Vector3(17.1f, 1.15f, 20.25f), new Vector3(17.35f, 0.8f, 19.5f));
            await Shot("-ops", new Vector3(14f, 2.2f, 24f), new Vector3(14f, 1.3f, 17f));
            await Shot("-sites-a", new Vector3(-half * 0.5f, 7, 16), new Vector3(-half * 0.5f, 2.5f, -2));
            await Shot("-sites-b", new Vector3(half * 0.5f, 7, 16), new Vector3(half * 0.5f, 2.5f, -2));
            float bl = small.Count * 0.65f;
            for (int i = 0; i < 3; i++)
            {
                float cx = -bl + bl * (2 * i + 1) / 3f;
                await Shot($"-kit{i + 1}", new Vector3(cx, 2.3f, 9.6f), new Vector3(cx, 0.85f, 6));
            }
            GetTree().Quit();
        }
    }

    /// <summary>Kit put together as it will stand in the game: a corner of the Data Centre (a dressed 42U rack, the trolley,
    /// Dana, the pin board, the night-shift desk, the load dial, a cooling unit) and an operations room of the later ages.</summary>
    private void Vignettes()
    {
        Node3D Put(string id, Vector3 at, float yawDeg = 0)
        {
            var n = Props.Make(id); AddChild(n);
            n.Position = at; n.RotationDegrees = new Vector3(0, yawDeg, 0);
            return n;
        }
        // a floor of tiles under the corner
        AddChild(new LowPoly().Box(new Vector3(0, 0.01f, 18.5f), new Vector3(9, 0.02f, 7), new Color(0.78f, 0.78f, 0.76f)).Instance("DcFloor"));
        // the rack, dressed from the top: two patch panels, a fibre tray, two routers, two switches; PDUs up the back
        var rack = Put("dc.rack-42u", new Vector3(0, 0, 17.4f));
        const float U = 0.04445f, firstU = 0.1f, railZ = 0.415f;
        foreach (var (id, u) in new[] { ("dc.box-patch", 40), ("dc.box-patch", 39), ("dc.box-fibretray", 37), ("dc.box-router", 34), ("dc.box-router", 31), ("dc.box-switch", 28), ("dc.box-switch", 27) })
        {
            var box = Props.Make(id); rack.AddChild(box);
            box.Position = new Vector3(0, firstU + u * U, railZ);
        }
        foreach (var sx in new[] { -1, 1 })
        {
            var pdu = Props.Make("dc.pdu"); rack.AddChild(pdu);
            pdu.Position = new Vector3(sx * 0.26f, 0.12f, -0.38f); pdu.RotationDegrees = new Vector3(0, -sx * 90, 0);
        }
        // a few leads from the patch panels down to the routers, as the game will draw them
        var lead = new StandardMaterial3D { AlbedoColor = new Color(0.22f, 0.45f, 0.85f) };
        var fibre = new StandardMaterial3D { AlbedoColor = new Color(0.98f, 0.84f, 0.15f) };
        for (int i = 0; i < 5; i++)
        {
            float x = -0.2f + i * 0.0174f * 3;
            var a = new Vector3(x, firstU + 39 * U + 0.016f, railZ + 0.02f);
            var b = new Vector3(-0.185f + i * 0.017f, firstU + 34 * U + 0.034f, railZ + 0.02f);
            var bow = new Vector3(0, 0, 0.09f);
            var curve = new Curve3D(); curve.AddPoint(a, Vector3.Zero, bow); curve.AddPoint(b, bow, Vector3.Zero);
            var path = new Path3D { Curve = curve }; rack.AddChild(path);
            path.AddChild(new CsgPolygon3D { Mode = CsgPolygon3D.ModeEnum.Path, PathNode = path.GetPath(), Polygon = Circle(0.003f), Material = i == 2 ? fibre : lead, PathRotation = CsgPolygon3D.PathRotationEnum.PathFollow });
        }
        Put("dc.trolley", new Vector3(0.95f, 0, 17.9f), -10);
        Put("person.dana", new Vector3(1.0f, 0, 18.9f), -35);
        Put("dc.crac-unit", new Vector3(-1.6f, 0, 16.6f));
        var board = Put("dc.pinboard", new Vector3(-3.2f, 1.5f, 16.2f), 20);
        AddChild(new LowPoly().Box(new Vector3(-3.2f, 0.75f, 16.05f), new Vector3(0.08f, 1.5f, 0.08f), new Color(0.4f, 0.3f, 0.2f)).Instance("BoardStand"));
        Put("dc.clip-rail", new Vector3(-3.25f, 2.55f, 16.2f), 20);
        var noc = Put("dc.noc-desk", new Vector3(-2.6f, 0, 19.6f), 30);
        ScreenFaces.Light(noc, "screen-", new Vector2(0.336f, 0.252f), new Vector2I(480, 360), i => new ScreenDemo { Kind = ScreenDemo.Look.Terminal, Seed = i });
        Put("person.noc-a", new Vector3(-2.1f, 0, 20.7f), 210);
        var dial = Put("dc.load-dial", new Vector3(2.2f, 1.6f, 16.2f));
        AddChild(new LowPoly().Box(new Vector3(2.2f, 0.6f, 16.17f), new Vector3(0.1f, 1.2f, 0.06f), new Color(0.4f, 0.4f, 0.42f)).Instance("DialPost"));
        Put("dc.ups-batteries", new Vector3(3.2f, 0, 17.0f), -90);
        _ = board; _ = dial;

        // an operations room of the later ages, and the handsets PIP lives in, on a table
        var ops = Put("room.ops-wall", new Vector3(14, 0, 16f));
        ScreenFaces.Light(ops, "screen-", new Vector2(1.17f, 0.67f), new Vector2I(640, 366), i => new ScreenDemo { Kind = ScreenDemo.Look.Dashboard, Seed = i, Pointer = i == 5 ? new Vector2(545, 35) : null });
        ScreenFaces.Light(ops, "desk-", new Vector2(0.52f, 0.3f), new Vector2I(520, 300), i => new ScreenDemo { Kind = ScreenDemo.Look.Dashboard, Seed = i + 3 });
        AddChild(new LowPoly().Box(new Vector3(17.5f, 0.37f, 19.5f), new Vector3(1.6f, 0.74f, 0.8f), new Color(0.6f, 0.46f, 0.3f)).Instance("PhoneTable"));
        float px = 16.95f;
        foreach (var id in new[] { "room.pip-handset", "phone.brick", "phone.flip", "phone.touch", "room.laptop" })
        {
            var n = Put(id, new Vector3(px, 0.74f + (id == "room.pip-handset" ? 0.09f : 0), 19.5f), -15);
            var (look, m, pxs) = id switch
            {
                "phone.brick" => (ScreenDemo.Look.Lcd, new Vector2(0.042f, 0.022f), new Vector2I(210, 110)),
                "phone.flip" => (ScreenDemo.Look.Phone, new Vector2(0.038f, 0.05f), new Vector2I(190, 250)),
                "phone.touch" => (ScreenDemo.Look.Phone, new Vector2(0.054f, 0.088f), new Vector2I(240, 390)),
                "room.laptop" => (ScreenDemo.Look.Dashboard, new Vector2(0.31f, 0.185f), new Vector2I(500, 300)),
                _ => (ScreenDemo.Look.Lcd, Vector2.Zero, Vector2I.Zero),
            };
            if (m != Vector2.Zero) ScreenFaces.Light(n, "screen", m, pxs, i => new ScreenDemo { Kind = look, Seed = 7 });
            px += id == "room.laptop" ? 0.4f : 0.22f;
        }
        Put("person.moss", new Vector3(12.6f, 0, 18.6f), 20);
    }

    private static Vector2[] Circle(float r) => Enumerable.Range(0, 6).Select(i => new Vector2(Mathf.Cos(i * Mathf.Tau / 6), Mathf.Sin(i * Mathf.Tau / 6)) * r).ToArray();

    private void Label(string text, Vector3 at, float px, bool flat = false) => AddChild(new Label3D
    {
        Text = text, Position = at, PixelSize = px, FontSize = 48, Modulate = new Color(0.1f, 0.1f, 0.1f), OutlineSize = 0,
        RotationDegrees = flat ? new Vector3(-60, 0, 0) : new Vector3(-90, 0, 0),
    });

    private void Orbit()
    {
        var dir = new Vector3(Mathf.Sin(_yaw) * Mathf.Cos(_pitch), -Mathf.Sin(_pitch), Mathf.Cos(_yaw) * Mathf.Cos(_pitch));
        _cam.LookAtFromPosition(_focus + dir * _dist, _focus, Vector3.Up);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseMotion m && (m.ButtonMask & MouseButtonMask.Left) != 0) { _yaw -= m.Relative.X * 0.01f; _pitch = Mathf.Clamp(_pitch - m.Relative.Y * 0.01f, -1.4f, 0.1f); Orbit(); }
        if (e is InputEventMouseMotion r && (r.ButtonMask & MouseButtonMask.Right) != 0) { _focus += _cam.GlobalBasis.X * -r.Relative.X * 0.03f; Orbit(); }
        if (e is InputEventMouseButton b && b.Pressed && b.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown) { _dist = Mathf.Clamp(_dist * (b.ButtonIndex == MouseButton.WheelUp ? 1.3f : 1.1f), 2, 120); Orbit(); }
        if (e is InputEventKey { Pressed: true, Keycode: Key.Escape }) GetTree().Quit();
    }
}
