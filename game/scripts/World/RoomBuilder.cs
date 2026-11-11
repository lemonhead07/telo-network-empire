using Godot;

namespace Telo.Game.World;

/// <summary>
/// Builds the Act 0 rented room from primitives, in the local space of a building node placed on the planet;
/// the interactables and their positions are the contract.
/// </summary>
public static class RoomBuilder
{
    public const float W = 6f, D = 5f, H = 2.7f;

    public static readonly Color Floor = new(0.32f, 0.24f, 0.17f), Wall = new(0.62f, 0.56f, 0.44f),
        Wood = new(0.42f, 0.28f, 0.16f), Metal = new(0.35f, 0.37f, 0.38f), Beige = new(0.78f, 0.74f, 0.62f),
        Board = new(0.55f, 0.47f, 0.33f), Fabric = new(0.25f, 0.3f, 0.22f);

    public const float DoorX = 1.2f, DoorW = 1.0f, DoorH = 2.1f;

    /// <summary>Walls, floor, roof, a doorway on the south (+Z) side with a ramp, and interior lights.</summary>
    public static void BuildShell(Node3D root)
    {
        Box(root, new Vector3(0, -0.25f, 0), new Vector3(W, 0.5f, D), Floor, solid: true);
        Box(root, new Vector3(0, H + 0.05f, 0), new Vector3(W, 0.1f, D), Wall * 0.9f, solid: true);
        Box(root, new Vector3(0, H + 0.2f, 0), new Vector3(W + 0.6f, 0.2f, D + 0.6f), new Color(0.28f, 0.2f, 0.18f)); // roof slab
        Box(root, new Vector3(0, H / 2, -D / 2), new Vector3(W, H, 0.1f), Wall, solid: true);
        Box(root, new Vector3(-W / 2, H / 2, 0), new Vector3(0.1f, H, D), Wall, solid: true);
        Box(root, new Vector3(W / 2, H / 2, 0), new Vector3(0.1f, H, D), Wall, solid: true);
        // south wall in three pieces around the doorway
        float l0 = -W / 2, l1 = DoorX - DoorW / 2, r0 = DoorX + DoorW / 2, r1 = W / 2;
        Box(root, new Vector3((l0 + l1) / 2, H / 2, D / 2), new Vector3(l1 - l0, H, 0.1f), Wall, solid: true);
        Box(root, new Vector3((r0 + r1) / 2, H / 2, D / 2), new Vector3(r1 - r0, H, 0.1f), Wall, solid: true);
        Box(root, new Vector3(DoorX, (H + DoorH) / 2, D / 2), new Vector3(DoorW, H - DoorH, 0.1f), Wall, solid: true);
        // ramp from the doorstep down to the ground
        var ramp = new StaticBody3D { Position = new Vector3(DoorX, -0.11f, D / 2 + 0.75f), RotationDegrees = new Vector3(6.5f, 0, 0) };
        ramp.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.5f, 0.1f, 1.6f) } });
        ramp.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.5f, 0.1f, 1.6f) }, MaterialOverride = Mat(Wood * 0.8f) });
        root.AddChild(ramp);
        var sign = Label(root, "TELEX & RADIO REPAIRS", new Vector3(DoorX, 2.4f, D / 2 + 0.07f), 0.16f);
        sign.Modulate = new Color(0.2f, 0.1f, 0.06f);

        // window (emissive pane)
        var pane = Box(root, new Vector3(W / 2 - 0.06f, 1.6f, 0.8f), new Vector3(0.14f, 0.9f, 1.2f), new Color(0.45f, 0.55f, 0.7f));
        ((StandardMaterial3D)pane.MaterialOverride).EmissionEnabled = true;
        ((StandardMaterial3D)pane.MaterialOverride).Emission = new Color(0.3f, 0.38f, 0.5f);

        root.AddChild(new OmniLight3D { Position = new Vector3(0.5f, 2.3f, 0), LightColor = new Color(1f, 0.82f, 0.6f), LightEnergy = 1.2f, OmniRange = 6, ShadowEnabled = true });
        root.AddChild(new OmniLight3D { Position = new Vector3(-1.8f, 1.4f, -1.6f), LightColor = new Color(1f, 0.7f, 0.45f), LightEnergy = 0.5f, OmniRange = 3 });
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
