using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Worldhalla.Plugin.Internal;

namespace Worldhalla.Plugin.Tests;

/*
 * Translation files are read by the game's own CSV parser, so they are checked with it too. English is
 * loaded under every other language, so a language may leave keys out, but a blank cell would replace
 * the English text with nothing.
 */
public class TranslationTests
{
    private const string FALLBACK_LANGUAGE = "English";

    private static readonly Assembly Plugin = typeof(Translations).Assembly;

    private static IEnumerable<string> Languages() => Plugin.GetManifestResourceNames()
        .Select(name => name.Replace('\\', '/').Split('/'))
        .Where(parts => parts.Length == 3 && parts[0] == "Localization")
        .Select(parts => parts[1])
        .Distinct();

    private static List<List<string>> ReadRows(string language, out string header)
    {
        var resource = Plugin.GetManifestResourceNames().Single(name => name.Replace('\\', '/').StartsWith($"Localization/{language}/"));
        using var stream = new StreamReader(Plugin.GetManifestResourceStream(resource)!);
        using var reader = new StringReader(stream.ReadToEnd());
        header = reader.ReadLine()!;

        var localization = (Localization)RuntimeHelpers.GetUninitializedObject(typeof(Localization));
        var split = typeof(Localization).GetMethod("DoQuoteLineSplit", BindingFlags.Instance | BindingFlags.NonPublic)!;

        return ((List<List<string>>)split.Invoke(localization, [reader])!)
            .Where(row => row.Count > 0 && row[0].Length > 0 && !row[0].StartsWith("//"))
            .ToList();
    }

    private static Dictionary<string, string> Read(string language)
    {
        var rows = ReadRows(language, out _);

        Assert.That(rows.Select(row => row[0]), Is.Unique, $"{language} repeats a key");
        Assert.That(rows.Where(row => row.Count != 2).Select(row => row[0]), Is.Empty, $"{language} has rows that aren't one key and one text");

        return rows.ToDictionary(row => row[0], row => row[1].Trim());
    }

    private static string[] Placeholders(string text) =>
        Regex.Matches(text, @"\$\d+").Cast<Match>().Select(match => match.Value).Distinct().OrderBy(value => value).ToArray();

    [Test, RequiresValheim]
    public void EnglishIsPresent()
    {
        Assert.That(Languages(), Does.Contain(FALLBACK_LANGUAGE));
    }

    [Test, RequiresValheim]
    public void EveryLanguageMatchesEnglish([ValueSource(nameof(Languages))] string language)
    {
        ReadRows(language, out var header);
        Assert.That(header.Trim(), Is.EqualTo($"Key,{language}"), "the column must be named after the folder, or the game finds no text in it");

        var english = Read(FALLBACK_LANGUAGE);
        var translated = Read(language);

        Assert.That(translated.Keys.Except(english.Keys), Is.Empty, "keys English doesn't have");
        Assert.That(translated.Where(entry => entry.Value.Length == 0).Select(entry => entry.Key), Is.Empty, "blank texts");

        foreach (var (key, text) in translated)
            Assert.That(Placeholders(text), Is.EqualTo(Placeholders(english[key])), $"'{key}' has different placeholders");
    }
}
