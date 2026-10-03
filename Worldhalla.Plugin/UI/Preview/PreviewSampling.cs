using Worldhalla.API.World;
using Worldhalla.Plugin.Core.Extensions;

namespace Worldhalla.Plugin.UI.Preview;

public static class PreviewSampling
{
    private const float WATER_LEVEL = 30f;
    private const float DEFAULT_WORLD_RADIUS = 10500f;

    // Asks what a chunk build asks. The mask's alpha is lava only in the ashlands; elsewhere it is fertility
    public static (float height, Biome biome, float lava) Sample(WorldGenerator generator, float x, float y)
    {
        var biome = generator.GetBiome(x, y);
        var height = generator.GetBiomeHeight(biome, x, y, out var mask) - WATER_LEVEL;

        return (height, (Biome)biome, biome == Heightmap.Biome.AshLands ? mask.a : 0f);
    }

    public static float WorldRadius(WorldGenerator generator)
    {
        return generator.GetWorld()?.Geography is { } geography ? geography.WorldSize / 2f : DEFAULT_WORLD_RADIUS;
    }
}
