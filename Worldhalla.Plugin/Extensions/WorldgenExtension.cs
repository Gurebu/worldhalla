using Worldhalla.API;
using Worldhalla.API.Settings;
using Worldhalla.Plugin.Internal;

namespace Worldhalla.Plugin.Extensions;

public static class WorldgenExtension
{
    internal static string GetGuid(this IWorldGenerator worldgen)
    {
        return Registry.GuidOf(worldgen);
    }

    public static ISettings? GetDefaultSettings(this IWorldGenerator worldgen)
    {
        return (worldgen as ISettingsHandler)?.DefaultSettings;
    }

    public static ISettings? LoadSettings(this IWorldGenerator worldgen, byte[] blob)
    {
        return worldgen is ISettingsHandler handler ? handler.BuildSettingsFromBinaryBlob(blob) : null;
    }
}
