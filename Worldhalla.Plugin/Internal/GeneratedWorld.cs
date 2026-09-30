using Worldhalla.API;
using Worldhalla.API.World;

namespace Worldhalla.Plugin.Internal;

internal sealed class GeneratedWorld(IWorldState state, bool biomeBlending)
{
    public IWorldState State { get; } = state;

    public bool BiomeBlending { get; } = biomeBlending;

    public static GeneratedWorld Of(IWorldGenerator worldgen, IWorldState state)
    {
        return new GeneratedWorld(state, worldgen.Features.Contains(IWorldGenerator.Feature.BiomeBlending));
    }

    public IWorldState.PointData RenderPoint(float x, float y)
    {
        return State.RenderPoint(x, y, BiomeBlending ? Biome.None : State.GetBiome(x, y));
    }
}
