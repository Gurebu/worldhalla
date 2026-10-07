using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using Worldhalla.API.World;

namespace Worldhalla.Plugin.Core;

public sealed class WorldStateWrapper(IWorldState state, bool biomeBlending)
{
    private const float HEIGHT_OFFSET = 30.0f;
    private const float FOREST_FACTOR_MULTIPLIER = 2.15f;

    private static int lastId;

    [ThreadStatic]
    private static (int, Vector2, IWorldState.PointData) cache;
    private readonly int id = Interlocked.Increment(ref lastId);

    [SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
    public IWorldGeography? Geography { get; } = state as IWorldGeography;

    public bool BiomeBlending { get; } = biomeBlending;

    public static WorldStateWrapper Of(RegisteredGenerator generator, IWorldState state)
    {
        return new WorldStateWrapper(state, generator.BiomeBlending);
    }
    
    public Heightmap.Biome GetBiome(float wx, float wy)
    {
        return (Heightmap.Biome)state.GetBiome(wx, wy);
    }
    
    public float GetHeight(Heightmap.Biome biome, float wx, float wy, out Color mask)
    {
        var pointData = BiomeBlending ? RenderCached(wx, wy) : state.RenderPoint(wx, wy, (Biome)biome);
        
        /*
         * Original game mask channel format:
         * 
         * R - dirt (game logic needs R > 0.5 for clear ground)
         * G - Deep North: snow (G > 0.15 blocks building placement)
         *   - Everywhere else: cultivated ground (G > 0.5 is plantable)
         * B - stone pavement (game logic needs B > 0.5 for clear ground)
         * A - Ashlands: lava (effects start at A > 0.2, full lava for all logic: A > 0.6)
         *   - Everywhere else: vegetation mask (values of A < 0.25 disable cultivation)
         *   - Mistlands: in addition to vegetation mask, low values change grass type and ground texture
         */
        
        mask = new Color(
            pointData.dirt,
            pointData.snow,
            pointData.pave,
            Mathf.Max(pointData.lava, pointData.fertility));
            
        return HEIGHT_OFFSET + pointData.height;
    }
    
    public float GetForestFactor(Vector3 position)
    {
        return FOREST_FACTOR_MULTIPLIER * (1 - state.GetForestation(position.x, position.z));
    }
    
    /*
     * At chunks whose corners lie in different biomes the game renders every point once per corner biome and
     * interpolates between the results. A generator that blends biomes itself returns the same point four times,
     * so the last point is cached. Chunks are built on worker threads, hence thread-static, and the entry names
     * its world by id so that a finished thread's entry doesn't keep a world state alive.
     */
    private IWorldState.PointData RenderCached(float wx, float wy)
    {
        var pos = new Vector2(wx, wy);
        if (cache.Item1 != id || cache.Item2 != pos)
            cache = (id, pos, state.RenderPoint(wx, wy, Biome.None));

        return cache.Item3;
    }
}
