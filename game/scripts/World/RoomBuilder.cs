using Godot;

namespace Telo.Game.World;

/// <summary>
/// Builds the Act 0 rented room procedurally from primitives (greybox). Art replaces these meshes later;
/// the interactables and their positions are the contract.
/// </summary>
public static class RoomBuilder
{
    public const float W = 6f, D = 5f, H = 2.7f;

    public static readonly Color Floor = new(0.32f, 0.24f, 0.17f), Wall = new(0.62f, 0.56f, 0.44f),
        Wood = new(0.42f, 0.28f, 0.16f), Metal = new(0.35f, 0.37f, 0.38f), Beige = new(0.78f, 0.74f, 0.62f),
        Board = new(0.55f, 0.47f, 0.33f), Fabric = new(0.25f, 0.3f, 0.22f);

    public static void BuildShell(Node3D root)
    {
        Box(root, new Vector3(0, -0.05f, 0), new Vector3(W, 0.1f, D), Floor, solid: true);
        Box(root, new Vector3(0, H + 0.05f, 0), new Vector3(W, 0.1f, D), Wall * 0.9f, solid: true);
        Box(root, new Vector3(0, H / 2, -D / 2), new Vector3(W, H, 0.1f), Wall, solid: true);
        Box(root, new Vector3(0, H / 2, D / 2), new Vector3(W, H, 0.1f), Wall, solid: true);
        Box(root, new Vector3(-W / 2, H / 2, 0), new Vector3(0.1f, H, D), Wall, solid: true);
        Box(root, new Vector3(W / 2, H / 2, 0), new Vector3(0.1f, H, D), Wall, solid: true);
        // window (emissive pane = streetlight outside)
        var pane = Box(root, new Vector3(W / 2 - 0.06f, 1.6f, 0.8f), new Vector3(0.02f, 0.9f, 1.2f), new Color(0.45f, 0.55f, 0.7f));
        ((StandardMaterial3D)pane.MaterialOverride).EmissionEnabled = true;
        ((StandardMaterial3D)pane.MaterialOverride).Emission = new Color(0.3f, 0.38f, 0.5f);

        root.AddChild(new OmniLight3D { Position = new Vector3(0.5f, 2.3f, 0), LightColor = new Color(1f, 0.82f, 0.6f), LightEnergy = 1.4f, OmniRange = 7, ShadowEnabled = true });
        root.AddChild(new OmniLight3D { Position = new Vector3(-1.8f, 1.4f, -1.6f), LightColor = new Color(1f, 0.7f, 0.45f), LightEnergy = 0.6f, OmniRange = 3 });

        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.05f, 0.05f, 0.07f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.35f, 0.3f, 0.28f), AmbientLightEnergy = 0.5f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic, FogEnabled = true, FogLightColor = new Color(0.4f, 0.35f, 0.3f), FogDensity = 0.02f,
        };
        root.AddChild(new WorldEnvironment { Environment = env });
    }

    /// <summary>Create an interactable prop: visual box + collision on the interactable body.</summary>
    public static Interactable Prop(Node3D root, string name, Vector3 pos, Vector3 size, Color color, Func<string> prompt, Action onUse)
    {
        var body = new Interactable { Name = name, Position = pos, Prompt = prompt, OnUse = onUse };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = Mat(color) });
        root.AddChild(body);
        return body;
    }

    public static MeshInstance3D Box(Node3D root, Vector3 pos, Vector3 size, Color color, bool solid = false)
    {
        var mesh = new MeshInstance3D { Position = pos, Mesh = new BoxMesh { Size = size }, MaterialOverride = Mat(color) };
        if (solid)
        {
            var body = new StaticBody3D { Position = pos };
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
            root.AddChild(body);
        }
        root.AddChild(mesh);
        return mesh;
    }

    public static Label3D Label(Node3D root, string text, Vector3 pos, float size = 0.06f, float yaw = 0)
    {
        var l = new Label3D { Text = text, Position = pos, PixelSize = size / 64f, FontSize = 64, Modulate = new Color(0.1f, 0.08f, 0.05f), OutlineSize = 0, RotationDegrees = new Vector3(0, yaw, 0) };
        root.AddChild(l);
        return l;
    }

    public static StandardMaterial3D Mat(Color c) => new() { AlbedoColor = c, Roughness = 0.85f };
}
