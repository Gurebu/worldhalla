using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Worldhalla.Plugin.Core;
using Worldhalla.Plugin.Core.Extensions;
using Worldhalla.Plugin.Localization;

namespace Worldhalla.Plugin.UI.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(FejdStartup))]
public static class WorldListPatch
{
    private const string MONIKER_ELEMENT = "worldgen";
    private const int MONIKER_LENGTH = 3;
    private const float GLYPH_WIDTH_EM = 0.75f;
    private const float MONIKER_GAP = 4f;
    private const string WORLDGEN_TOOLTIP_TOPIC = "$wh_tooltip_topic";
    private const string UNKNOWN_MONIKER = "???";

    private static void EnsureMonikerColumn(GameObject worldListElement)
    {
        if (worldListElement.transform.Find(MONIKER_ELEMENT) != null)
            return;

        var name = (RectTransform)worldListElement.transform.Find("name");
        var moniker = UnityEngine.Object.Instantiate(name.gameObject, name.parent);
        moniker.name = MONIKER_ELEMENT;

        var text = moniker.GetComponent<TMP_Text>();
        text.horizontalAlignment = HorizontalAlignmentOptions.Left;
        text.text = "";

        var width = MONIKER_LENGTH * GLYPH_WIDTH_EM * text.fontSize;

        var monikerRect = (RectTransform)moniker.transform;
        monikerRect.offsetMax = new Vector2(name.offsetMin.x + width, name.offsetMax.y);
        name.offsetMin += new Vector2(width + MONIKER_GAP, 0f);
    }

    private static string GetMoniker(World world)
    {
        var moniker = world.GetGenerator() switch
        {
            null => "",
            { Guid: var guid } when Registry.FindByGuid(guid, out var generator) => generator.Moniker,
            _ => UNKNOWN_MONIKER,
        };

        return moniker.Length > MONIKER_LENGTH ? moniker.Substring(0, MONIKER_LENGTH) : moniker;
    }

    private static string FormatAsTopic(UITooltip tooltip, string topic)
    {
        var topicElement = Utils.FindChild(tooltip.m_tooltipPrefab.transform, "Topic");
        if (topicElement == null)
            return $"{topic}\n";

        var text = topicElement.GetComponent<TMP_Text>();
        var color = ColorUtility.ToHtmlStringRGBA(text.color);
        var formatted = FormattableString.Invariant($"<size={text.fontSize}><color=#{color}>{topic}</color></size>");
        if ((text.fontStyle & FontStyles.Bold) != 0)
            formatted = $"<b>{formatted}</b>";
        if ((text.fontStyle & FontStyles.Italic) != 0)
            formatted = $"<i>{formatted}</i>";

        var layoutGroup = topicElement.parent.GetComponent<VerticalLayoutGroup>();
        var spacing = layoutGroup != null ? layoutGroup.spacing : 0f;

        return FormattableString.Invariant($"{formatted}\n<line-height={spacing}px>\n</line-height>");
    }

    private static void PrependWorldgenTooltip(FejdStartup startup, GameObject worldListElement, World world)
    {
        var tooltip = worldListElement.GetComponentInChildren<UITooltip>();
        if (tooltip == null)
            return;

        if (world.GetGenerator() is not { } worldgen)
            return;

        var text = Registry.FindByGuid(worldgen.Guid, out var generator)
            ? Translations.Get("$wh_tooltip_name", generator.Name)
            : Translations.Get("$wh_tooltip_unknown");
        text += $"\n{Translations.Get("$wh_tooltip_guid", worldgen.Guid)}";
        if (worldgen is IncompatibleGeneratorBinding { Reason: Incompatibility.WorldhallaSettingsUnreadable })
            text += $"\n{Translations.Get("$wh_tooltip_worldhalla_settings_unreadable")}";
        if (worldgen is IncompatibleGeneratorBinding { Reason: Incompatibility.GeneratorSettingsUnreadable })
            text += $"\n{Translations.Get("$wh_tooltip_generator_settings_unreadable")}";
        if (worldgen is CompatibleGeneratorBinding { GeneratorSettings: { } settings })
            text += $"\n{settings.Summary().TrimEnd()}";

        if (tooltip.m_text != "")
        {
            var header = tooltip.m_topic != "" ? FormatAsTopic(tooltip, tooltip.m_topic) : "";
            text += $"\n\n{header}{tooltip.m_text}";
        }

        var anchor = startup.m_worldSourceInfoPanel.activeSelf ? startup.m_tooltipSecondaryAnchor : startup.m_tooltipAnchor;
        tooltip.Set(Translations.Get(WORLDGEN_TOOLTIP_TOPIC), text, anchor);
    }

    [HarmonyPrefix, HarmonyPatch(nameof(FejdStartup.UpdateWorldList))]
    private static void UpdateWorldListPrefix(FejdStartup __instance)
    {
        EnsureMonikerColumn(__instance.m_worldListElement);
    }

    [HarmonyPostfix, HarmonyPriority(Priority.Last), HarmonyPatch(nameof(FejdStartup.UpdateWorldList))]
    private static void UpdateWorldListPostfix(FejdStartup __instance)
    {
        for (var i = 0; i < __instance.m_worlds.Count; i++)
        {
            var worldListElement = __instance.m_worldListElements[i];
            var world = __instance.m_worlds[i];

            var moniker = worldListElement.transform.Find(MONIKER_ELEMENT);
            if (moniker != null)
                moniker.GetComponent<TMP_Text>().text = GetMoniker(world);

            PrependWorldgenTooltip(__instance, worldListElement, world);
        }
    }
}
