using Godot;

namespace Telo.Game;

public enum GraphicsQuality { Low, Medium, High }

/// <summary>
/// The player's own settings (mouse, screen, graphics, the length of a day, volume), kept in user://settings.cfg.
/// These are about this computer and this player, not the game: they are never in a save, and the sim never sees them.
/// Automated runs (--smoke, --shot, --shots, --autoplay) always use the defaults and never write the file,
/// so a player's settings can't change what the checks see.
/// </summary>
public static class GameSettings
{
    public const string FilePath = "user://settings.cfg";
    /// <summary>Things that follow the graphics setting at runtime join these groups (the sun, the far trees and rocks).</summary>
    public const string SunGroup = "settings_sun", FarPropsGroup = "settings_far_props";

    public const int MouseSteps = 10, DefaultMouse = 5, DefaultVolume = 80;
    public static readonly int[] DayMinuteChoices = { 8, 12, 16 };

    /// <summary>1 (slow) to 10 (fast). 5 is the feel the game was built with.</summary>
    public static int Mouse { get; set; } = DefaultMouse;
    public static bool InvertY { get; set; }
    public static bool Fullscreen { get; set; } = true;
    public static GraphicsQuality Graphics { get; set; } = GraphicsQuality.Medium;
    /// <summary>Real minutes from six in the morning to ten at night (the waking day; the night is slept).</summary>
    public static int DayMinutes { get; set; } = 8;
    /// <summary>Master volume, 0 to 100 in steps of 10. 0 mutes.</summary>
    public static int Volume { get; set; } = DefaultVolume;

    /// <summary>True for the developer's automated runs: defaults only, nothing saved, the window left alone.</summary>
    public static bool Automated { get; } = OS.GetCmdlineUserArgs().Any(a => a == "--smoke" || a.StartsWith("--shot") || a.StartsWith("--autoplay"));

    static GameSettings() => Load();

    /// <summary>Radians of turn per pixel of mouse movement. Each step is about a quarter faster than the one below; step 5 is 0.0025.</summary>
    public static float MouseSensitivity => 0.0025f * Mathf.Pow(2f, (Mouse - DefaultMouse) / 3f);

    /// <summary>
    /// Real seconds in a waking day. The game's pace (how much a day holds, the Act 5 script player's timings) is
    /// measured at 8 minutes; 12 and 16 are for players who want longer at each job, and make the game longer in real time.
    /// </summary>
    public static float DayLengthSeconds => DayMinutes * 60f;

    /// <summary>How far away trees and rocks are still drawn, in metres.</summary>
    public static float PropDrawDistance => Graphics switch { GraphicsQuality.Low => 1000f, GraphicsQuality.High => 2800f, _ => 1800f };

    public static string QualityName(GraphicsQuality q) => q switch { GraphicsQuality.Low => "Low", GraphicsQuality.High => "High", _ => "Medium" };

    public static void ResetToDefaults()
    {
        Mouse = DefaultMouse; InvertY = false; Fullscreen = true;
        Graphics = GraphicsQuality.Medium; DayMinutes = 8; Volume = DefaultVolume;
    }

    public static void Load()
    {
        ResetToDefaults();
        if (Automated) return;
        var cfg = new ConfigFile();
        if (cfg.Load(FilePath) != Error.Ok) return;
        Mouse = Math.Clamp((int)cfg.GetValue("controls", "mouse_sensitivity", DefaultMouse), 1, MouseSteps);
        InvertY = (bool)cfg.GetValue("controls", "invert_y", false);
        Fullscreen = (bool)cfg.GetValue("display", "fullscreen", true);
        Graphics = ((string)cfg.GetValue("display", "graphics", "medium")) switch { "low" => GraphicsQuality.Low, "high" => GraphicsQuality.High, _ => GraphicsQuality.Medium };
        int day = (int)cfg.GetValue("game", "day_minutes", 8);
        DayMinutes = DayMinuteChoices.Contains(day) ? day : 8;
        Volume = Math.Clamp((int)cfg.GetValue("audio", "master_volume", DefaultVolume) / 10 * 10, 0, 100);
    }

    public static void Save()
    {
        if (Automated) return;
        var cfg = new ConfigFile();
        cfg.SetValue("controls", "mouse_sensitivity", Mouse);
        cfg.SetValue("controls", "invert_y", InvertY);
        cfg.SetValue("display", "fullscreen", Fullscreen);
        cfg.SetValue("display", "graphics", QualityName(Graphics).ToLowerInvariant());
        cfg.SetValue("game", "day_minutes", DayMinutes);
        cfg.SetValue("audio", "master_volume", Volume);
        cfg.Save(FilePath);
    }

    /// <summary>
    /// Put the settings into effect: window, renderer, sound, and anything in the world that follows them.
    /// Safe to call at any time and as often as you like. Mouse and day length need nothing here: they are read where they are used.
    /// </summary>
    public static void Apply()
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return;
        var root = tree.Root;

        // the window: automated runs keep the project's window as it is, so screenshots stay the same size
        if (!Automated && DisplayServer.GetName() != "headless")
        {
            var want = Fullscreen ? Window.ModeEnum.Fullscreen : Window.ModeEnum.Windowed;
            if (root.Mode != want) root.Mode = want;
        }

        // Medium is the game as it has always looked. Low is for weak computers: fewer pixels drawn (and sharpened back up),
        // no multisampling, short hard shadows, trees fade sooner. Every 2D page on an object keeps its full resolution.
        // (a computer without Vulkan runs the simpler OpenGL renderer, which has no FSR sharpening or FXAA: plain scaling there)
        string renderer = RenderingServer.GetCurrentRenderingMethod();
        switch (Graphics)
        {
            case GraphicsQuality.Low:
                root.Scaling3DMode = renderer == "forward_plus" ? Viewport.Scaling3DModeEnum.Fsr : Viewport.Scaling3DModeEnum.Bilinear;
                root.Scaling3DScale = 0.75f;
                root.Msaa3D = Viewport.Msaa.Disabled;
                root.ScreenSpaceAA = renderer == "gl_compatibility" ? Viewport.ScreenSpaceAAEnum.Disabled : Viewport.ScreenSpaceAAEnum.Fxaa;
                RenderingServer.DirectionalShadowAtlasSetSize(2048, true);
                RenderingServer.DirectionalSoftShadowFilterSetQuality(RenderingServer.ShadowQuality.Hard);
                RenderingServer.PositionalSoftShadowFilterSetQuality(RenderingServer.ShadowQuality.Hard);
                break;
            case GraphicsQuality.High:
                root.Scaling3DMode = Viewport.Scaling3DModeEnum.Bilinear;
                root.Scaling3DScale = 1f;
                root.Msaa3D = Viewport.Msaa.Msaa8X;
                root.ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled;
                RenderingServer.DirectionalShadowAtlasSetSize(4096, true);
                RenderingServer.DirectionalSoftShadowFilterSetQuality(RenderingServer.ShadowQuality.SoftMedium);
                RenderingServer.PositionalSoftShadowFilterSetQuality(RenderingServer.ShadowQuality.SoftMedium);
                break;
            default:   // the project's own settings (project.godot): MSAA 4x, 4096 shadow map, soft-low filtering
                root.Scaling3DMode = Viewport.Scaling3DModeEnum.Bilinear;
                root.Scaling3DScale = 1f;
                root.Msaa3D = Viewport.Msaa.Msaa4X;
                root.ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled;
                RenderingServer.DirectionalShadowAtlasSetSize(4096, true);
                RenderingServer.DirectionalSoftShadowFilterSetQuality(RenderingServer.ShadowQuality.SoftLow);
                RenderingServer.PositionalSoftShadowFilterSetQuality(RenderingServer.ShadowQuality.SoftLow);
                break;
        }
        foreach (var n in tree.GetNodesInGroup(SunGroup)) if (n is DirectionalLight3D sun) ApplySun(sun);
        foreach (var n in tree.GetNodesInGroup(FarPropsGroup)) if (n is GeometryInstance3D g) g.VisibilityRangeEnd = PropDrawDistance;

        // master volume: the slider is perceptual (squared), so 50% sounds about half as loud
        AudioServer.SetBusMute(0, Volume <= 0);
        if (Volume > 0) AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(Mathf.Pow(Volume / 100f, 2f)));
    }

    /// <summary>The sun's shadows for the current graphics setting. The sky calls this when it makes the sun; Apply() repeats it on a change.</summary>
    public static void ApplySun(DirectionalLight3D sun)
    {
        sun.AddToGroup(SunGroup);
        (sun.DirectionalShadowMode, sun.DirectionalShadowMaxDistance) = Graphics switch
        {
            GraphicsQuality.Low => (DirectionalLight3D.ShadowMode.Orthogonal, 120f),
            GraphicsQuality.High => (DirectionalLight3D.ShadowMode.Parallel4Splits, 500f),
            _ => (DirectionalLight3D.ShadowMode.Parallel4Splits, 320f),
        };
    }

    /// <summary>Distant trees and rocks: drawn out to the current setting's distance, and kept in step when it changes.</summary>
    public static void ApplyFarProps(GeometryInstance3D g)
    {
        g.AddToGroup(FarPropsGroup);
        g.VisibilityRangeEnd = PropDrawDistance;
    }
}
