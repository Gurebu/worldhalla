using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace Worldhalla.Plugin.Localization;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(global::Localization))]
public static class LocalizationPatch
{
    [HarmonyPostfix, HarmonyPatch(nameof(global::Localization.SetupLanguage))]
    private static void SetupLanguage(global::Localization __instance, string language)
    {
        Translations.Load(__instance, language);
    }
}
