using System.Runtime.CompilerServices;
using Worldhalla.API;
using Worldhalla.API.Settings;

namespace Worldhalla.Plugin.Core.Extensions;

public static class WorldExtension
{
    private class ExtensionData
    {
        public string? worldgenGuid;
        public IWorldGenerator? worldgen;
        public ISettings? settings;
        public byte[]? unreadSettings;
        public GeneratedWorld? generated;
        public bool legacyFormat;
    }

    private static readonly ConditionalWeakTable<World, ExtensionData> DataStore = new();
    
    private static ExtensionData GetData(this World world)
    {
        return DataStore.GetValue(world, _ => new ExtensionData());
    }
    
    public static ISettings? GetSettings(this World world)
    {
        return world.GetData().settings ?? world.GetData().worldgen?.GetDefaultSettings();
    }

    public static void SetSettings(this World world, ISettings? settings)
    {
        world.GetData().settings = settings;
    }

    public static byte[] SaveSettings(this World world)
    {
        if (world.GetData() is { worldgen: null, unreadSettings: { } unread })
            return unread;

        return world.GetSettings()?.SaveToBinaryBlob() ?? Array.Empty<byte>();
    }
    
    public static void SetWorldgen(this World world, string guid, IWorldGenerator worldgen)
    {
        world.GetData().worldgenGuid = guid;
        world.GetData().worldgen = worldgen;
    }

    // The blob stays as read, so saving the world without its generator installed keeps its settings
    public static void SetUnknownWorldgen(this World world, string guid, byte[] settings)
    {
        world.GetData().worldgenGuid = guid;
        world.GetData().unreadSettings = settings;
    }

    public static string? GetWorldgenGuid(this World world)
    {
        return world.GetData().worldgenGuid;
    }

    public static IWorldGenerator? GetWorldgen(this World world)
    {
        return world.GetData().worldgen;
    }
    
    public static void SetLegacyFormat(this World world, bool legacyFormat)
    {
        world.GetData().legacyFormat = legacyFormat;
    }

    public static bool IsLegacyFormat(this World world) => world.GetData().legacyFormat;

    public static void SetGeneratedWorld(this World world, GeneratedWorld generated)
    {
        world.GetData().generated = generated;
    }

    public static GeneratedWorld? TakeGeneratedWorld(this World world)
    {
        var generated = world.GetData().generated;
        world.GetData().generated = null;

        return generated;
    }

    public static bool IsVanillaWorld(this World world) => world.GetData().worldgenGuid == null;
}
