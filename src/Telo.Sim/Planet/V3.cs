namespace Telo.Sim.Planet;

/// <summary>Minimal double-precision vector for the pure sim (no engine types here).</summary>
public readonly record struct V3(double X, double Y, double Z)
{
    public static V3 operator +(V3 a, V3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static V3 operator -(V3 a, V3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static V3 operator *(V3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);
    public double Dot(V3 o) => X * o.X + Y * o.Y + Z * o.Z;
    public V3 Cross(V3 o) => new(Y * o.Z - Z * o.Y, Z * o.X - X * o.Z, X * o.Y - Y * o.X);
    public double Length => Math.Sqrt(Dot(this));
    public V3 Normalized() { double l = Length; return l < 1e-12 ? new V3(0, 1, 0) : this * (1.0 / l); }
}
