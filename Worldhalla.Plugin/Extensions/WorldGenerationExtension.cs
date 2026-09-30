using System.Runtime.CompilerServices;
using Worldhalla.Plugin.Internal;

namespace Worldhalla.Plugin.Extensions;

public static class WorldGeneratorExtension
{
    private static readonly ConditionalWeakTable<WorldGenerator, GeneratedWorld> WorldStore = new();

    internal static void SetWorld(this WorldGenerator worldGenerator, GeneratedWorld world)
    {
        WorldStore.AddOrUpdate(worldGenerator, world);
    }

    internal static GeneratedWorld? GetWorld(this WorldGenerator? worldGenerator)
    {
        if (worldGenerator == null)
            return null;

        WorldStore.TryGetValue(worldGenerator, out var world);

        return world;
    }
}
