using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Worldhalla.Plugin.Internal;

namespace Worldhalla.Plugin.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Localization))]
public static class LocalizationPatch
{
    [HarmonyPostfix, HarmonyPatch(nameof(Localization.SetupLanguage))]
    private static void SetupLanguage(Localization __instance, string language)
    {
        Translations.Load(__instance, language);
    }
}
