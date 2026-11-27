using Godot;

namespace Telo.Game.World;

/// <summary>
/// Sun, day/night, an atmosphere shell you can fly out of, and stars beyond it; and the light the world is seen in (LOOK1):
/// a warm low sun in the mornings and evenings, haze in the distance the colour of the sky at the horizon, ambient
/// occlusion, a filmic curve and a little glow. The light follows the sun's height where the camera is, so it is
/// evening wherever on the planet it is evening; from high above, the sun is simply the sun.
/// </summary>
public partial class PlanetSky : Node3D
{
    private DirectionalLight3D _sun = null!;
    private ShaderMaterial _air = null!, _stars = null!;
    private Godot.Environment _env = null!;
    private float _townLongitude, _radius, _airR;

    public Vector3 SunDir { get; private set; } = Vector3.Right;

    // the sky's colours; the haze (fog) is the horizon's, so far hills melt into the sky
    private static readonly Color Zenith = new(0.32f, 0.55f, 0.90f), Horizon = new(0.72f, 0.83f, 0.94f), NightSky = new(0.025f, 0.035f, 0.08f);
    private static readonly Color DuskGlow = new(1.0f, 0.55f, 0.30f), DuskPink = new(0.78f, 0.50f, 0.52f);

    /// <summary>The haze: a sixth gone at a kilometre, nearly a third at two.</summary>
    private const float FogDensity = 0.00017f;

    // The sky shell. What is the same for every pixel (where you stand, how high the sun is there, whether you are out in
    // space) is worked out once a frame in Relight and passed in, so the shader does only the per-pixel part.
    private const string AirShader = @"
shader_type spatial;
render_mode unshaded, cull_front, blend_mix, depth_draw_never, fog_disabled;
uniform vec3 sun_dir = vec3(1.0, 0.0, 0.0);
uniform vec3 cam_up = vec3(0.0, 1.0, 0.0);
uniform float local_day = 1.0;   // how high the sun is where you stand, as daylight
uniform float dusk = 0.0;        // how near sunrise or sunset it is where you stand
uniform float outside = 0.0;     // 1 out in space
uniform vec3 zenith : source_color;
uniform vec3 horizon : source_color;
uniform vec3 night : source_color;
uniform vec3 dusk_glow : source_color;
uniform vec3 dusk_pink : source_color;
varying vec3 wn;
void vertex() { wn = normalize((MODEL_MATRIX * vec4(VERTEX, 0.0)).xyz); }
void fragment() {
    vec3 view_w = normalize((INV_VIEW_MATRIX * vec4(-VIEW, 0.0)).xyz);
    float d = dot(normalize(wn), sun_dir);            // daylight where this bit of sky is
    float day = mix(smoothstep(-0.40, 0.20, d), local_day, 0.35);
    float l = 1.0 - clamp(dot(view_w, cam_up), 0.0, 1.0);
    float low = l * l * l;                              // 1 at the horizon
    vec3 col = mix(night, mix(zenith, horizon, low), day);
    // sunrise and sunset: a glow along the horizon, strongest towards the sun
    float toward = max(dot(view_w, sun_dir), 0.0);
    if (dusk > 0.001) col = mix(col, mix(dusk_pink, dusk_glow, toward * toward), dusk * low * (0.35 + 0.55 * toward) * smoothstep(-0.25, 0.05, d));
    col += vec3(1.0, 0.90, 0.72) * (smoothstep(0.9993, 0.9997, toward) * 2.5 + pow(toward, 60.0) * 0.30 * day);
    ALBEDO = mix(col, mix(night, mix(zenith, horizon, 0.25), day), outside);   // from space: a thin blue shell
    ALPHA = clamp(day + dusk * 0.6, 0.04, 0.97) * mix(1.0, 0.28, outside);
}";

    private const string StarShader = @"
shader_type spatial;
render_mode unshaded, cull_front, fog_disabled;
uniform vec3 sun_dir = vec3(1.0, 0.0, 0.0);
varying vec3 dir;
void vertex() { dir = VERTEX; }
void fragment() {
    vec3 d = normalize(dir);
    vec3 view_w = normalize((INV_VIEW_MATRIX * vec4(-VIEW, 0.0)).xyz);   // the sun is far away: no parallax, so no second sun
    vec3 g = floor(d * 95.0);
    float h = fract(sin(dot(g, vec3(12.9898, 78.233, 37.719))) * 43758.5453);
    vec3 f = fract(d * 95.0) - 0.5;
    float star = step(0.992, h) * smoothstep(0.30, 0.0, length(f));
    float s = max(dot(view_w, sun_dir), 0.0);
    vec3 sun = vec3(1.0, 0.95, 0.82) * (smoothstep(0.9993, 0.9997, s) * 5.0 + pow(s, 300.0) * 0.5);
    ALBEDO = vec3(star) * (0.5 + h) + sun + vec3(0.004, 0.005, 0.012);
}";

    public void Setup(float planetRadius, Vector3 townDir)
    {
        _radius = planetRadius;
        _townLongitude = Mathf.Atan2(townDir.Z, townDir.X);
        _sun = new DirectionalLight3D
        {
            LightColor = new Color(1f, 0.96f, 0.88f), LightEnergy = 1.15f, ShadowEnabled = true, ShadowBlur = 1.3f,
            DirectionalShadowMaxDistance = 320, DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits,
        };
        GameSettings.ApplySun(_sun);   // shadow reach follows the graphics setting
        AddChild(_sun);

        _air = new ShaderMaterial { Shader = new Shader { Code = AirShader } };
        float airR = planetRadius + 650;
        _airR = airR;
        _air.SetShaderParameter("zenith", Zenith); _air.SetShaderParameter("horizon", Horizon); _air.SetShaderParameter("night", NightSky);
        _air.SetShaderParameter("dusk_glow", DuskGlow); _air.SetShaderParameter("dusk_pink", DuskPink);
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
        _env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = Colors.Black,
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.55f, 0.62f, 0.75f), AmbientLightEnergy = 0.38f,
            // a filmic curve: highlights roll off instead of clipping, a touch more contrast and colour to make up for it
            TonemapMode = Godot.Environment.ToneMapper.Filmic, TonemapExposure = 1.0f, TonemapWhite = 2.2f,
            AdjustmentEnabled = true, AdjustmentContrast = 1.06f, AdjustmentSaturation = 1.04f,
            // haze: thin near, thick over kilometres; it takes the horizon's colour and glows towards the sun
            FogEnabled = true, FogMode = Godot.Environment.FogModeEnum.Exponential, FogDensity = FogDensity, FogSkyAffect = 0f,
            FogSunScatter = 0.18f, FogHeightDensity = 0f, FogAerialPerspective = 0f,
            // shadows in corners and under eaves; ambient occlusion and glow are switched by the graphics setting
            SsaoRadius = 1.1f, SsaoIntensity = 1.8f, SsaoPower = 1.4f, SsaoDetail = 0.6f, SsaoLightAffect = 0.18f,
            SsilRadius = 4f, SsilIntensity = 0.7f,
            GlowIntensity = 0.55f, GlowStrength = 0.9f, GlowBloom = 0.0f, GlowHdrThreshold = 1.1f, GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Softlight,
        };
        var world = new WorldEnvironment { Environment = _env };
        GameSettings.ApplyEnvironment(world);
        AddChild(world);
    }

    /// <summary>Hour of day (0–24) at the town. The sun is overhead there at noon.</summary>
    public void SetHour(float hour)
    {
        float a = _townLongitude - (hour - 12f) / 24f * Mathf.Tau;
        SunDir = new Vector3(Mathf.Cos(a), 0.32f, Mathf.Sin(a)).Normalized();
        _sun.Basis = Basis.LookingAt(-SunDir, Vector3.Up);
        _air.SetShaderParameter("sun_dir", SunDir);
        _stars.SetShaderParameter("sun_dir", SunDir);
        Relight();   // every frame (GameRoot._Process): the camera may have moved into another part of the day
    }

    /// <summary>The sun's colour and strength, the ambient light and the haze, from the sun's height where the camera is.</summary>
    private void Relight()
    {
        var cam = GetViewport()?.GetCamera3D();
        var at = cam?.GlobalPosition ?? Vector3.Up * _radius;
        float e = at.Normalized().Dot(SunDir);
        // high above the planet you see day and night side by side: the light is the plain sun, and the haze thins away
        float space = Mathf.SmoothStep(200f, 2000f, at.Length() - _radius);

        float up = Mathf.SmoothStep(-0.06f, 0.10f, e);           // is the sun up here at all
        float low = 1f - Mathf.SmoothStep(0.08f, 0.62f, e);      // how low (and so how warm) it is: golden below about 20 degrees
        float dusk = 1f - Mathf.SmoothStep(0.0f, 0.40f, Mathf.Abs(e + 0.02f));
        // daylight in the sky round the horizon (as the sky shader works it out, averaged round the horizon)
        float day = Mathf.Lerp(Mathf.SmoothStep(-0.40f, 0.20f, e * 0.8f), Mathf.SmoothStep(-0.25f, 0.10f, e), 0.35f);

        var noon = new Color(1f, 0.97f, 0.90f); var golden = new Color(1f, 0.70f, 0.42f); var ember = new Color(1f, 0.48f, 0.24f);
        var sunCol = noon.Lerp(golden, Mathf.Clamp(low * 1.3f, 0, 1)).Lerp(ember, Mathf.SmoothStep(0.55f, 1f, low) * 0.7f);
        float sunEnergy = 1.25f * up * (1f - 0.25f * low);
        _sun.LightColor = noon.Lerp(sunCol, 1f - space);
        _sun.LightEnergy = Mathf.Lerp(sunEnergy, 1.2f, space);

        // ambient: the sky's light. Blue-white by day, warm at the edges of the day, a dim blue at night (rooms have lamps)
        var ambDay = new Color(0.56f, 0.64f, 0.78f); var ambDusk = new Color(0.68f, 0.56f, 0.54f); var ambNight = new Color(0.30f, 0.37f, 0.58f);
        var amb = ambNight.Lerp(ambDay, day).Lerp(ambDusk, Mathf.Max(dusk, low * up) * 0.55f);
        _env.AmbientLightColor = amb.Lerp(ambDay, space);
        _env.AmbientLightEnergy = Mathf.Lerp(Mathf.Lerp(0.33f, 0.40f, day), 0.40f, space);

        // the sky's per-frame numbers (the shader does only what differs from pixel to pixel)
        _air.SetShaderParameter("cam_up", at.Normalized());
        _air.SetShaderParameter("local_day", Mathf.SmoothStep(-0.25f, 0.10f, e));
        _air.SetShaderParameter("dusk", dusk);
        _air.SetShaderParameter("outside", Mathf.SmoothStep(_airR * 0.97f, _airR * 1.6f, at.Length()));

        // haze: the horizon's colour (as the sky shader draws it, averaged round the horizon)
        var haze = NightSky.Lerp(Horizon, day).Lerp(DuskPink.Lerp(DuskGlow, 0.35f), dusk * 0.45f * Mathf.SmoothStep(-0.25f, 0.05f, e));
        _env.FogLightColor = haze;
        // haze sits in the low air: looking down from a hill or a plane you see through less of it
        _env.FogDensity = FogDensity * (1f - space) / (1f + Mathf.Max(at.Length() - _radius - 20f, 0f) / 250f);
    }
}
