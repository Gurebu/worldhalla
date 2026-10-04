using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using Worldhalla.API;
using Worldhalla.API.World;

namespace Worldhalla.Plugin.Core;

public sealed class WorldStateWrapper(IWorldState state, bool biomeBlending)
{
    private const float FOREST_FACTOR_MULTIPLIER = 2.15f;

    public IWorldState State { get; } = state;
    
    [SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
    public IWorldGeography? Geography { get; } = state as IWorldGeography;

    public bool BiomeBlending { get; } = biomeBlending;

    public static WorldStateWrapper Of(RegisteredGenerator generator, IWorldState state)
    {
        return new WorldStateWrapper(state, generator.BiomeBlending);
    }

    public float ForestFactor(Vector3 position)
    {
        return FOREST_FACTOR_MULTIPLIER * (1 - State.GetForestation(position.x, position.z));
    }
}
