using UnityEngine;

namespace Worldhalla.Plugin.UI.Preview.Locations;

/*
 * The zone system's location definitions, in the order ZoneSystem.SetupLocations lists them. They are read from the
 * prefabs without instantiating anything: location and alt biome lists register themselves in static lists on Awake,
 * and the game would then see every location twice once a world loads.
 */
public sealed class LocationDefinitions
{
    private const string ZONE_SYSTEM_PATH = "Assets/Systems/_ZoneSystem.prefab";

    public readonly List<ZoneSystem.ZoneLocation> locations;
    public readonly List<AltBiome> altBiomes;

    private LocationDefinitions(List<ZoneSystem.ZoneLocation> locations, List<AltBiome> altBiomes)
    {
        this.locations = locations;
        this.altBiomes = altBiomes;
    }

    // Main thread only
    public static LocationDefinitions Load()
    {
        return GameAssets.Read(ZONE_SYSTEM_PATH, prefab => Read(prefab.GetComponent<ZoneSystem>()));
    }

    private static LocationDefinitions Read(ZoneSystem zoneSystem)
    {
        var locations = new List<ZoneSystem.ZoneLocation>(zoneSystem.m_locations);

        var lists = zoneSystem.m_locationLists
            .Select(list => list != null ? list.GetComponent<LocationList>() : null)
            .ToList();
        if (lists.Any(list => list == null))
            Logger.LogWarning($"Location simulation: {lists.Count(list => list == null)} location lists did not load");

        var loaded = lists.Where(list => list != null).Select(list => list!).ToList();
        loaded.Sort((a, b) => a.m_sortOrder.CompareTo(b.m_sortOrder));
        foreach (var list in loaded)
            locations.AddRange(list.m_locations);

        var altBiomes = new List<AltBiome>();
        foreach (var list in zoneSystem.m_altBiomeLists)
        {
            if (list == null)
            {
                Logger.LogWarning("Location simulation: an alt biome list did not load");
                continue;
            }

            altBiomes.AddRange(list.m_alts);
        }

        foreach (var altBiome in altBiomes)
        foreach (var added in altBiome.m_addLocations)
        {
            var copy = added.Clone();
            copy.AltBiomeParent = altBiome.m_name;
            locations.Add(copy);
        }

        Logger.LogInfo($"Location simulation: {zoneSystem.m_locations.Count} base locations, {loaded.Count} lists, " +
                       $"{altBiomes.Count} alt biomes, {locations.Count} locations in all");

        return new LocationDefinitions(locations, altBiomes);
    }
}
