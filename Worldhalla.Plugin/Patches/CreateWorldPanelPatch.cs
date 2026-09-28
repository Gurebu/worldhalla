using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using HarmonyLib;
using Worldhalla.Plugin.Extensions;
using Worldhalla.Plugin.UI;

namespace Worldhalla.Plugin.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(FejdStartup))]
public static class CreateWorldPanelPatch
{
    private static WorldCreationDialog? GetDialog(FejdStartup startup)
    {
        return startup.m_createWorldPanel.GetComponentInChildren<WorldCreationDialog>(true);
    }

    internal static void AssignSelectedWorldgen(FejdStartup startup)
    {
        if (GetDialog(startup) is not { SelectedWorldgen: { } worldgen } dialog)
            return;

        startup.m_world.SetWorldgen(worldgen);
        startup.m_world.SetSettings(dialog.Settings);
    }

    // Built before the create-world panel is first shown, so the vanilla widgets moved into it wake up under their new parent
    [HarmonyPostfix, HarmonyPatch(nameof(FejdStartup.Awake))]
    private static void Awake(FejdStartup __instance)
    {
        WorldCreationDialogBuilder.Build(__instance);
    }

    [HarmonyPostfix, HarmonyPatch(nameof(FejdStartup.OnWorldNew))]
    private static void OnWorldNew(FejdStartup __instance)
    {
        GetDialog(__instance)?.Open();
    }

    [HarmonyTranspiler, HarmonyPatch(nameof(FejdStartup.OnNewWorldDone))]
    internal static IEnumerable<CodeInstruction> TranspileNewWorldDone(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .AdvanceToAfterNewWorldAssignment()
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(CodeInstruction.Call(typeof(CreateWorldPanelPatch), nameof(AssignSelectedWorldgen)))
            .InstructionEnumeration();
    }
}
