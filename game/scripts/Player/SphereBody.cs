using Godot;

namespace Telo.Game.Player;

/// <summary>
/// A body that lives on a planet: "down" is toward the planet centre (the world origin),
/// it stays upright relative to the ground, and it floats at the ocean surface.
/// </summary>
public abstract partial class SphereBody : CharacterBody3D
{
    public const float Gravity = 9.8f;

    /// <summary>Sea-level radius of the planet in metres.</summary>
    public float SeaRadius { get; set; } = 2500;
    /// <summary>How far below sea level the origin sits when floating.</summary>
    protected float FloatDepth = 1.3f;

    protected float VerticalSpeed;
    public bool InWater { get; private set; }

    public Vector3 Up => GlobalPosition.Normalized();
    public Vector3 Forward => -GlobalBasis.Z;
    public float Altitude => GlobalPosition.Length() - SeaRadius;

    /// <summary>Re-square the body so its Y axis points away from the planet, keeping its heading.</summary>
    protected void AlignToPlanet()
    {
        var up = Up;
        var fwd = Forward - up * Forward.Dot(up);
        if (fwd.LengthSquared() < 1e-6f) fwd = up.Cross(Vector3.Right);
        fwd = fwd.Normalized();
        var right = fwd.Cross(up).Normalized();
        GlobalBasis = new Basis(right, up, -fwd);
        UpDirection = up;
    }

    protected void Turn(float radians) => GlobalBasis = GlobalBasis.Rotated(Up, radians).Orthonormalized();

    /// <summary>Move with planet gravity. <paramref name="tangential"/> is the desired velocity along the ground.</summary>
    protected void MoveOnPlanet(Vector3 tangential, double delta)
    {
        var up = Up;
        float r = GlobalPosition.Length();
        InWater = r < SeaRadius - FloatDepth + 0.25f;
        if (r < SeaRadius - FloatDepth) VerticalSpeed = Mathf.Max(VerticalSpeed * 0.8f, (SeaRadius - FloatDepth - r) * 3f);
        else if (IsOnFloor() && VerticalSpeed <= 0) VerticalSpeed = -0.5f;
        else VerticalSpeed -= Gravity * (float)delta;

        Velocity = tangential + up * VerticalSpeed;
        MoveAndSlide();
        AlignToPlanet();
    }

    /// <summary>Put the body at a world position, standing upright, facing roughly <paramref name="facing"/>.</summary>
    public void PlaceAt(Vector3 position, Vector3 facing)
    {
        GlobalPosition = position;
        var up = Up;
        var fwd = (facing - up * facing.Dot(up)).Normalized();
        GlobalBasis = new Basis(fwd.Cross(up).Normalized(), up, -fwd);
        VerticalSpeed = 0;
        Velocity = Vector3.Zero;
    }
}
