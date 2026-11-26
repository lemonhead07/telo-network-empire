using Godot;
using Telo.Game.Audio;
using Telo.Game.World;

namespace Telo.Game.Player;

/// <summary>First-person walker on the planet, with a look-at interaction ray and a survey (fly) mode for development.</summary>
public partial class FirstPersonController : SphereBody
{
    public const float WalkSpeed = 2.8f, SprintSpeed = 5.2f, SwimSpeed = 1.7f, JumpVelocity = 3.8f;
    public const float FlySpeed = 45f, FlyFastSpeed = 260f;

    public Camera3D Camera { get; private set; } = null!;
    private RayCast3D _ray = null!, _look = null!;
    /// <summary>
    /// Things you can read from further off than arm's length (people in the street) sit on this physics layer alone. The long
    /// look ray finds them; it stops at walls and the ground, and anything within reach (a door) still comes first.
    /// </summary>
    public const uint LookLayer = 4;
    public const float LookReach = 14f;
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

    // ---- sound (EA7): a footstep for each stride walked on the ground, and the sounds of holding E to work ----------------
    /// <summary>Metres between footsteps.</summary>
    public const float WalkStride = 0.75f, SprintStride = 1.0f;
    /// <summary>What the ground sounds like where you are (a footstep's sound id). Set by GameRoot; asked once a step.</summary>
    public Func<Vector3, string> StepSound { get; set; } = _ => "step.grass";
    private float _stride, _air, _workIn;
    private bool _leftFoot;
    private Vector3 _lastFeet;
    private static readonly Dictionary<string, float> SoundLength = new();

    public override void _Ready()
    {
        _shape = new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.3f, Height = 1.75f }, Position = new Vector3(0, 0.875f, 0) };
        AddChild(_shape);
        Camera = new Camera3D { Position = new Vector3(0, 1.62f, 0), Fov = 72, Current = true, Near = 0.08f, Far = 60000 };
        AddChild(Camera);
        _ray = new RayCast3D { TargetPosition = new Vector3(0, 0, -2.6f), CollisionMask = 3 };
        Camera.AddChild(_ray);
        _look = new RayCast3D { TargetPosition = new Vector3(0, 0, -LookReach), CollisionMask = 1 | LookLayer };
        Camera.AddChild(_look);
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
            float k = GameSettings.MouseSensitivity;   // the player's settings page
            Turn(-mm.Relative.X * k);
            var r = Camera.Rotation;
            r.X = Mathf.Clamp(r.X - mm.Relative.Y * k * (GameSettings.InvertY ? -1 : 1), -1.5f, 1.5f);
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
            _holding?.OnHold?.Invoke(0); _holding = null; _held = 0;
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
            _air = 0; _lastFeet = GlobalPosition;   // no footsteps in the air
        }
        else
        {
            if (Input.IsActionJustPressed("jump") && (IsOnFloor() || InWater)) VerticalSpeed = JumpVelocity;
            var dir = (GlobalBasis * new Vector3(input.X, 0, input.Y)).Normalized();
            float speed = InWater ? SwimSpeed : Input.IsActionPressed("sprint") ? SprintSpeed : WalkSpeed;
            MoveOnPlanet(dir * speed * Pace, delta);
            Footsteps((float)delta, Input.IsActionPressed("sprint"));
        }
        Focus = _ray.IsColliding() ? _ray.GetCollider() as Interactable : null;
        if (Focus is null && _look.IsColliding() && _look.GetCollider() is Interactable far && (far.CollisionLayer & LookLayer) != 0) Focus = far;

        // hold E to work: the job is done when the hold completes; keep holding to carry straight on
        if (Focus is { } f && Input.IsActionPressed("interact") && f.CanUse() && f.HoldSeconds() is var need and > 0)
        {
            if (f != _holding) { _holding?.OnHold?.Invoke(0); _holding = f; _held = 0; _workIn = WorkSound(f.StartSound, null, f); }
            _workIn -= (float)delta;
            if (_workIn <= 0) _workIn = WorkSound(f.HoldSound, "work.tick", f);
            _held += (float)delta;
            HoldProgress = Mathf.Clamp(_held / need, 0, 1);
            f.OnHold?.Invoke(HoldProgress);   // the work shows on the thing (WorkPoints: the piece turns, sinks, slides home)
            if (_held >= need) { _held = 0; HoldProgress = 0; WorkSound(f.DoneSound, "work.done", f); _workIn = 0.35f; f.OnUse?.Invoke(); }
        }
        else { _holding?.OnHold?.Invoke(0); _holding = null; _held = 0; HoldProgress = 0; }
    }

    /// <summary>A footstep each stride along the ground (none in the air or the water); a thud on landing from a jump. In your ear.</summary>
    private void Footsteps(float delta, bool sprint)
    {
        var at = GlobalPosition;
        var moved = at - _lastFeet;
        _lastFeet = at;
        float d = (moved - Up * moved.Dot(Up)).Length();
        if (d > 1.5f) d = 0;   // put somewhere (out of the van, into bed, off the ferry): not a step
        if (InWater) { _air = 0; return; }
        if (!IsOnFloor()) { _air += delta; return; }
        if (_air > 0.35f) { Sfx.Play("step.land", db: -2); _stride = 0; }
        _air = 0;
        _stride += d;
        float every = sprint ? SprintStride : WalkStride;
        if (_stride < every) return;
        _stride = Mathf.Min(_stride - every, every);
        _leftFoot = !_leftFoot;
        Sfx.Play(StepSound(at), db: sprint ? 0 : -2, pitch: _leftFoot ? 0.97f : 1.03f);
    }

    /// <summary>Play a work sound from the thing being worked (unset: <paramref name="usual"/>). Returns the seconds before the next may play.</summary>
    private static float WorkSound(Func<string?>? sound, string? usual, Interactable at)
    {
        if ((sound is null ? usual : sound()) is not { } id) return 0;
        Sfx.Play(id, at.GlobalPosition);
        if (!SoundLength.TryGetValue(id, out var len))
            SoundLength[id] = len = Sfx.StreamsFor(id) is { Length: > 0 } w ? (float)w.Average(x => x.GetLength()) : 0;
        return Mathf.Max(0.3f, len + 0.04f);   // one after another: a long sound (a drill, a spade) isn't stacked on itself
    }
}
