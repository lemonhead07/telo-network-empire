namespace Telo.Sim.Core;

/// <summary>
/// Deterministic SplitMix64 RNG. Same seed => same sequence on every platform and .NET version.
/// System.Random is banned in the sim because its algorithm is not guaranteed stable.
/// </summary>
public sealed class DetRng
{
    private ulong _state;

    public DetRng(ulong seed) => _state = seed;

    public ulong State => _state;

    public ulong NextU64()
    {
        ulong z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform int in [0, maxExclusive).</summary>
    public int Next(int maxExclusive)
    {
        if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return (int)(NextU64() % (ulong)maxExclusive);
    }

    public int Range(int minInclusive, int maxExclusive) => minInclusive + Next(maxExclusive - minInclusive);

    /// <summary>True with probability permille/1000. Integer maths keeps it deterministic.</summary>
    public bool ChancePermille(int permille) => Next(1000) < permille;

    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>Derive an independent seed for a sub-system (e.g. one minigame) without consuming this stream.</summary>
    public static ulong Derive(ulong seed, string salt)
    {
        ulong h = 1469598103934665603UL; // FNV-1a
        foreach (char c in salt) { h ^= c; h *= 1099511628211UL; }
        return new DetRng(seed ^ h).NextU64();
    }
}
