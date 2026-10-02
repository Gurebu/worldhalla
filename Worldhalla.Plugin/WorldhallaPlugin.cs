using BepInEx;
using HarmonyLib;
using Worldhalla.Plugin.Compat;
using Worldhalla.Plugin.Core;
using Worldhalla.Plugin.Localization;
using Worldhalla.Plugin.UI.Patches;

namespace Worldhalla.Plugin;

[BepInPlugin(GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInIncompatibility(ModCompat.GUID_BETTER_CONTINENTS)]
public class WorldhallaPlugin : BaseUnityPlugin
{
    private const string GUID = "dev.gurebu.worldhalla";

    private bool disabled;

    public void Awake()
    {
        Worldhalla.Plugin.Logger.SetLogger((level, message) =>
        {
            switch (level)
            {
                case Worldhalla.Plugin.Logger.LogLevel.Info:
                    Logger.LogInfo(message);
                    break;
                case Worldhalla.Plugin.Logger.LogLevel.Warning:
                    Logger.LogWarning(message);
                    break;
                case Worldhalla.Plugin.Logger.LogLevel.Error:
                    Logger.LogError(message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, null);
            }
        });

        if (StandDownForOutdatedRiverheim())
            return;

        var worldListPatch = Config.Bind("Compatibility", "WorldListPatch", true,
            "Shows the world generator in the world list and its tooltip. Disable to improve compatibility with mods that change the world list.");

        var harmony = new Harmony(GUID);
        foreach (var type in AccessTools.GetTypesFromAssembly(typeof(WorldhallaPlugin).Assembly))
        {
            if (type == typeof(WorldListPatch) && !worldListPatch.Value)
                continue;

            harmony.CreateClassProcessor(type).Patch();
        }

        if (Traverse.Create<global::Localization>().Field<global::Localization>("m_instance").Value is { } localization)
            Translations.Load(localization, localization.GetSelectedLanguage());
    }
    
    public void Start()
    {
        if (disabled)
            return;

        // An outdated Riverheim loaded after Awake has seen Worldhalla's patches go in, so they come out again
        if (StandDownForOutdatedRiverheim())
        {
            Harmony.UnpatchID(GUID);
            return;
        }

        Registry.DiscoverGenerators();
    }

    private bool StandDownForOutdatedRiverheim()
    {
        if (ModCompat.FindOutdatedRiverheim() is not { } riverheim)
            return false;

        Logger.LogError($"{riverheim.Metadata.Name} {riverheim.Metadata.Version} ({riverheim.Metadata.GUID}) is incompatible with " +
                        "Worldhalla. Update it to 1.3 or newer; until then Worldhalla stays inactive.");
        disabled = true;
        return true;
    }

    public void OnDestroy()
    {
        Registry.PurgeAll();
        Harmony.UnpatchID(GUID);
    }
}
