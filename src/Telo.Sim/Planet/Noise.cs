namespace Telo.Sim.Planet;

/// <summary>
/// Deterministic 3D gradient (Perlin-style) noise built on integer hashing and + - * / only,
/// so the same seed gives the same planet on every machine.
/// </summary>
public static class Noise
{
    private static readonly (int X, int Y, int Z)[] Grad =
    {
        (1,1,0),(-1,1,0),(1,-1,0),(-1,-1,0),(1,0,1),(-1,0,1),(1,0,-1),(-1,0,-1),(0,1,1),(0,-1,1),(0,1,-1),(0,-1,-1),
    };

    private static uint Hash(int x, int y, int z, uint seed)
    {
        unchecked
        {
            uint h = seed;
            h ^= (uint)x * 0x8DA6B343u; h = (h << 13) | (h >> 19); h *= 0x9E3779B1u;
            h ^= (uint)y * 0xD8163841u; h = (h << 13) | (h >> 19); h *= 0x9E3779B1u;
            h ^= (uint)z * 0xCB1AB31Fu; h = (h << 13) | (h >> 19); h *= 0x9E3779B1u;
            h ^= h >> 15; h *= 0x85EBCA6Bu; h ^= h >> 13;
            return h;
        }
    }

    private static double Dot(int ix, int iy, int iz, double fx, double fy, double fz, uint seed)
    {
        var g = Grad[Hash(ix, iy, iz, seed) % 12];
        return g.X * fx + g.Y * fy + g.Z * fz;
    }

    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    /// <summary>Roughly in [-1, 1].</summary>
    public static double Perlin(V3 p, uint seed)
    {
        int ix = (int)Math.Floor(p.X), iy = (int)Math.Floor(p.Y), iz = (int)Math.Floor(p.Z);
        double fx = p.X - ix, fy = p.Y - iy, fz = p.Z - iz;
        double u = Fade(fx), v = Fade(fy), w = Fade(fz);
        double x00 = Lerp(Dot(ix, iy, iz, fx, fy, fz, seed), Dot(ix + 1, iy, iz, fx - 1, fy, fz, seed), u);
        double x10 = Lerp(Dot(ix, iy + 1, iz, fx, fy - 1, fz, seed), Dot(ix + 1, iy + 1, iz, fx - 1, fy - 1, fz, seed), u);
        double x01 = Lerp(Dot(ix, iy, iz + 1, fx, fy, fz - 1, seed), Dot(ix + 1, iy, iz + 1, fx - 1, fy, fz - 1, seed), u);
        double x11 = Lerp(Dot(ix, iy + 1, iz + 1, fx, fy - 1, fz - 1, seed), Dot(ix + 1, iy + 1, iz + 1, fx - 1, fy - 1, fz - 1, seed), u);
        return Lerp(Lerp(x00, x10, v), Lerp(x01, x11, v), w) * 1.1;
    }

    /// <summary>Fractal sum of octaves; result roughly in [-1, 1].</summary>
    public static double Fbm(V3 p, uint seed, int octaves, double lacunarity = 2.0, double gain = 0.5)
    {
        double sum = 0, amp = 1, norm = 0;
        for (int i = 0; i < octaves; i++)
        {
            sum += Perlin(p, seed + (uint)i * 1013u) * amp;
            norm += amp; amp *= gain; p *= lacunarity;
        }
        return sum / norm;
    }

    /// <summary>Ridged noise in [0, 1]: sharp crests, used for mountain chains.</summary>
    public static double Ridged(V3 p, uint seed, int octaves)
    {
        double sum = 0, amp = 1, norm = 0;
        for (int i = 0; i < octaves; i++)
        {
            double n = 1 - Math.Abs(Perlin(p, seed + (uint)i * 7919u));
            sum += n * n * amp; norm += amp; amp *= 0.5; p *= 2.0;
        }
        return sum / norm;
    }

    public static double Smooth(double a, double b, double x)
    {
        double t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
