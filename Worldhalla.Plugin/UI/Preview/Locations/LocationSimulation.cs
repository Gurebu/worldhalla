using System.Collections;
using System.Diagnostics;
using SoftReferenceableAssets;
using UnityEngine;
using Worldhalla.Plugin.Core;
using Worldhalla.Plugin.Core.Extensions;
using Random = UnityEngine.Random;

namespace Worldhalla.Plugin.UI.Preview.Locations;

/*
 * Experimental: places the locations of a world the way ZoneSystem.GenerateLocationsTimeSliced does, in the game's
 * order, up to the last one that gets a map marker; every location placed earlier can take a zone a later one wanted.
 *
 * Placement runs on the main thread in slices, since it draws from the engine's global random state, terrain delta
 * included. Each slice swaps that state in and out, so the game's own draws are left as they were.
 */
public sealed class LocationSimulation
{
    public readonly struct Placed(string name, Vector3 position, Texture2D icon, string? caption)
    {
        public readonly string name = name;
        public readonly Vector3 position = position;
        public readonly Texture2D icon = icon;
        public readonly string? caption = caption;
    }

    private const double SLICE_SECONDS = 1.0 / 150.0;
    private const float SAMPLING_SHARE = 0.5f;

    // Written by the simulation, on any thread; read by the UI
    public sealed class Progress
    {
        private volatile float fraction;

        public float Fraction
        {
            get => fraction;
            set => fraction = value;
        }
    }
    private const float SEA_LEVEL = 30f;

    private readonly WorldGenerator generator;
    private readonly AltBiomeWorldData points;
    private readonly Stopwatch slice = new();

    private readonly Dictionary<Vector2s, ZoneSystem.LocationInstance> instances = new();
    private readonly Dictionary<AssetID, List<ZoneSystem.LocationInstance>> byAsset = new();
    private readonly Dictionary<string, List<ZoneSystem.LocationInstance>> byGroup = new();
    private readonly Dictionary<string, List<ZoneSystem.LocationInstance>> byMaxGroup = new();
    private readonly Dictionary<Vector2s, Heightmap.Biome> zoneBiomes = new();
    private readonly List<float> surroundings = [];
    private readonly List<(ZoneSystem.ZoneLocation location, Vector3 position)> placements = [];

    private LocationSimulation(WorldGenerator generator, AltBiomeWorldData points)
    {
        this.generator = generator;
        this.points = points;
    }

    // Main thread only: the generator's constructor draws from the engine's random state
    public static WorldGenerator CreateGenerator(string seed, GeneratedWorld world)
    {
        if (world.State is VanillaWorldState vanilla)
            return vanilla.Generator;

        var simulated = new World("", seed) { m_menu = true };
        var generator = new WorldGenerator(simulated);
        simulated.m_menu = false;
        generator.SetWorld(world);
        return generator;
    }

    // Hands over the placed locations the minimap has an icon for, as it would show them
    public static IEnumerator Run(WorldGenerator generator, Progress progress, Action<List<Placed>> done, CancellationToken token)
    {
        var total = Stopwatch.StartNew();

        // Lets the UI show progress before the asset loading below holds up a frame
        yield return null;

        LocationDefinitions definitions;
        MapIcons icons;
        try
        {
            definitions = LocationDefinitions.Load();
            icons = MapIcons.Load();
        }
        catch (Exception e)
        {
            Logger.LogError($"Location simulation: loading game assets failed: {e}");
            yield break;
        }

        Logger.LogInfo($"Location simulation: definitions loaded in {total.ElapsedMilliseconds} ms");

        var build = Task.Run(() => BiomePointLists.Build(generator, fraction => progress.Fraction = SAMPLING_SHARE * fraction, token), token);
        while (!build.IsCompleted)
            yield return null;

        if (build.Status != TaskStatus.RanToCompletion)
        {
            Logger.LogError($"Location simulation: biome points failed: {build.Exception}");
            yield break;
        }

        Logger.LogInfo($"Location simulation: biome points built at {total.ElapsedMilliseconds} ms");

        var all = definitions.locations
            .OrderByDescending(location => location.m_prioritized)
            .Where(location => location.m_enable && location.m_quantity != 0)
            .ToList();

        var last = all.FindLastIndex(location => MarkerOf(location.m_prefab.Name, location, icons) != null);
        var ordered = all.Take(last + 1).ToList();
        Logger.LogInfo($"Location simulation: placing {ordered.Count} of {all.Count} location types, " +
                       $"{ordered.Count(location => location.m_prioritized)} of them prioritized");

        var simulation = new LocationSimulation(generator, build.Result);
        yield return simulation.Sliced(simulation.PlaceAll(ordered, definitions.altBiomes, progress), token);

        Logger.LogInfo($"Location simulation: done in {total.ElapsedMilliseconds} ms");

        if (token.IsCancellationRequested)
            yield break;

        done(simulation.placements
            .Select(placement => Mark(placement.location.m_prefab.Name, placement.location, placement.position, icons))
            .OfType<Placed>()
            .ToList());
    }

    private static Placed? Mark(string name, ZoneSystem.ZoneLocation location, Vector3 position, MapIcons icons)
    {
        return MarkerOf(name, location, icons) is var (icon, caption) ? new Placed(name, position, icon, caption) : null;
    }

    private static (Texture2D icon, string? caption)? MarkerOf(string name, ZoneSystem.ZoneLocation location, MapIcons icons)
    {
        if (BossLocations.Captions.TryGetValue(name, out var key))
        {
            var caption = global::Localization.instance.Localize(key);
            if (caption.StartsWith("["))
                Logger.LogWarning($"Location simulation: no translation for boss caption '{key}'");

            return icons.boss != null ? (icons.boss, caption) : null;
        }

        if ((location.m_iconAlways || location.m_iconPlaced) && icons.locations.TryGetValue(name, out var icon))
            return (icon, null);

        return null;
    }

    private IEnumerator Sliced(IEnumerator steps, CancellationToken token)
    {
        var state = Random.state;

        while (!token.IsCancellationRequested)
        {
            var outer = Random.state;
            Random.state = state;
            bool more;

            try
            {
                slice.Restart();
                more = steps.MoveNext();
            }
            finally
            {
                state = Random.state;
                Random.state = outer;
            }

            if (!more)
                yield break;

            yield return null;
        }
    }

    private IEnumerator PlaceAll(List<ZoneSystem.ZoneLocation> ordered, List<AltBiome> altBiomes, Progress progress)
    {
        for (var index = 0; index < ordered.Count; index++)
        {
            progress.Fraction = SAMPLING_SHARE + (1f - SAMPLING_SHARE) * index / ordered.Count;
            var location = ordered[index];
            var name = location.m_prefab.Name;
            var notes = "";
            if (location.AltBiomeParent != null)
                notes += $" [added by alt biome '{location.AltBiomeParent}', not simulated]";
            if (altBiomes.Where(altBiome => altBiome.m_blockLocationNames.Contains(location.m_name)).Select(altBiome => altBiome.m_name).ToList() is { Count: > 0 } blocking)
                notes += $" [blockable by alt biomes {string.Join(", ", blocking)}]";

            if (location.AltBiomeParent != null)
            {
                Logger.LogWarning($"Location simulation: {name}{notes}; whatever comes after it may be placed differently in game");
                continue;
            }

            var started = Stopwatch.StartNew();
            var before = placements.Count;

            var placing = Place(location, name);
            while (placing.MoveNext())
                yield return null;

            var placed = placements.Skip(before).Select(placement => placement.position).ToList();
            var shown = string.Join(" ", placed.Take(5).Select(p => $"({p.x:F1}, {p.z:F1})"));
            Logger.LogInfo($"Location simulation: {name} ({location.m_name}) placed {placed.Count}/{location.m_quantity} " +
                           $"in {started.ElapsedMilliseconds} ms{notes}: {shown}{(placed.Count > 5 ? " ..." : "")}");
        }
    }

    // A port of ZoneSystem.GenerateLocationsTimeSliced(ZoneLocation, ...), minus its error counters
    private IEnumerator Place(ZoneSystem.ZoneLocation location, string name)
    {
        Random.InitState(generator.GetSeed() + name.GetStableHashCode());

        var maxRadius = Mathf.Max(location.m_exteriorRadius, location.m_interiorRadius);
        var attempts = location.m_prioritized ? 60000 : 12000;
        var placed = instances.Values.Count(instance => instance.m_location.m_prefab.Name == name);
        var maxRange = 10000f;
        surroundings.Clear();

        if (location.m_unique && placed > 0)
            yield break;

        if (location.m_centerFirst)
            maxRange = location.m_minDistance;

        for (var i = 0; i < attempts && placed < location.m_quantity; i++)
        {
            if (slice.Elapsed.TotalSeconds >= SLICE_SECONDS)
                yield return null;

            var zone = location.m_centerFirst
                ? ZoneSystem.GetRandomZone(maxRange)
                : ZoneSystem.GetZone(AltBiomeWorldData.MapSpaceToWorldSpace(location.m_minAltitude >= 0f
                    ? points.GetRandomPointByBiomesAboveSeaLevel(location.m_biome)
                    : points.GetRandomPointByBiomes(location.m_biome)));

            if (location.m_centerFirst)
                maxRange += 1f;

            if (instances.ContainsKey(zone))
                continue;

            if ((location.m_biomeArea & GetBiomeArea(ZoneSystem.GetZoneCenter(zone))) == 0)
                continue;

            for (var j = 0; j < 6; j++)
            {
                if (slice.Elapsed.TotalSeconds >= SLICE_SECONDS)
                    yield return null;

                if (TryPoint(location, zone, maxRadius) is not { } point)
                    continue;

                Register(location, point);
                placed++;
                break;
            }
        }
    }

    private Vector3? TryPoint(ZoneSystem.ZoneLocation location, Vector2s zone, float maxRadius)
    {
        var point = ZoneSystem.GetRandomPointInZone(zone, maxRadius);
        var magnitude = point.magnitude;
        if (location.m_minDistance != 0f && magnitude < location.m_minDistance)
            return null;
        if (location.m_maxDistance != 0f && magnitude > location.m_maxDistance)
            return null;

        if ((location.m_biome & generator.GetBiome(point)) == 0)
            return null;

        point.y = generator.GetHeight(point.x, point.z, out var mask);
        var altitude = (float)(point.y - (double)SEA_LEVEL);
        if (altitude < location.m_minAltitude || altitude > location.m_maxAltitude)
            return null;

        if (location.m_inForest)
        {
            var forestFactor = generator.GetWorld()?.ForestFactor(point) ?? WorldGenerator.GetForestFactor(point);
            if (forestFactor < location.m_forestTresholdMin || forestFactor > location.m_forestTresholdMax)
                return null;
        }

        if (location.m_minDistanceFromCenter > 0f || location.m_maxDistanceFromCenter > 0f)
        {
            var distance = Utils.LengthXZ(point);
            if ((location.m_minDistanceFromCenter > 0f && distance < location.m_minDistanceFromCenter) ||
                (location.m_maxDistanceFromCenter > 0f && distance > location.m_maxDistanceFromCenter))
                return null;
        }

        generator.GetTerrainDelta(point, location.m_exteriorRadius, out var delta, out _);
        if (delta > location.m_maxTerrainDelta || delta < location.m_minTerrainDelta)
            return null;

        if (location.m_minDistanceFromSimilar > 0f &&
            HaveLocationInRange(location.m_prefab.m_assetID, location.m_group, point, location.m_minDistanceFromSimilar, false))
            return null;

        if (location.m_maxDistanceFromSimilar > 0f &&
            !HaveLocationInRange(location.m_prefab.m_assetID, location.m_groupMax, point, location.m_maxDistanceFromSimilar, true))
            return null;

        var vegetation = mask.a;
        if (location.m_minimumVegetation > 0f && vegetation <= location.m_minimumVegetation)
            return null;
        if (location.m_maximumVegetation < 1f && vegetation >= location.m_maximumVegetation)
            return null;

        if (location.m_surroundCheckVegetation && !PassesSurroundCheck(location, point))
            return null;

        return point;
    }

    private bool PassesSurroundCheck(ZoneSystem.ZoneLocation location, Vector3 point)
    {
        var total = 0f;
        for (var layer = 0; layer < location.m_surroundCheckLayers; layer++)
        {
            var radius = (float)(layer + 1) / location.m_surroundCheckLayers * location.m_surroundCheckDistance;
            for (var k = 0; k < 6; k++)
            {
                var angle = k / 6f * MathF.PI * 2f;
                var sample = point + new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
                generator.GetHeight(sample.x, sample.z, out var mask);
                var weight = (location.m_surroundCheckDistance - radius) / (location.m_surroundCheckDistance * 2f);
                total += mask.a * weight;
            }
        }

        surroundings.Add(total);
        if (surroundings.Count < 10)
            return false;

        var max = surroundings.Max();
        var average = surroundings.Average();
        return total >= average + (max - average) * location.m_surroundBetterThanAverage;
    }

    // WorldGenerator.GetBiomeArea(Vector2s) caches in static dictionaries shared with the running game
    private Heightmap.BiomeArea GetBiomeArea(Vector2s zoneCenter)
    {
        var biome = GetZoneBiome(zoneCenter);
        foreach (var offset in WorldGenerator.s_biomeAreaOffsetsInt)
        {
            if (GetZoneBiome(zoneCenter - offset) != biome)
                return Heightmap.BiomeArea.Edge;
        }

        return Heightmap.BiomeArea.Median;
    }

    private Heightmap.Biome GetZoneBiome(Vector2s point)
    {
        if (!zoneBiomes.TryGetValue(point, out var biome))
            zoneBiomes[point] = biome = generator.GetBiome(point.x, point.y);

        return biome;
    }

    private void Register(ZoneSystem.ZoneLocation location, Vector3 point)
    {
        var instance = new ZoneSystem.LocationInstance { m_location = location, m_position = point };
        instances.Add(ZoneSystem.GetZone(point), instance);
        placements.Add((location, point));
        AddTo(byAsset, location.m_prefab.m_assetID, instance);
        AddTo(byGroup, location.m_group, instance);
        AddTo(byMaxGroup, location.m_groupMax, instance);
    }

    private static void AddTo<TKey>(Dictionary<TKey, List<ZoneSystem.LocationInstance>> cache, TKey key, ZoneSystem.LocationInstance instance)
    {
        if (!cache.TryGetValue(key, out var list))
            cache[key] = list = [];

        list.Add(instance);
    }

    private bool HaveLocationInRange(AssetID asset, string group, Vector3 point, float radius, bool maxGroup)
    {
        if (byAsset.TryGetValue(asset, out var same) && AnyWithin(same, point, radius))
            return true;
        if (group.Length > 0 && !maxGroup && byGroup.TryGetValue(group, out var grouped) && AnyWithin(grouped, point, radius))
            return true;
        if (group.Length > 0 && maxGroup && byMaxGroup.TryGetValue(group, out var maxGrouped) && AnyWithin(maxGrouped, point, radius))
            return true;
        return false;
    }

    private static bool AnyWithin(List<ZoneSystem.LocationInstance> instances, Vector3 point, float radius)
    {
        return instances.Any(instance => (instance.m_position - point).sqrMagnitude < radius * radius);
    }
}
