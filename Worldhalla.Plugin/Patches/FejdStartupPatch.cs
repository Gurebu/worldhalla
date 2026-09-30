using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using UnityEngine.UI;
using Worldhalla.Plugin.Extensions;
using Worldhalla.Plugin.Internal;

namespace Worldhalla.Plugin.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(FejdStartup))]
public static class FejdStartupPatch
{
    private const string MIGRATION_HEADER = "$wh_migration_header";

    private static bool NeedsMigrationConfirmed(FejdStartup startup)
    {
        var world = startup.m_world;

        return world != null && !startup.m_startingWorld && world.m_dataError == World.SaveDataError.None &&
               world.IsLegacyFormat();
    }

    private static void ConfirmMigration(World world, Action start)
    {
        var text = Translations.Get("$wh_migration_text", world.m_name, world.GetWorldgen()!.Name);

        UnifiedPopup.Push(new YesNoPopup(Translations.Get(MIGRATION_HEADER), text, () =>
        {
            UnifiedPopup.Pop();
            start();
        }, UnifiedPopup.Pop, localizeText: false));
    }

    [HarmonyPostfix, HarmonyPatch(nameof(FejdStartup.Awake))]
    private static void Awake(FejdStartup __instance)
    {
        var start = __instance.m_worldStart.onClick;

        __instance.m_worldStart.onClick = new Button.ButtonClickedEvent();
        __instance.m_worldStart.onClick.AddListener(() =>
        {
            if (NeedsMigrationConfirmed(__instance))
                ConfirmMigration(__instance.m_world, start.Invoke);
            else
                start.Invoke();
        });
    }

    [HarmonyPostfix, HarmonyPatch(nameof(FejdStartup.ShowConnectError))]
    private static void ShowConnectError(FejdStartup __instance, ZNet.ConnectionStatus statusOverride)
    {
        var message = ZNetPatch.TakeRefusalMessage();
        var status = statusOverride == ZNet.ConnectionStatus.None ? ZNet.GetConnectionStatus() : statusOverride;

        if (message != null && status == ZNet.ConnectionStatus.ErrorVersion)
            __instance.m_connectionFailedError.text = message;
    }
}
