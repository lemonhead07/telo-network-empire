using Godot;
using Telo.Game.World;

namespace Telo.Game.Player;

/// <summary>First-person walker on the planet, with a look-at interaction ray and a survey (fly) mode for development.</summary>
public partial class FirstPersonController : SphereBody
{
    public const float WalkSpeed = 2.8f, SprintSpeed = 5.2f, SwimSpeed = 1.7f, JumpVelocity = 3.8f, MouseSensitivity = 0.0025f;
    public const float FlySpeed = 45f, FlyFastSpeed = 260f;

    public Camera3D Camera { get; private set; } = null!;
    private RayCast3D _ray = null!;
    private CollisionShape3D _shape = null!;

    public Func<bool> IsFrozen { get; set; } = () => false;
    public bool Flying { get; private set; }
    public bool Active { get; private set; } = true;
    /// <summary>Walking speed multiplier (carrying something heavy slows you down).</summary>
    public float Pace { get; set; } = 1f;

    /// <summary>What the player is looking at right now (null if nothing interactable).</summary>
    public Interactable? Focus { get; private set; }
    /// <summary>How far through the current hold-to-work action (0 to 1).</summary>
    public float HoldProgress { get; private set; }
    private Interactable? _holding;
    private float _held;

    public override void _Ready()
    {
        _shape = new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.3f, Height = 1.75f }, Position = new Vector3(0, 0.875f, 0) };
        AddChild(_shape);
        Camera = new Camera3D { Position = new Vector3(0, 1.62f, 0), Fov = 72, Current = true, Near = 0.08f, Far = 60000 };
        AddChild(Camera);
        _ray = new RayCast3D { TargetPosition = new Vector3(0, 0, -2.6f), CollisionMask = 3 };
        Camera.AddChild(_ray);
        FloorMaxAngle = Mathf.DegToRad(46);
        FloorSnapLength = 0.4f;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    /// <summary>Switch the on-foot player off (while driving) or back on.</summary>
    public void SetActive(bool active)
    {
        Active = active;
        Visible = active;
        _shape.Disabled = !active;
        Camera.Current = active;
        if (!active) Focus = null;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Active || IsFrozen()) return;
        if (e is InputEventMouseMotion mm && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            Turn(-mm.Relative.X * MouseSensitivity);
            var r = Camera.Rotation;
            r.X = Mathf.Clamp(r.X - mm.Relative.Y * MouseSensitivity, -1.5f, 1.5f);
            Camera.Rotation = r;
        }
        else if (e is InputEventMouseButton { Pressed: true } && Input.MouseMode != Input.MouseModeEnum.Captured)
            Input.MouseMode = Input.MouseModeEnum.Captured;
        else if (e.IsActionPressed("interact") && Focus?.OnUse is { } use)
        {
            if (Focus.HoldSeconds() <= 0) use();   // things that take time are done by holding (see _PhysicsProcess)
            GetViewport().SetInputAsHandled();
        }
        else if (e.IsActionPressed("alt_use") && Focus is { OnAlt: { } alt } && Focus.AltPrompt() is not null)
        {
            alt();
            GetViewport().SetInputAsHandled();
        }
        else if (e.IsActionPressed("fly")) { Flying = !Flying; VerticalSpeed = 0; }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Active) return;
        if (IsFrozen())
        {
            if (!Flying) MoveOnPlanet(Vector3.Zero, delta);
            Focus = null; HoldProgress = 0;
            return;
        }

        var input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        if (Flying)
        {
            // Survey mode: fly where you look. Still collides with the world.
            var dir = Camera.GlobalBasis * new Vector3(input.X, 0, input.Y);
            if (Input.IsActionPressed("jump")) dir += Up;
            Velocity = dir.Normalized() * (Input.IsActionPressed("sprint") ? FlyFastSpeed : FlySpeed);
            MoveAndSlide();
            AlignToPlanet();
        }
        else
        {
            if (Input.IsActionJustPressed("jump") && (IsOnFloor() || InWater)) VerticalSpeed = JumpVelocity;
            var dir = (GlobalBasis * new Vector3(input.X, 0, input.Y)).Normalized();
            float speed = InWater ? SwimSpeed : Input.IsActionPressed("sprint") ? SprintSpeed : WalkSpeed;
            MoveOnPlanet(dir * speed * Pace, delta);
        }
        Focus = _ray.IsColliding() ? _ray.GetCollider() as Interactable : null;

        // hold E to work: the job is done when the hold completes; keep holding to carry straight on
        if (Focus is { } f && Input.IsActionPressed("interact") && f.CanUse() && f.HoldSeconds() is var need and > 0)
        {
            if (f != _holding) { _holding = f; _held = 0; }
            _held += (float)delta;
            HoldProgress = Mathf.Clamp(_held / need, 0, 1);
            if (_held >= need) { _held = 0; HoldProgress = 0; f.OnUse?.Invoke(); }
        }
        else { _holding = null; _held = 0; HoldProgress = 0; }
    }
}
