using System.Runtime.CompilerServices;

namespace Worldhalla.Plugin.Core.Extensions;

public static class WorldGeneratorExtension
{
    private static readonly ConditionalWeakTable<WorldGenerator, WorldStateWrapper> StateStore = new();

    public static void SetState(this WorldGenerator worldGenerator, WorldStateWrapper state)
    {
        StateStore.AddOrUpdate(worldGenerator, state);
    }

    public static WorldStateWrapper? GetState(this WorldGenerator? worldGenerator)
    {
        if (worldGenerator == null)
            return null;

        StateStore.TryGetValue(worldGenerator, out var state);

        return state;
    }
}
