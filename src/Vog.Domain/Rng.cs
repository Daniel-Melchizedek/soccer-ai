namespace Vog.Domain;

/// <summary>
/// xoshiro256** seeded with SplitMix64. Used instead of <see cref="Random"/> so a seed produces the
/// same match on every .NET version and machine (golden matches depend on this).
/// </summary>
public sealed class Rng
{
    private ulong _s0, _s1, _s2, _s3;

    public Rng(ulong seed)
    {
        var sm = seed;
        _s0 = SplitMix(ref sm);
        _s1 = SplitMix(ref sm);
        _s2 = SplitMix(ref sm);
        _s3 = SplitMix(ref sm);
    }

    public ulong NextUInt64()
    {
        var result = RotateLeft(_s1 * 5, 7) * 9;
        var t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotateLeft(_s3, 45);
        return result;
    }

    /// <summary>Uniform in [0, 1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    public double Uniform(double min, double max) => min + (max - min) * NextDouble();

    public int NextInt(int maxExclusive) => (int)(NextDouble() * maxExclusive);

    public int NextInt(int minInclusive, int maxExclusive) => minInclusive + NextInt(maxExclusive - minInclusive);

    public bool Chance(double probability) => NextDouble() < probability;

    public double Gaussian(double mean = 0, double stdDev = 1)
    {
        var u1 = 1.0 - NextDouble();
        var u2 = NextDouble();
        return mean + stdDev * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    public T Pick<T>(IReadOnlyList<T> items) => items[NextInt(items.Count)];

    /// <summary>Picks an index with probability proportional to its (non-negative) weight.</summary>
    public int PickWeighted(IReadOnlyList<double> weights)
    {
        var total = 0.0;
        foreach (var w in weights)
        {
            total += w;
        }

        var r = NextDouble() * total;
        for (var i = 0; i < weights.Count; i++)
        {
            r -= weights[i];
            if (r <= 0)
            {
                return i;
            }
        }

        return weights.Count - 1;
    }

    public Rng Fork(ulong salt) => new(NextUInt64() ^ salt);

    private static ulong SplitMix(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        var z = x;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static ulong RotateLeft(ulong x, int k) => (x << k) | (x >> (64 - k));
}
