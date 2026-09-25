using System.Runtime.CompilerServices;
using Worldhalla.API;

namespace Worldhalla.Plugin.Extensions;

public static class WorldExtension
{
    private class ExtensionData
    {
        public IWorldGenerator? worldgen;
        public ISettings? settings;
    }

    private static readonly ConditionalWeakTable<World, ExtensionData> DataStore = new();
    
    private static ExtensionData GetData(this World world)
    {
        return DataStore.GetValue(world, _ => new ExtensionData());
    }
    
    public static ISettings GetSettings(this World world)
    {
        return world.GetData().settings ?? world.GetData().worldgen?.DefaultSettings;
    }
    
    public static void SetSettings(this World world, ISettings settings)
    {
        world.GetData().settings = settings;
    }
    
    public static void SetWorldgen(this World world, IWorldGenerator worldgen)
    {
        world.GetData().worldgen = worldgen;
    }

    public static IWorldGenerator? GetWorldgen(this World world)
    {
        return world.GetData().worldgen;
    }
    
    public static bool IsVanillaWorld(this World world) => world.GetData().worldgen == null;
}
