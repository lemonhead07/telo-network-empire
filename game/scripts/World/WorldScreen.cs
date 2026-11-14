using Godot;
using Telo.Game.UI;

namespace Telo.Game.World;

/// <summary>
/// A screen that exists in the world: the UI is drawn onto the surface of an object (a handset, a notebook page,
/// the Board's terminal). Using the object leans the camera in; the mouse then clicks on the object itself.
/// There are no floating menus — if you can read it, it is somewhere.
/// </summary>
public partial class WorldScreen : Node3D
{
    public const int PxW = 1280, PxH = 800;

    public ModalView View { get; private set; } = null!;
    private SubViewport _vp = null!;
    private float _w, _h;

    public static WorldScreen Create(ModalView view, float widthM, bool glows)
    {
        var s = new WorldScreen { View = view, _w = widthM, _h = widthM * PxH / PxW };
        s._vp = new SubViewport { Size = new Vector2I(PxW, PxH), RenderTargetUpdateMode = SubViewport.UpdateMode.Always, HandleInputLocally = true, GuiDisableInput = false, TransparentBg = false };
        s.AddChild(s._vp);
        view.InWorld = true;
        s._vp.AddChild(view);

        var tex = s._vp.GetTexture();
        var mat = new StandardMaterial3D { AlbedoTexture = tex, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic };
        // Unshaded keeps text crisp and readable in any light. (Later: paper that takes the room's lighting.)
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        if (!glows) mat.AlbedoColor = new Color(0.9f, 0.88f, 0.84f);
        s.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(s._w, s._h) }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return s;
    }

    /// <summary>Where a camera should sit to read this screen comfortably.</summary>
    public Transform3D LeanPose(float fovDeg, float aspect)
    {
        float tan = Mathf.Tan(Mathf.DegToRad(fovDeg) / 2);
        float dist = Mathf.Max(_h / 2 / tan, _w / 2 / (tan * aspect)) / 0.9f;
        return new Transform3D(GlobalBasis.Orthonormalized(), GlobalPosition + GlobalBasis.Z.Normalized() * dist);
    }

    /// <summary>Where on the player's display a pixel of this screen currently appears (inverse of the click mapping).</summary>
    public Vector2 DisplayPointFor(Vector2 px, Camera3D cam)
    {
        var local = new Vector3((px.X / PxW - 0.5f) * _w, (0.5f - px.Y / PxH) * _h, 0);
        return cam.UnprojectPosition(GlobalTransform * local);
    }

    public override void _Input(InputEvent e)
    {
        if (!View.IsOpen) return;
        if (e.IsActionPressed("cancel")) { View.Close(); GetViewport().SetInputAsHandled(); return; }
        if (e is InputEventKey { Pressed: true, Echo: false } key && View.OnKey(key)) { GetViewport().SetInputAsHandled(); return; }
        if (e is not InputEventMouse m) return;
        var cam = GetViewport().GetCamera3D();
        if (cam is null) return;

        // Where does the cursor's ray meet the screen's surface?
        var origin = cam.ProjectRayOrigin(m.Position);
        var dir = cam.ProjectRayNormal(m.Position);
        var normal = GlobalBasis.Z.Normalized();
        float denom = dir.Dot(normal);
        if (Mathf.Abs(denom) < 1e-5f) return;
        float t = (GlobalPosition - origin).Dot(normal) / denom;
        if (t < 0) return;
        var local = GlobalTransform.AffineInverse() * (origin + dir * t);
        var px = new Vector2((local.X / _w + 0.5f) * PxW, (0.5f - local.Y / _h) * PxH);

        var copy = (InputEventMouse)m.Duplicate();
        copy.Position = px;
        copy.GlobalPosition = px;
        _vp.PushInput(copy, true);
        GetViewport().SetInputAsHandled();
    }
}
