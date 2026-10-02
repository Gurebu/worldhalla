using Worldhalla.API;
using Worldhalla.API.Settings;

namespace Worldhalla.Plugin.Core.Extensions;

public static class WorldgenExtension
{
    public static ISettings? GetDefaultSettings(this IWorldGenerator worldgen)
    {
        return (worldgen as ISettingsHandler)?.DefaultSettings;
    }

    public static ISettings? LoadSettings(this IWorldGenerator worldgen, byte[] blob)
    {
        return worldgen is ISettingsHandler handler ? handler.BuildSettingsFromBinaryBlob(blob) : null;
    }
}
