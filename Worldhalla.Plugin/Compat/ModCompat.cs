using BepInEx.Bootstrap;

namespace Worldhalla.Plugin.Compat;

public static class ModCompat
{
    public const string GUID_BETTER_CONTINENTS = "BetterContinents";
    public const string GUID_EWS = "expand_world_size";
    public const string GUID_EWD = "expand_world_data";
    public const string GUID_LPA = "nickpappas.locationplacementaccelerator";

    private static readonly System.Version RiverheimMinimum = new(1, 3, 0);
    private static readonly string[] RiverheimGuids = [RiverheimMigration.GUID_RELEASE_STABLE, RiverheimMigration.GUID_BETA];

    // Riverheim before 1.3 patches world loading and saving itself, in a way Worldhalla can't share the game with
    public static BepInEx.PluginInfo? FindOutdatedRiverheim()
    {
        return RiverheimGuids
            .Select(guid => Chainloader.PluginInfos.TryGetValue(guid, out var info) ? info : null)
            .FirstOrDefault(info => info != null && info.Metadata.Version < RiverheimMinimum);
    }
}
