using System.Reflection;
using UnityEngine;

namespace Worldhalla.Plugin.Localization;

public static class Translations
{
    private const string FALLBACK_LANGUAGE = "English";
    private const string RESOURCE_ROOT = "Localization/";

    public static string Get(string key, params string[] words)
    {
        return global::Localization.instance.Localize(key, words);
    }

    public static void Load(global::Localization localization, string language)
    {
        LoadLanguage(localization, FALLBACK_LANGUAGE);
        if (language != FALLBACK_LANGUAGE)
            LoadLanguage(localization, language);
    }

    private static void LoadLanguage(global::Localization localization, string language)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var folder = $"{RESOURCE_ROOT}{language}/";

        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.Replace('\\', '/').StartsWith(folder, StringComparison.Ordinal))
                continue;

            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);

            var file = new TextAsset(reader.ReadToEnd()) { name = resource };
            if (!localization.LoadCSV(file, language))
                Logger.LogWarning($"Could not load translations '{resource}'");
        }
    }
}
