using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using UnityEngine;
using Worldhalla.API.World;
using Worldhalla.Plugin.Core.Extensions;
using Worldhalla.Plugin.Localization;

namespace Worldhalla.Plugin.Core.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(WorldGenerator))]
public static class WorldGeneratorPatch
{
    private const int KEEPALIVE_INTERVAL_MS = 1000;
    private static readonly ThreadLocal<(IWorldState, Vector2, IWorldState.PointData)> cache = new();

    private static string? failureMessage;

    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.Pregenerate))]
    private static bool Pregenerate(WorldGenerator __instance)
    {
        var world = __instance.m_world;
        if (world.m_menu || world.GetWorldgen() is not InstalledWorldgen worldgen)
            return true;

        try
        {
            __instance.SetWorld(Generate(world, worldgen));
        }
        catch (Exception e)
        {
            Logger.LogError($"'{worldgen.Guid}' failed to generate world '{world.m_name}': {e}");

            if (ZNet.instance.IsServer())
                FailWorldLoad(worldgen);
        }

        return false;
    }

    private static GeneratedWorld Generate(World world, InstalledWorldgen worldgen)
    {
        if (world.m_seed == 0)
            throw new InvalidOperationException($"Seed is 0 for world '{world.m_name}', but it is not a menu world");

        var generation = WorldGenerationTask.Start(worldgen.Generator, world.m_seed, worldgen.Settings);

        var keepingAlive = true;
        while (!generation.Wait(KEEPALIVE_INTERVAL_MS))
        {
            if (keepingAlive)
                keepingAlive = SendPingReplies();
        }

        return generation.Wait();
    }

    // Vanilla's own exit for a world that failed to load: the game logs out without saving and the menu reports it
    private static void FailWorldLoad(InstalledWorldgen worldgen)
    {
        failureMessage = Translations.Get("$wh_generation_failed", worldgen.Generator.Name);
        ZNet.m_loadError = true;

        if (ZNet.instance.IsDedicated())
            Application.Quit();
    }

    public static string? TakeFailureMessage()
    {
        var message = failureMessage;
        failureMessage = null;

        return message;
    }

    /*
     * A client generates inside its handler of the server's PeerInfo, and the blocked main thread can't
     * answer the server's pings, so a long generation gets the client timed out. An unrequested ping reply
     * resets the server's timeout all the same.
     */
    private static bool SendPingReplies()
    {
        if (ZNet.instance == null)
            return true;

        try
        {
            foreach (var peer in ZNet.instance.GetPeers())
            {
                if (peer.m_rpc.IsConnected())
                    ZRpcPatch.AnswerPing(peer.m_rpc, ZRpcPatch.PingRequest());
            }

            return true;
        }
        catch (Exception e)
        {
            Logger.LogWarning($"Stopped keeping the connection alive during world generation: {e}");
            return false;
        }
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

        if (__instance.GetWorld() is not {} world)
            return true;

        __result = GetHeight(world, (Biome)biome, wx, wy, out mask);

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
        if (__instance.GetWorld() is not { BiomeBlending: true })
            return true;

        __result = __instance.GetBiomeHeight(Heightmap.Biome.None, wx, wy, out _);

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

        if (__instance.GetWorld() is not { BiomeBlending: true })
            return true;

        __result = __instance.GetBiomeHeight(Heightmap.Biome.None, wx, wy, out mask);

        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.GetBiome), typeof(float), typeof(float), typeof(float), typeof(bool))]
    private static bool GetBiome(
        WorldGenerator __instance,
        float wx,
        float wy,
        ref Heightmap.Biome __result)
    {
        if (__instance.GetWorld() is not {} world)
            return true;

        __result = (Heightmap.Biome)world.State.GetBiome(wx, wy);

        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.GetForestFactor))]
    private static bool GetForestFactor(
        ref Vector3 pos,
        ref float __result)
    {
        if (WorldGenerator.instance.GetWorld() is not {} world)
            return true;
        
        __result = world.ForestFactor(pos);

        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.IsAshlands))]
    private static bool IsAshlands(float x, float y, ref bool __result)
    {
        if (WorldGenerator.instance.GetWorld()?.Geography is not { } geography)
            return true;

        __result = geography.IsAshlands(x, y);

        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.IsDeepnorth))]
    private static bool IsDeepnorth(float x, float y, ref bool __result)
    {
        if (WorldGenerator.instance.GetWorld()?.Geography is not { } geography)
            return true;

        __result = geography.IsDeepNorth(x, y);

        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.GetAshlandsOceanGradient), typeof(float), typeof(float))]
    private static bool GetAshlandsOceanGradient(float x, float y, ref float __result)
    {
        if (WorldGenerator.instance.GetWorld()?.Geography is not { } geography)
            return true;

        __result = geography.GetAshlandsOceanGradient(x, y);

        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.DeepNorthWaveFade))]
    private static bool DeepNorthWaveFade(float wx, float wy, ref double __result)
    {
        if (WorldGenerator.instance.GetWorld()?.Geography is not { } geography)
            return true;

        __result = geography.GetDeepNorthWaveFade(wx, wy);

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

        if (__instance.GetWorld() is not {} world)
            return true;

        __result = GetHeight(world, Biome.AshLands, wx, wy, out mask);

        return false;
    }

    private const float heightOffset = 30.0f;
    
    private static float GetHeight(GeneratedWorld world, Biome biome, float wx, float wy, out Color mask)
    {
        var pointData = world.BiomeBlending
            ? RenderCached(world.State, wx, wy)
            : world.State.RenderPoint(wx, wy, biome);
            
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

    /*
     * At chunks whose corners lie in different biomes the game renders every point once per corner biome and
     * interpolates between the results. A generator that blends biomes itself returns the same point four times,
     * so the last point is cached. Chunks are built on worker threads, hence thread-local.
     */
    private static IWorldState.PointData RenderCached(IWorldState state, float wx, float wy)
    {
        var pos = new Vector2(wx, wy);
        if (!cache.IsValueCreated || cache.Value.Item1 != state || cache.Value.Item2 != pos)
            cache.Value = (state, pos, state.RenderPoint(wx, wy, Biome.None));

        return cache.Value.Item3;
    }
}
