using UnityEngine;
using Worldhalla.API;
using Worldhalla.API.World;

namespace Worldhalla.Plugin.Core;

public sealed class GeneratedWorld(IWorldState state, bool biomeBlending)
{
    private const float FOREST_FACTOR_MULTIPLIER = 2.15f;

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

    public float ForestFactor(Vector3 position)
    {
        return FOREST_FACTOR_MULTIPLIER * (1 - State.GetForestation(position.x, position.z));
    }
}
