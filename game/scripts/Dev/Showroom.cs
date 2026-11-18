using Godot;
using Telo.Game.World;

namespace Telo.Game.Dev;

/// <summary>
/// Every model in game/assets/models on a lawn, under the game's light: project sites along the back, the things you carry and
/// fit in front. A dev tool for checking models in the real renderer (D15).
///   godot --path game res://scenes/Showroom.tscn                          (look around: orbit with the mouse, scroll to zoom)
///   godot --path game res://scenes/Showroom.tscn -- --shot=/abs/path/x.png (saves x-sites-a, x-sites-b, x-kit, then quits)
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
