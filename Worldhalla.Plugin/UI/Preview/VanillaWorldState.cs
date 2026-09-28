using Worldhalla.API.World;
using Worldhalla.Plugin.UI.Preview.Vanilla;

namespace Worldhalla.Plugin.UI.Preview;

internal sealed class VanillaWorldState : IWorldState
{
    private const float WATER_LEVEL = 30f;

    private readonly WorldGenerator generator;

    private VanillaWorldState(WorldGenerator generator)
    {
        this.generator = generator;
    }

    // Must be called on the main thread: the constructor seeds through UnityEngine.Random. Constructing as a menu
    // world skips river placement there, which then runs on a preview thread through VanillaRiverPlacement.
    public static Task<IWorldState> Generate(string seed)
    {
        var world = new World("", seed) { m_menu = true };
        var generator = new WorldGenerator(world);
        world.m_menu = false;

        var state = new VanillaWorldState(generator);
        if (!VanillaRiverPlacement.Prepare())
            return Task.FromResult<IWorldState>(state);

        return Task.Run<IWorldState>(() =>
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

    public IWorldState.PointData RenderPoint(float x, float y)
    {
        var biome = generator.GetBiome(x, y);
        return new IWorldState.PointData
        {
            biome = (Biome)biome,
            height = generator.GetBiomeHeight(biome, x, y, out _) - WATER_LEVEL,
        };
    }
}
