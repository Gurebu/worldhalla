namespace Worldhalla.Plugin.UI.Preview.Vanilla;

// Xorshift128 as UnityEngine.Random implements it, reproducing the engine bit for bit
public sealed class UnityRandom
{
    private const uint SEED_MULTIPLIER = 1812433253u;
    private const uint UNIT_MASK = 0x7FFFFF;
    private const float UNIT_RECIPROCAL = 1f / 8388607f;

    private uint s0, s1, s2, s3;

    public (uint, uint, uint, uint) State
    {
        get => (s0, s1, s2, s3);
        set => (s0, s1, s2, s3) = value;
    }

    public void InitState(int seed)
    {
        s0 = (uint)seed;
        s1 = unchecked(s0 * SEED_MULTIPLIER + 1u);
        s2 = unchecked(s1 * SEED_MULTIPLIER + 1u);
        s3 = unchecked(s2 * SEED_MULTIPLIER + 1u);
    }

    public uint Next()
    {
        var t = s0 ^ (s0 << 11);
        s0 = s1;
        s1 = s2;
        s2 = s3;
        s3 = s3 ^ (s3 >> 19) ^ t ^ (t >> 8);
        return s3;
    }

    public int Range(int min, int max)
    {
        if (min > max)
            throw new ArgumentException($"Reversed range [{min}, {max}) has not been checked against the engine");
        if (min == max)
            return min;
        return unchecked(min + (int)(Next() % (uint)(max - min)));
    }

    // The engine interpolates from max down to min. Every intermediate is cast back to float, so the result doesn't
    // depend on whether the JIT keeps extra precision.
    public float Range(float min, float max)
    {
        var t = (float)((Next() & UNIT_MASK) * UNIT_RECIPROCAL);
        return (float)((float)(t * min) + (float)((float)(1f - t) * max));
    }
}
