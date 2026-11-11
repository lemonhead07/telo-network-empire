using Godot;
using Telo.Game.World;

namespace Telo.Game.Player;

/// <summary>Minimal first-person walker with a look-at interaction ray. Freezes while a modal view is open.</summary>
public partial class FirstPersonController : CharacterBody3D
{
    public const float WalkSpeed = 2.6f, SprintSpeed = 4.4f, JumpVelocity = 3.6f, Gravity = 9.8f, MouseSensitivity = 0.0025f;

    public Camera3D Camera { get; private set; } = null!;
    private RayCast3D _ray = null!;

    public Func<bool> IsFrozen { get; set; } = () => false;

    /// <summary>What the player is looking at right now (null if nothing interactable).</summary>
    public Interactable? Focus { get; private set; }

    public override void _Ready()
    {
        AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.3f, Height = 1.75f }, Position = new Vector3(0, 0.875f, 0) });
        Camera = new Camera3D { Position = new Vector3(0, 1.62f, 0), Fov = 72, Current = true };
        AddChild(Camera);
        _ray = new RayCast3D { TargetPosition = new Vector3(0, 0, -2.2f), CollideWithBodies = true };
        Camera.AddChild(_ray);
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (IsFrozen()) return;
        if (e is InputEventMouseMotion mm && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateY(-mm.Relative.X * MouseSensitivity);
            var r = Camera.Rotation;
            r.X = Mathf.Clamp(r.X - mm.Relative.Y * MouseSensitivity, -1.4f, 1.4f);
            Camera.Rotation = r;
        }
        else if (e is InputEventMouseButton { Pressed: true } && Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else if (e.IsActionPressed("interact") && Focus?.OnUse is { } use)
        {
            use();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var v = Velocity;
        if (!IsOnFloor()) v.Y -= Gravity * (float)delta;

        if (IsFrozen())
        {
            v.X = 0; v.Z = 0; Velocity = v; MoveAndSlide(); Focus = null;
            return;
        }

        if (Input.IsActionJustPressed("jump") && IsOnFloor()) v.Y = JumpVelocity;
        var input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        var dir = (Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();
        float speed = Input.IsActionPressed("sprint") ? SprintSpeed : WalkSpeed;
        v.X = dir.X * speed; v.Z = dir.Z * speed;
        Velocity = v;
        MoveAndSlide();

        Focus = _ray.IsColliding() ? _ray.GetCollider() as Interactable : null;
    }
}
