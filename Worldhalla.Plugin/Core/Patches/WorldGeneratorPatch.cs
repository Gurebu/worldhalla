using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using UnityEngine;
using Worldhalla.Plugin.Core.Extensions;
using Worldhalla.Plugin.Localization;

namespace Worldhalla.Plugin.Core.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(WorldGenerator))]
public static class WorldGeneratorPatch
{
    private const int KEEPALIVE_INTERVAL_MS = 1000;
    private static string? failureMessage;

    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.Pregenerate))]
    private static bool Pregenerate(WorldGenerator __instance)
    {
        var world = __instance.m_world;
        if (world.m_menu || world.GetGenerator() is not CompatibleGeneratorBinding worldgen)
            return true;

        try
        {
            __instance.SetState(Generate(world, worldgen));
        }
        catch (Exception e)
        {
            Logger.LogError($"'{worldgen.Guid}' failed to generate world '{world.m_name}': {e}");

            if (ZNet.instance.IsServer())
                FailWorldLoad(worldgen);
        }

        return false;
    }

    private static WorldStateWrapper Generate(World world, CompatibleGeneratorBinding generator)
    {
        if (world.m_seed == 0)
            Logger.LogWarning($"World '{world.m_name}' has seed 0, this is probably an error");

        var generation = WorldGenerationTask.Start(generator.Generator, world.m_seed, generator.GeneratorSettings);

        var keepingAlive = true;
        while (!generation.Wait(KEEPALIVE_INTERVAL_MS))
        {
            if (keepingAlive)
                keepingAlive = SendPingReplies();
        }

        return generation.Wait();
    }

    private static void FailWorldLoad(CompatibleGeneratorBinding generator)
    {
        failureMessage = Translations.Get("$wh_generation_failed", generator.Generator.Name);
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

        if (__instance.GetState() is not {} world)
            return true;

        __result = world.GetHeight(biome, wx, wy, out mask);

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
        if (__instance.GetState() is not { BiomeBlending: true })
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

        if (__instance.GetState() is not { BiomeBlending: true })
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
        if (__instance.GetState() is not {} world)
            return true;

        __result = world.GetBiome(wx, wy);

        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.GetForestFactor))]
    private static bool GetForestFactor(
        ref Vector3 pos,
        ref float __result)
    {
        if (WorldGenerator.instance.GetState() is not {} world)
            return true;
        
        __result = world.GetForestFactor(pos);

        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.IsAshlands))]
    private static bool IsAshlands(float x, float y, ref bool __result)
    {
        if (WorldGenerator.instance.GetState()?.Geography is not { } geography)
            return true;

        __result = geography.IsAshlands(x, y);

        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.IsDeepnorth))]
    private static bool IsDeepnorth(float x, float y, ref bool __result)
    {
        if (WorldGenerator.instance.GetState()?.Geography is not { } geography)
            return true;

        __result = geography.IsDeepNorth(x, y);

        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.GetAshlandsOceanGradient), typeof(float), typeof(float))]
    private static bool GetAshlandsOceanGradient(float x, float y, ref float __result)
    {
        if (WorldGenerator.instance.GetState()?.Geography is not { } geography)
            return true;

        __result = geography.GetAshlandsOceanGradient(x, y);

        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.DeepNorthWaveFade))]
    private static bool DeepNorthWaveFade(float wx, float wy, ref double __result)
    {
        if (WorldGenerator.instance.GetState()?.Geography is not { } geography)
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

        if (__instance.GetState() is not {} world)
            return true;

        __result = world.GetHeight(Heightmap.Biome.AshLands, wx, wy, out mask);

        return false;
    }
}
