using BepInEx;
using HarmonyLib;
using Worldhalla.Plugin.Compat;
using Worldhalla.Plugin.Internal;
using Worldhalla.Plugin.Patches;

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

        var worldListPatch = Config.Bind("Compatibility", "WorldListPatch", true,
            "Shows the world generator in the world list and its tooltip. Disable to improve compatibility with mods that change the world list.");

        var harmony = new Harmony(GUID);
        foreach (var type in AccessTools.GetTypesFromAssembly(typeof(WorldhallaPlugin).Assembly))
        {
            if (type == typeof(WorldListPatch) && !worldListPatch.Value)
                continue;

            harmony.CreateClassProcessor(type).Patch();
        }

        if (Traverse.Create<Localization>().Field<Localization>("m_instance").Value is { } localization)
            Translations.Load(localization, localization.GetSelectedLanguage());
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
