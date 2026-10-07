using DotnetNoise;
using Worldhalla.API.World;

namespace GeneratorSample;

public class SampleWorld : IWorldState
{
    private const float EDGE_START = 9400f;
    private const float EDGE_END = 10300f;

    private const float ISLAND_SIZE = 1450f;
    private const float SEA_THRESHOLD = 0.08f;
    private const float HEIGHT_SCALE = 320f;
    private const float MIN_HEIGHT = -90f;
    private const float SPAWN_RADIUS = 1200f;
    private const float SPAWN_SHAPE = 0.2f;

    private const float POLE_OFFSET = 4000f;
    private const float POLE_DISTANCE = 12000f;
    private const float POLE_WAVE = 100f;
    private const float POLE_LIFT = 0.3f;
    private const float POLE_LIFT_DISTANCE = 1500f;
    private const float GAP_WIDTH = 400f;
    private const float GAP_DEPTH = -40f;

    private const float MOUNTAIN_HEIGHT = 85f;
    private const float SWAMP_INNER = 2000f;
    private const float SWAMP_OUTER = 6500f;
    private const float SWAMP_THRESHOLD = 0.05f;
    private const float SWAMP_FADE = 0.15f;
    private const float SWAMP_LEVEL = 1.5f;
    private const float BIOME_JITTER = 1200f;

    private readonly FastNoise land;
    private readonly FastNoise detail;
    private readonly FastNoise rings;
    private readonly FastNoise variety;
    private readonly FastNoise ground;
    private readonly FastNoise forest;
    private readonly FastNoise lava;
    private readonly FastNoise swamps;

    public SampleWorld(int seed, float islandScale)
    {
        land = Perlin(seed, ISLAND_SIZE * islandScale, 5);
        detail = Perlin(seed + 1, 60f, 3);
        rings = Perlin(seed + 2, 1500f, 3);
        variety = Perlin(seed + 3, 1000f, 1);
        ground = Perlin(seed + 4, 200f, 2);
        forest = Perlin(seed + 5, 400f, 3);
        lava = Perlin(seed + 6, 300f, 3);
        swamps = Perlin(seed + 7, 700f, 2);
    }

    public Biome GetBiome(float x, float y)
    {
        if (PoleDistance(x, y, POLE_OFFSET) > 0f)
            return Biome.AshLands;

        var height = Height(x, y);
        if (height < 0f)
            return Biome.Ocean;

        if (PoleDistance(x, y, -POLE_OFFSET) > 0f)
            return Biome.DeepNorth;

        if (height > MOUNTAIN_HEIGHT)
            return Biome.Mountain;

        var ring = Ring(x, y);
        if (Swampiness(x, y, ring) > 0.5f)
            return Biome.Swamp;

        var choice = variety.GetNoise(x, y);
        if (ring < 2000f)
            return Biome.Meadows;
        if (ring < 4500f)
            return choice > -0.25f ? Biome.BlackForest : Biome.Meadows;
        if (ring < 7000f)
            return choice > -0.3f ? Biome.Plains : Biome.BlackForest;
        return choice > -0.1f ? Biome.Mistlands : Biome.Plains;
    }

    public float GetForestation(float x, float y)
    {
        return Clamp01(0.5f + 0.8f * forest.GetNoise(x, y));
    }

    public IWorldState.PointData RenderPoint(float x, float y, Biome biome)
    {
        var height = Height(x, y);

        return new IWorldState.PointData
        {
            biome = biome,
            height = height,
            lava = biome == Biome.AshLands && height > 2f ? Clamp01(4f * (lava.GetNoise(x, y) - 0.25f)) : 0f,
            fertility = biome == Biome.Mistlands ? Clamp01(0.5f + ground.GetNoise(x, y)) : 0f,
        };
    }

    private float Height(float x, float y)
    {
        var radius = Length(x, y);
        var ashlands = PoleDistance(x, y, POLE_OFFSET);
        var deepNorth = PoleDistance(x, y, -POLE_OFFSET);

        var shape = land.GetNoise(x, y);
        shape += (SPAWN_SHAPE - shape) * SmoothStep(1f - radius / SPAWN_RADIUS);
        shape += POLE_LIFT * SmoothStep(Math.Max(ashlands, deepNorth) / POLE_LIFT_DISTANCE);
        shape -= SmoothStep((radius - EDGE_START) / (EDGE_END - EDGE_START));

        var height = Math.Max(MIN_HEIGHT, (shape - SEA_THRESHOLD) * HEIGHT_SCALE);
        height += 3f * detail.GetNoise(x, y);

        // Swamps sink their land to just above the water, keeping the coastline where it was. The slope down to them
        // lies outside the swamp, which is flat by the time GetBiome calls it one
        if (height > 0f)
        {
            var swampHeight = Math.Min(height, SWAMP_LEVEL * (1f + detail.GetNoise(x, y)) + 0.2f);
            height += (swampHeight - height) * Clamp01(2f * Swampiness(x, y, Ring(x, y)));
        }

        var gap = SmoothStep(Math.Min(Math.Abs(ashlands), Math.Abs(deepNorth)) / GAP_WIDTH);
        return Math.Min(height, GAP_DEPTH + (height - GAP_DEPTH) * gap);
    }

    private static FastNoise Perlin(int seed, float featureSize, int octaves)
    {
        return new FastNoise(seed)
        {
            UsedNoiseType = octaves > 1 ? FastNoise.NoiseType.PerlinFractal : FastNoise.NoiseType.Perlin,
            Frequency = 1f / featureSize,
            Octaves = octaves,
        };
    }

    private float Ring(float x, float y)
    {
        return Length(x, y) + BIOME_JITTER * rings.GetNoise(x, y);
    }

    private float Swampiness(float x, float y, float ring)
    {
        if (ring < SWAMP_INNER || ring > SWAMP_OUTER)
            return 0f;

        return SmoothStep((swamps.GetNoise(x, y) - SWAMP_THRESHOLD) / SWAMP_FADE);
    }

    // Positive past the pole's boundary, as vanilla's WorldGenerator.IsAshlands and IsDeepnorth draw it
    private static float PoleDistance(float x, float y, float offset)
    {
        var wave = (float)Math.Sin(Math.Atan2(x, y) * 20.0) * POLE_WAVE;
        return Length(x, y - offset) - (POLE_DISTANCE + wave);
    }

    private static float Length(float x, float y) => (float)Math.Sqrt(x * x + y * y);

    private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));

    private static float SmoothStep(float t)
    {
        t = Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
