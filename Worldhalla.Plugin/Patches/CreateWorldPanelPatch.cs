using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Worldhalla.API;
using Worldhalla.Plugin.Extensions;
using Worldhalla.Plugin.Internal;
using Object = UnityEngine.Object;

namespace Worldhalla.Plugin.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(FejdStartup))]
public static class CreateWorldPanelPatch
{
    private const string WORLDGEN_DROPDOWN = "WorldgenDropdown";
    private const string WORLDGEN_LABEL = "worldgen";
    private const string WORLDGEN_LABEL_TEXT = "Worldgen";
    private const string VANILLA_OPTION = "Vanilla";

    private static RectTransform GetPanel(FejdStartup startup)
    {
        return (RectTransform)startup.m_newWorldSeed.transform.parent;
    }

    private static TMP_Dropdown EnsureWorldgenRow(FejdStartup startup)
    {
        var panel = GetPanel(startup);
        var existing = panel.Find(WORLDGEN_DROPDOWN);
        if (existing != null)
            return existing.GetComponent<TMP_Dropdown>();

        var nameField = (RectTransform)startup.m_newWorldName.transform;
        var seedField = (RectTransform)startup.m_newWorldSeed.transform;
        var seedLabel = panel.Find("seed") as RectTransform;
        var rowStep = nameField.anchoredPosition.y - seedField.anchoredPosition.y;

        InsertRow(panel, GetCenterY(seedField) - rowStep / 2f, rowStep);

        if (seedLabel != null)
        {
            var label = Object.Instantiate(seedLabel.gameObject, panel);
            label.name = WORLDGEN_LABEL;
            label.GetComponent<TMP_Text>().text = WORLDGEN_LABEL_TEXT;
            ((RectTransform)label.transform).anchoredPosition = seedLabel.anchoredPosition - new Vector2(0f, rowStep);
        }

        var template = startup.m_settingsPrefab.GetComponentInChildren<TMP_Dropdown>(true);
        var dropdownObject = Object.Instantiate(template.gameObject, panel);
        dropdownObject.name = WORLDGEN_DROPDOWN;

        var dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
        RemoveExternalCaption(dropdown);
        dropdown.captionText.overflowMode = TextOverflowModes.Ellipsis;
        dropdown.itemText.overflowMode = TextOverflowModes.Ellipsis;

        var dropdownRect = (RectTransform)dropdownObject.transform;
        dropdownRect.anchorMin = seedField.anchorMin;
        dropdownRect.anchorMax = seedField.anchorMax;
        dropdownRect.pivot = seedField.pivot;
        dropdownRect.sizeDelta = new Vector2(seedField.sizeDelta.x, dropdownRect.sizeDelta.y);
        dropdownRect.anchoredPosition = seedField.anchoredPosition - new Vector2(0f, rowStep);

        return dropdown;
    }

    // Settings menu dropdowns may carry a side caption next to the box, besides the one showing the selected value
    private static void RemoveExternalCaption(TMP_Dropdown dropdown)
    {
        foreach (Transform child in dropdown.transform)
        {
            if (child.GetComponent<TMP_Text>() != null && child != dropdown.captionText.transform)
                Object.Destroy(child.gameObject);
        }
    }

    private static float GetCenterY(RectTransform rect)
    {
        return rect.localPosition.y + (0.5f - rect.pivot.y) * rect.rect.height;
    }

    private static void InsertRow(RectTransform panel, float splitY, float height)
    {
        foreach (RectTransform child in panel)
        {
            if (child.anchorMin.y != child.anchorMax.y)
                continue;

            var anchor = child.anchorMin.y;
            var shift = GetCenterY(child) > splitY ? height * (1f - anchor) : -height * anchor;
            child.anchoredPosition += new Vector2(0f, shift);
        }

        panel.sizeDelta += new Vector2(0f, height);
    }

    private static List<IWorldGenerator> GetSelectableGenerators()
    {
        return Registry.Generators
            .OrderBy(generator => generator.Name)
            .ThenBy(generator => generator.Guid)
            .ToList();
    }

    private static List<string> GetWorldgenOptions()
    {
        var generators = GetSelectableGenerators()
            .Select(generator => $"{generator.Name} ({generator.Guid})");

        return new[] { VANILLA_OPTION }.Concat(generators).ToList();
    }

    private static IWorldGenerator? GetSelectedWorldgen(FejdStartup startup)
    {
        var dropdown = GetPanel(startup).Find(WORLDGEN_DROPDOWN);
        if (dropdown == null)
            return null;

        var index = dropdown.GetComponent<TMP_Dropdown>().value - 1;
        var generators = GetSelectableGenerators();

        return index >= 0 && index < generators.Count ? generators[index] : null;
    }

    internal static void AssignSelectedWorldgen(FejdStartup startup)
    {
        var worldgen = GetSelectedWorldgen(startup);
        if (worldgen == null)
            return;

        startup.m_world.SetWorldgen(worldgen);
        startup.m_world.SetSettings(worldgen.DefaultSettings);
    }

    [HarmonyPostfix, HarmonyPatch(nameof(FejdStartup.OnWorldNew))]
    private static void OnWorldNew(FejdStartup __instance)
    {
        var dropdown = EnsureWorldgenRow(__instance);

        dropdown.ClearOptions();
        dropdown.AddOptions(GetWorldgenOptions());
        dropdown.SetValueWithoutNotify(0);
        dropdown.RefreshShownValue();
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
