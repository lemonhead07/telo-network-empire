using Godot;
using Telo.Game.World;

namespace Telo.Game.Player;

/// <summary>
/// The service van. Arcade handling: it grips the planet, slows on steep ground, and wallows if you drive into the sea.
/// Steep slopes stop it — terrain is the obstacle that roads will later solve.
/// </summary>
public partial class Van : SphereBody
{
    public const float MaxSpeed = 22f, OffRoadSpeed = 11f, ReverseSpeed = 7f, Accel = 9f, Brake = 16f, WaterSpeed = 2.2f, TurnRate = 1.7f;

    public Camera3D Camera { get; private set; } = null!;
    public Interactable Door { get; private set; } = null!;
    public bool Driven { get; private set; }
    public float Speed { get; private set; }
    private Node3D _visual = null!;
    /// <summary>Is this world position on a road or in a settlement? Off-road the van is half as fast.</summary>
    public Func<Vector3, bool> IsPaved { get; set; } = _ => true;
    public bool OnRoad { get; private set; } = true;

    public override void _Ready()
    {
        FloatDepth = 0.5f;
        AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 1.05f }, Position = new Vector3(0, 1.05f, 0) });
        FloorMaxAngle = Mathf.DegToRad(36);
        FloorSnapLength = 0.6f;

        var cream = new Color(0.86f, 0.82f, 0.68f); var dark = new Color(0.12f, 0.12f, 0.13f);
        var lp = new LowPoly()
            .Box(new Vector3(0, 1.15f, 0.3f), new Vector3(1.9f, 1.5f, 3.6f), cream)                    // cargo body
            .Box(new Vector3(0, 0.95f, -1.95f), new Vector3(1.9f, 1.1f, 0.9f), cream * 0.97f)           // nose
            .Box(new Vector3(0, 1.5f, -1.52f), new Vector3(1.7f, 0.6f, 0.08f), new Color(0.25f, 0.35f, 0.42f)) // windscreen
            .Box(new Vector3(0, 0.95f, 0.3f), new Vector3(1.94f, 0.22f, 3.62f), new Color(0.72f, 0.28f, 0.16f)) // stripe
            .Box(new Vector3(0, 0.42f, -2.42f), new Vector3(1.9f, 0.18f, 0.12f), dark)                  // bumper
            .Box(new Vector3(-0.6f, 0.82f, -2.41f), new Vector3(0.3f, 0.18f, 0.06f), new Color(1f, 0.95f, 0.7f))
            .Box(new Vector3(0.6f, 0.82f, -2.41f), new Vector3(0.3f, 0.18f, 0.06f), new Color(1f, 0.95f, 0.7f))
            .Box(new Vector3(0, 2.0f, 0.3f), new Vector3(1.2f, 0.08f, 2.4f), new Color(0.5f, 0.5f, 0.52f)); // roof rack (ladder goes here)
        foreach (var (x, z) in new[] { (-0.95f, -1.5f), (0.95f, -1.5f), (-0.95f, 1.4f), (0.95f, 1.4f) })
            lp.Box(new Vector3(x, 0.38f, z), new Vector3(0.3f, 0.76f, 0.76f), dark);
        _visual = lp.Instance("Body");
        AddChild(_visual);

        Camera = new Camera3D { Position = new Vector3(0, 3.6f, 8.2f), RotationDegrees = new Vector3(-12, 0, 0), Fov = 70, Near = 0.1f, Far = 60000 };
        AddChild(Camera);

        // Interaction shell on layer 2 so it never blocks movement, only the player's look ray.
        Door = new Interactable { CollisionLayer = 2, CollisionMask = 0 };
        Door.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2.2f, 2f, 4.8f) }, Position = new Vector3(0, 1.1f, 0) });
        AddChild(Door);
    }

    public void SetDriven(bool driven)
    {
        Driven = driven;
        Camera.Current = driven;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        float throttle = Driven ? Input.GetAxis("move_back", "move_forward") : 0;
        float steer = Driven ? Input.GetAxis("move_right", "move_left") : 0;

        OnRoad = IsPaved(GlobalPosition);
        float target = throttle > 0 ? (OnRoad ? MaxSpeed : OffRoadSpeed) * throttle : ReverseSpeed * throttle;
        if (InWater) target = Mathf.Clamp(target, -WaterSpeed, WaterSpeed);
        float rate = Mathf.Abs(target) < Mathf.Abs(Speed) || Mathf.Sign(target) != Mathf.Sign(Speed) ? Brake : Accel;
        Speed = Mathf.MoveToward(Speed, target, rate * dt);

        Turn(steer * TurnRate * dt * Mathf.Clamp(Speed / 6f, -1f, 1f));
        MoveOnPlanet(Forward * Speed, delta);

        // What we actually achieved (walls and steep slopes eat speed).
        float actual = Velocity.Dot(Forward);
        if (Mathf.Abs(actual) < Mathf.Abs(Speed)) Speed = Mathf.Lerp(Speed, actual, 0.3f);

        // Lean the body to the ground it is on.
        var n = IsOnFloor() ? GlobalBasis.Inverse() * GetFloorNormal() : Vector3.Up;
        _visual.Quaternion = _visual.Quaternion.Slerp(new Quaternion(Vector3.Up, n.Normalized()), Mathf.Min(1, 8 * dt));
    }
}
