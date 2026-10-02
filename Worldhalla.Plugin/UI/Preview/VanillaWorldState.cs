using Worldhalla.API.World;
using Worldhalla.Plugin.Core;
using Worldhalla.Plugin.UI.Preview.Vanilla;

namespace Worldhalla.Plugin.UI.Preview;

public sealed class VanillaWorldState : IWorldState
{
    private const float WATER_LEVEL = 30f;

    private readonly WorldGenerator generator;

    public WorldGenerator Generator => generator;

    private VanillaWorldState(WorldGenerator generator)
    {
        this.generator = generator;
    }

    // Must be called on the main thread: the constructor seeds through UnityEngine.Random. Constructing as a menu
    // world skips river placement there, which then runs on a preview thread through VanillaRiverPlacement.
    public static Task<GeneratedWorld> Generate(string seed)
    {
        var world = new World("", seed) { m_menu = true };
        var generator = new WorldGenerator(world);
        world.m_menu = false;

        var state = new GeneratedWorld(new VanillaWorldState(generator), biomeBlending: false);
        if (!VanillaRiverPlacement.Prepare())
            return Task.FromResult(state);

        return Task.Run(() =>
        {
            VanillaRiverPlacement.Pregenerate(generator);
            return state;
        });
    }

    public Biome GetBiome(float x, float y)
    {
        return (Biome)generator.GetBiome(x, y);
    }

    public float GetForestation(float x, float y)
    {
        return 0f;
    }

    public IWorldState.PointData RenderPoint(float x, float y, Biome biome)
    {
        return new IWorldState.PointData
        {
            biome = biome,
            height = generator.GetBiomeHeight((Heightmap.Biome)biome, x, y, out _) - WATER_LEVEL,
        };
    }
}
