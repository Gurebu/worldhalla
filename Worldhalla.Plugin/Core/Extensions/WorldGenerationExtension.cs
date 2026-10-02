using System.Runtime.CompilerServices;

namespace Worldhalla.Plugin.Core.Extensions;

public static class WorldGeneratorExtension
{
    private static readonly ConditionalWeakTable<WorldGenerator, GeneratedWorld> WorldStore = new();

    public static void SetWorld(this WorldGenerator worldGenerator, GeneratedWorld world)
    {
        WorldStore.AddOrUpdate(worldGenerator, world);
    }

    public static GeneratedWorld? GetWorld(this WorldGenerator? worldGenerator)
    {
        if (worldGenerator == null)
            return null;

        WorldStore.TryGetValue(worldGenerator, out var world);

        return world;
    }
}
