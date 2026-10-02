using System.Runtime.CompilerServices;

namespace Worldhalla.Plugin.Core.Extensions;

public static class WorldExtension
{
    private class ExtensionData
    {
        public WorldgenBinding? worldgen;
    }

    private static readonly ConditionalWeakTable<World, ExtensionData> DataStore = new();

    private static ExtensionData GetData(this World world)
    {
        return DataStore.GetValue(world, _ => new ExtensionData());
    }

    public static WorldgenBinding? GetWorldgen(this World world)
    {
        return world.GetData().worldgen;
    }

    public static void SetWorldgen(this World world, WorldgenBinding? worldgen)
    {
        world.GetData().worldgen = worldgen;
    }
}
