using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using HarmonyLib;
using Worldhalla.Plugin.Core;
using Worldhalla.Plugin.Core.Extensions;

namespace Worldhalla.Plugin.UI.Patches;

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

        startup.m_world.SetGenerator(new CompatibleGeneratorBinding(worldgen, dialog.WorldhallaSettings, dialog.Settings));
    }

    [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(nameof(FejdStartup.Awake))]
    private static void BeforeAwake(FejdStartup __instance, out (VanillaCreateWorldPanel vanilla, GuiElementPalette palette) __state)
    {
        __state = (new VanillaCreateWorldPanel(__instance), new GuiElementPalette(__instance));
    }

    [HarmonyPostfix, HarmonyPatch(nameof(FejdStartup.Awake))]
    private static void Awake(FejdStartup __instance, (VanillaCreateWorldPanel vanilla, GuiElementPalette palette) __state)
    {
        WorldCreationDialogBuilder.Build(__instance, __state.vanilla, __state.palette);
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
