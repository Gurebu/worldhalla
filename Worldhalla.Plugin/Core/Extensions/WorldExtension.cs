using System.Runtime.CompilerServices;

namespace Worldhalla.Plugin.Core.Extensions;

public static class WorldExtension
{
    private class ExtensionData
    {
        public GeneratorBinding? generatorBinding;
    }

    private static readonly ConditionalWeakTable<World, ExtensionData> DataStore = new();

    private static ExtensionData GetData(this World world)
    {
        return DataStore.GetValue(world, _ => new ExtensionData());
    }

    public static GeneratorBinding? GetGenerator(this World world)
    {
        return world.GetData().generatorBinding;
    }

    public static void SetGenerator(this World world, GeneratorBinding? generator)
    {
        world.GetData().generatorBinding = generator;
    }
}
