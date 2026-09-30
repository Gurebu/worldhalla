using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using UnityEngine.UI;
using Worldhalla.Plugin.Extensions;

namespace Worldhalla.Plugin.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(FejdStartup))]
public static class FejdStartupPatch
{
    private const string MIGRATION_HEADER = "Upgrade the world?";

    private static bool NeedsMigrationConfirmed(FejdStartup startup)
    {
        var world = startup.m_world;

        return world != null && !startup.m_startingWorld && world.m_dataError == World.SaveDataError.None &&
               world.IsLegacyFormat();
    }

    private static void ConfirmMigration(World world, Action start)
    {
        var text = $"'{world.m_name}' was created with an older version of {world.GetWorldgen()!.Name}. " +
                   "Once loaded, it is saved in a new format that older versions can't read.";

        UnifiedPopup.Push(new YesNoPopup(MIGRATION_HEADER, text, () =>
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
