using System.Runtime.CompilerServices;
using Worldhalla.API;

namespace Worldhalla.Plugin.Extensions;

public static class WorldGeneratorExtension
{
    private static readonly ConditionalWeakTable<WorldGenerator, IWorldState> StateStore = new();

    public static void SetState(this WorldGenerator worldGenerator, IWorldState worldState)
    {
        StateStore.AddOrUpdate(worldGenerator, worldState);
    }

    public static IWorldState? GetState(this WorldGenerator? worldGenerator)
    {
        if (worldGenerator == null)
            return null;

        StateStore.TryGetValue(worldGenerator, out var worldState);

        return worldState;
    }
}
