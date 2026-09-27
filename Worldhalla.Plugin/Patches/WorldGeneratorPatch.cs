using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using UnityEngine;
using Worldhalla.API;
using Worldhalla.Plugin.Extensions;

namespace Worldhalla.Plugin.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(WorldGenerator))]
public static class WorldGeneratorPatch
{
    private const float FOREST_FACTOR_MULTIPLIER = 2.15f;
    private static readonly ThreadLocal<(Vector2, IWorldState.PointData)> cache = new();

    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.Pregenerate))]
    private static bool Pregenerate(WorldGenerator __instance)
    {
        var world = __instance.m_world;
        if (world.m_menu || world.IsVanillaWorld())
            return true;
        
        if (world.m_seed == 0)
            throw new InvalidOperationException($"Seed is 0 for world '{world.m_name}', but it is not a menu world");

        var worldgen = world.GetWorldgen()!;

        __instance.SetState(worldgen.Generate(world.m_seed, world.GetSettings()));

        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.GetBiomeHeight))]
    private static bool GetBiomeHeight(
        WorldGenerator __instance,
        Heightmap.Biome biome,
        float wx,
        float wy,
        out Color mask,
        ref float __result)
    {
        mask = Color.black;

        if (__instance.GetState() is not {} state)
            return true;

        __result = GetHeight(state, wx, wy, out mask);

        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.GetHeight))]
    [HarmonyPatch([typeof(float), typeof(float)])]
    private static bool GetHeight(
        WorldGenerator __instance,
        float wx,
        float wy,
        ref float __result)
    {
        if (__instance.GetState() is not {} state)
            return true;
        
        __result = GetHeight(state, wx, wy, out _);
        
        return false;
    }
    
    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.GetHeight))]
    [HarmonyPatch([typeof(float), typeof(float), typeof(Color)], [ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out])]
    private static bool GetHeightWithMask(
        WorldGenerator __instance,
        float wx,
        float wy,
        out Color mask,
        ref float __result)
    {
        mask = Color.black;
        
        if (__instance.GetState() is not {} state)
            return true;
        
        __result = GetHeight(state, wx, wy, out mask);
        
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.GetBiome), typeof(float), typeof(float), typeof(float), typeof(bool))]
    private static bool GetBiome(
        WorldGenerator __instance,
        float wx,
        float wy,
        ref Heightmap.Biome __result)
    {
        if (__instance.GetState() is not {} state)
            return true;

        __result = (Heightmap.Biome)state.GetBiome(wx, wy);

        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.GetForestFactor))]
    private static bool GetForestFactor(
        ref Vector3 pos,
        ref float __result)
    {
        if (WorldGenerator.instance.GetState() is not {} state)
            return true;
        
        __result = FOREST_FACTOR_MULTIPLIER * (1 - state.GetForestation(pos.x, pos.z));

        return false;
    }

    /* Unfortunate hack to ensure map generates properly */
    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.GetAshlandsHeight))]
    private static bool GetAshlandsHeight(
        WorldGenerator __instance,
        float wx,
        float wy,
        out Color mask,
        ref float __result)
    {
        mask = Color.black;

        if (__instance.GetState() is not {} state)
            return true;

        __result = GetHeight(state, wx, wy, out mask);

        return false;
    }

    // TODO take this from worldgen settings
    private const float heightOffset = 30.0f;
    
    private static float GetHeight(IWorldState state, float wx, float wy, out Color mask)
    {
        // TODO only cache if the blending feature is enabled
        // TODO remove riverheim-related commentary
        
        /*
         * Valheim does its biome blending during chunk generation.
         * If any of the 4 corners of the chunk differ in their biomes (border chunk),
         * it will query height 4 times for each point of the chunk and do bilinear interpolation.
         *
         * For Riverheim this is a massive waste since it does its own biome blending.
         *
         * So we cache a single height value so that it's only computed once instead of 4 times.
         * Cache has to be thread-local since chunk generation can happen on worker threads.
         */

        var pos = new Vector2(wx, wy);
        if (!cache.IsValueCreated || cache.Value.Item1 != pos)
            cache.Value = (pos, state.RenderPoint(wx, wy));

        var pointData = cache.Value.Item2;
            
        /*
         * Original game mask channel format:
         * R - dirt
         * G - cultivated land everywhere, snow in the deep north
         * B - stone pavement
         * A - varies, in most biomes it controls vegetation spawning & the ability to cultivate land
         *   - in mistlands, this channel additionally determines ground texture, low values are rocky
         *   - in ashlands, this channel is instead lava
         */
        
        mask = new Color(
            pointData.dirt,
            pointData.snow,
            pointData.pave,
            Mathf.Max(pointData.lava, pointData.fertility));
            
        return heightOffset + pointData.height;
    }
}
