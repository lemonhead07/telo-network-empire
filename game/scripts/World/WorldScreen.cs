using Godot;
using Telo.Game.UI;
using Telo.Sim.Content;

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
    private ScreenCursor _cursor = null!;
    private float _w, _h;

    public static WorldScreen Create(ModalView view, float widthM, bool glows, bool onTop = false)
    {
        var s = new WorldScreen { View = view, _w = widthM, _h = widthM * PxH / PxW };
        s._vp = new SubViewport { Size = new Vector2I(PxW, PxH), RenderTargetUpdateMode = SubViewport.UpdateMode.Always, HandleInputLocally = true, GuiDisableInput = false, TransparentBg = false };
        s.AddChild(s._vp);
        view.InWorld = true;
        s._vp.AddChild(view);
        s._cursor = new ScreenCursor();   // the pointer is drawn on the screen itself, on top of the view
        s._vp.AddChild(s._cursor);

        var tex = s._vp.GetTexture();
        var mat = new StandardMaterial3D { AlbedoTexture = tex, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic };
        // Unshaded keeps text crisp and readable in any light. (Later: paper that takes the room's lighting.)
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        if (!glows) mat.AlbedoColor = new Color(0.9f, 0.88f, 0.84f);
        if (onTop) { mat.NoDepthTest = true; mat.RenderPriority = 10; }   // a thing in your hand is never behind a wall
        s.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(s._w, s._h) }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return s;
    }

    /// <summary>Where a camera should sit to read this screen comfortably.</summary>
    public Transform3D LeanPose(float fovDeg, float aspect) =>
        new(GlobalBasis.Orthonormalized(), GlobalPosition + GlobalBasis.Z.Normalized() * FitDistance(fovDeg, aspect, 0.9f));

    /// <summary>How far in front of a camera this screen fills that share of its view.</summary>
    public float FitDistance(float fovDeg, float aspect, float fill)
    {
        float tan = Mathf.Tan(Mathf.DegToRad(fovDeg) / 2);
        return Mathf.Max(_h / 2 / tan, _w / 2 / (tan * aspect)) / fill;
    }

    // ---- one screen, several pages: a view shown over another, and the way back -------------------
    private readonly Stack<(ModalView View, InterfaceTier Tier)> _under = new();
    /// <summary>A view shown over another has closed and the one under it is back (it may be one nobody is using).</summary>
    public event Action? Uncovered;

    /// <summary>
    /// Show another view on this screen, over the one it has now. When that view closes (Esc, or its own way out), the one
    /// under it comes back as it was. PIP's handset uses this for the pause page and whatever the pause page opens.
    /// </summary>
    public void Push(ModalView view, InterfaceTier tier)
    {
        if (view == View) { view.Open(tier); return; }
        _under.Push((View, View.Tier));
        Host(view);
        Action? back = null;
        back = () =>
        {
            view.Closed -= back;
            if (View != view || _under.Count == 0) return;
            var (was, wasTier) = _under.Pop();
            Host(was);
            was.Show(wasTier);
            if (was.IsOpen) { was.Rebuild(); Input.MouseMode = Input.MouseModeEnum.Visible; }   // still in use: the pointer stays free
            Uncovered?.Invoke();
        };
        view.Closed += back;
        view.Open(tier);
    }

    private void Host(ModalView view)
    {
        if (View.GetParent() == _vp) _vp.RemoveChild(View);
        view.GetParent()?.RemoveChild(view);
        view.InWorld = true;
        View = view;
        _vp.AddChild(view);
        _vp.MoveChild(_cursor, -1);   // the pointer stays on top
    }

    /// <summary>Where on the player's display a pixel of this screen currently appears (inverse of the click mapping).</summary>
    public Vector2 DisplayPointFor(Vector2 px, Camera3D cam)
    {
        var local = new Vector3((px.X / PxW - 0.5f) * _w, (0.5f - px.Y / PxH) * _h, 0);
        return cam.UnprojectPosition(GlobalTransform * local);
    }

    public override void _Process(double delta)
    {
        if (!View.IsOpen && _cursor.Visible) _cursor.Visible = false;
    }

    /// <summary>The screen a view is drawn on, if it is drawn in the world.</summary>
    public static WorldScreen? Of(ModalView view) => view.GetParent()?.GetParent() as WorldScreen;

    /// <summary>Draw the pointer at a pixel of this screen without touching the system mouse (screenshots).
    /// With <paramref name="onButton"/>, it goes to the middle of the first button that can be pressed instead.</summary>
    public void ShowPointer(Vector2 px, bool onButton = false)
    {
        if (onButton && FirstButton(View) is { } b) px = b.GetGlobalRect().GetCenter();
        _cursor.Tier = View.Tier;
        _cursor.MoveTo(px, onButton ? FirstButton(View) : null);
    }

    private static BaseButton? FirstButton(Node n)
    {
        foreach (var c in n.GetChildren())
        {
            if (c is BaseButton { Disabled: false, Visible: true } b) return b;
            if (FirstButton(c) is { } found) return found;
        }
        return null;
    }

    /// <summary>Hide the system pointer while it is over this screen (the screen draws its own), show it again off the edge.</summary>
    private void PointerOnScreen(bool on, Vector2 px)
    {
        if (on)
        {
            _cursor.Tier = View.Tier;
            _cursor.MoveTo(px, _vp.GuiGetHoveredControl());
        }
        else _cursor.Visible = false;
        var want = on ? Input.MouseModeEnum.Hidden : Input.MouseModeEnum.Visible;
        if (Input.MouseMode != want && Input.MouseMode != Input.MouseModeEnum.Captured) Input.MouseMode = want;
    }

    public override void _Input(InputEvent e)
    {
        if (!View.IsOpen) return;
        if (e.IsActionPressed("cancel")) { Telo.Game.Audio.Sfx.Play("ui.back"); if (!View.OnCancel()) View.Close(); GetViewport().SetInputAsHandled(); return; }
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
        bool on = px.X >= 0 && px.X <= PxW && px.Y >= 0 && px.Y <= PxH;

        var copy = (InputEventMouse)m.Duplicate();
        copy.Position = px;
        copy.GlobalPosition = px;
        _vp.PushInput(copy, true);
        if (m is InputEventMouseMotion) PointerOnScreen(on, px);
        GetViewport().SetInputAsHandled();
    }
}
