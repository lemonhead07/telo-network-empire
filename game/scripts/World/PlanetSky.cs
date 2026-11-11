using Godot;

namespace Telo.Game.World;

/// <summary>Sun, day/night, an atmosphere shell you can fly out of, and stars beyond it.</summary>
public partial class PlanetSky : Node3D
{
    private DirectionalLight3D _sun = null!;
    private ShaderMaterial _air = null!, _stars = null!;
    private float _townLongitude;

    public Vector3 SunDir { get; private set; } = Vector3.Right;

    private const string AirShader = @"
shader_type spatial;
render_mode unshaded, cull_front, blend_mix, depth_draw_never;
uniform vec3 sun_dir = vec3(1.0, 0.0, 0.0);
uniform float air_r = 3000.0;
varying vec3 wn;
void vertex() { wn = normalize((MODEL_MATRIX * vec4(VERTEX, 0.0)).xyz); }
void fragment() {
    float d = dot(normalize(wn), sun_dir);
    float day = smoothstep(-0.30, 0.15, d);
    float dusk = (1.0 - smoothstep(0.0, 0.40, abs(d + 0.08))) * 0.7;
    vec3 col = mix(vec3(0.03, 0.04, 0.10), vec3(0.40, 0.64, 0.93), day);
    col = mix(col, vec3(0.98, 0.56, 0.32), dusk);
    vec3 view_w = normalize((INV_VIEW_MATRIX * vec4(-VIEW, 0.0)).xyz);
    float s = max(dot(view_w, sun_dir), 0.0);
    col += vec3(1.0, 0.92, 0.75) * (smoothstep(0.9990, 0.9996, s) * 2.5 + pow(s, 40.0) * 0.25);
    ALBEDO = col;
    float outside = smoothstep(air_r * 0.97, air_r * 1.6, length(CAMERA_POSITION_WORLD));
    ALPHA = clamp(day + dusk * 0.6, 0.04, 0.96) * mix(1.0, 0.28, outside);
}";

    private const string StarShader = @"
shader_type spatial;
render_mode unshaded, cull_front;
uniform vec3 sun_dir = vec3(1.0, 0.0, 0.0);
varying vec3 dir;
void vertex() { dir = VERTEX; }
void fragment() {
    vec3 d = normalize(dir);
    vec3 g = floor(d * 95.0);
    float h = fract(sin(dot(g, vec3(12.9898, 78.233, 37.719))) * 43758.5453);
    vec3 f = fract(d * 95.0) - 0.5;
    float star = step(0.992, h) * smoothstep(0.30, 0.0, length(f));
    float s = max(dot(d, sun_dir), 0.0);
    vec3 sun = vec3(1.0, 0.95, 0.82) * (smoothstep(0.9993, 0.9997, s) * 5.0 + pow(s, 300.0) * 0.5);
    ALBEDO = vec3(star) * (0.5 + h) + sun + vec3(0.004, 0.005, 0.012);
}";

    public void Setup(float planetRadius, Vector3 townDir)
    {
        _townLongitude = Mathf.Atan2(townDir.Z, townDir.X);
        _sun = new DirectionalLight3D
        {
            LightColor = new Color(1f, 0.96f, 0.88f), LightEnergy = 1.15f, ShadowEnabled = true,
            DirectionalShadowMaxDistance = 320, DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits,
        };
        AddChild(_sun);

        _air = new ShaderMaterial { Shader = new Shader { Code = AirShader } };
        float airR = planetRadius + 650;
        _air.SetShaderParameter("air_r", airR);
        AddChild(new MeshInstance3D
        {
            Name = "Atmosphere", Mesh = new SphereMesh { Radius = airR, Height = airR * 2, RadialSegments = 96, Rings = 48 },
            MaterialOverride = _air, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        _stars = new ShaderMaterial { Shader = new Shader { Code = StarShader } };
        AddChild(new MeshInstance3D
        {
            Name = "Stars", Mesh = new SphereMesh { Radius = 40000, Height = 80000, RadialSegments = 32, Rings = 16 },
            MaterialOverride = _stars, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = Colors.Black,
                AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.55f, 0.62f, 0.75f), AmbientLightEnergy = 0.38f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
            },
        });
    }

    /// <summary>Hour of day (0–24) at the town. The sun is overhead there at noon.</summary>
    public void SetHour(float hour)
    {
        float a = _townLongitude - (hour - 12f) / 24f * Mathf.Tau;
        SunDir = new Vector3(Mathf.Cos(a), 0.32f, Mathf.Sin(a)).Normalized();
        _sun.Basis = Basis.LookingAt(-SunDir, Vector3.Up);
        _air.SetShaderParameter("sun_dir", SunDir);
        _stars.SetShaderParameter("sun_dir", SunDir);
    }
}
