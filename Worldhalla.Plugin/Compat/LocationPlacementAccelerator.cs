using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace Worldhalla.Plugin.Compat;

public static class LocationPlacementAccelerator
{
    private const string VANILLA_MODE = "Vanilla";
    private const string DEFAULT_MODE = "Survey";

    private static readonly ConfigDefinition Mode = new("1 - General", "PlacementMode");

    public static bool PlacesLikeVanilla
    {
        get
        {
            if (!Chainloader.PluginInfos.TryGetValue(ModCompat.GUID_LPA, out var plugin) || plugin.Instance == null)
                return true;

            var config = plugin.Instance.Config;
            var mode = config.ContainsKey(Mode) ? config[Mode].BoxedValue.ToString() : DEFAULT_MODE;
            return mode == VANILLA_MODE;
        }
    }
}
