using BepInEx;
using HarmonyLib;
using Worldhalla.Plugin.Compat;
using Worldhalla.Plugin.Internal;

namespace Worldhalla.Plugin;

[BepInPlugin(GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInIncompatibility(ModCompat.GUID_BETTER_CONTINENTS)]
public class WorldhallaPlugin : BaseUnityPlugin
{
    private const string GUID = "dev.gurebu.worldhalla";

    public void Awake()
    {
        Internal.Logger.SetLogger((level, message) =>
        {
            switch (level)
            {
                case Internal.Logger.LogLevel.Info:
                    Logger.LogInfo(message);
                    break;
                case Internal.Logger.LogLevel.Warning:
                    Logger.LogWarning(message);
                    break;
                case Internal.Logger.LogLevel.Error:
                    Logger.LogError(message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, null);
            }
        });

        new Harmony(GUID).PatchAll();
    }
    
    public void Start()
    {
        Registry.DiscoverGenerators();
    }

    public void OnDestroy()
    {
        Registry.PurgeAll();
        Harmony.UnpatchID(GUID);
    }
}
