using UnityEngine;
using Object = UnityEngine.Object;

namespace Worldhalla.Plugin.UI.Preview.Locations;

// The minimap's icons for unique locations by location prefab name, and its boss pin
public sealed class MapIcons
{
    private static readonly string[] MinimapPrefabs =
    [
        "Assets/UI/prefabs/IngameGui/IngameGui_HUD_Minimap.prefab",
        "Assets/Systems/_GameMain.prefab",
    ];

    private static MapIcons? loaded;

    public readonly Dictionary<string, Texture2D> locations;
    public readonly Texture2D? boss;

    private MapIcons(Dictionary<string, Texture2D> locations, Texture2D? boss)
    {
        this.locations = locations;
        this.boss = boss;
    }

    // Main thread only
    public static MapIcons Load()
    {
        if (loaded != null)
            return loaded;

        foreach (var path in MinimapPrefabs)
        {
            var icons = GameAssets.Read(path, prefab => prefab.GetComponentInChildren<Minimap>(true) is { } minimap ? Read(minimap) : null);
            if (icons == null)
                continue;

            Logger.LogInfo($"Location simulation: {icons.locations.Count} map icons from {path}: {string.Join(", ", icons.locations.Keys)}" +
                           (icons.boss == null ? "; no boss icon" : ""));
            return loaded = icons;
        }

        throw new InvalidOperationException("No minimap found to take location icons from");
    }

    private static MapIcons Read(Minimap minimap)
    {
        var locations = minimap.m_locationIcons
            .Where(icon => icon.m_icon != null)
            .GroupBy(icon => icon.m_name)
            .ToDictionary(group => group.Key, group => Outlined(group.First().m_icon));

        var boss = minimap.m_icons.FirstOrDefault(icon => icon.m_name == Minimap.PinType.Boss && icon.m_icon != null).m_icon;

        return new MapIcons(locations, boss != null ? Outlined(boss) : null);
    }

    private static Texture2D Outlined(Sprite sprite)
    {
        var copy = GameAssets.Copy(sprite);
        try
        {
            return IconOutline.Apply(copy);
        }
        finally
        {
            Object.Destroy(copy);
        }
    }
}
