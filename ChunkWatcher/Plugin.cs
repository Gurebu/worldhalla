using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ChunkWatcher;

[BepInPlugin(GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private const string GUID = "dev.gurebu.chunkwatcher";
    public static Action<string> Log;
    
    public void Awake()
    {
        new Harmony(GUID).PatchAll(Assembly.GetExecutingAssembly());
        Log = message => Logger.Log(LogLevel.Info, message);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(HeightmapBuilder))]
public static class HeightmapBuilderPatch
{
    private static readonly object Lockobj = new();

    private static int callCount = 0;
    private static int bufferPos = 0;
    private static readonly double[] callTimes = new double[32];
    
    [HarmonyPrefix, HarmonyPatch(nameof(HeightmapBuilder.RequestTerrain))]
    private static void RequestTerrainPrefix(Vector3 center)
    {
        int calls;
        double callSum;
        
        lock (Lockobj)
        {
            calls = Math.Min(callCount, callTimes.Length);
            callSum = 0;
            for (int i = 0; i < calls; i++)
                callSum += callTimes[i];
        }

        if (calls == 0)
            return;
        
        Plugin.Log($"Avg heightmap build time over last {calls} builds: {callSum / calls * 1000.0:0.00}us");
    }
    
    [HarmonyPrefix, HarmonyPatch(nameof(HeightmapBuilder.Build))]
    private static bool BuildPrefix(HeightmapBuilder __instance, HeightmapBuilder.HMBuildData data)
    {
        var ts = DateTime.Now;
        
        BuildReversePatch(__instance, data);
        
        var duration = DateTime.Now - ts;

        lock (Lockobj)
        {
            callCount++;
            callTimes[bufferPos] = duration.TotalMilliseconds;
            bufferPos = (bufferPos + 1) % callTimes.Length;
        }

        return false;
    }

    [HarmonyReversePatch, HarmonyPatch(nameof(HeightmapBuilder.Build))]
    private static void BuildReversePatch(object instance, HeightmapBuilder.HMBuildData data) => throw new Exception();
}

[HarmonyPatch(typeof(WorldGenerator))]
public static class WorldGeneratorPatch
{
    [HarmonyPostfix, HarmonyPatch(nameof(WorldGenerator.Initialize))]
    private static void InitializePostfix()
    {
        Measure(WorldGenerator.instance);
    }
    
    private const int COUNT = 1000000;

    [MethodImpl(MethodImplOptions.NoOptimization)]
    private static void Measure(WorldGenerator worldGen)
    {
        Plugin.Log("Testing performance...");

        var worldSize = 10500f;

        var samples = new Vector2[COUNT];
        for (int i = 0; i < COUNT; i++)
        {
            var r = worldSize * Mathf.Sqrt(UnityEngine.Random.value);
            var a = UnityEngine.Random.value * Mathf.PI * 2;
            samples[i] = new Vector2(r * Mathf.Cos(a), r * Mathf.Sin(a));
        }

        var biomes = new Heightmap.Biome[COUNT];

        var now = DateTime.Now;
        for (int i = 0; i < COUNT; i++)
            biomes[i] = worldGen.GetBiome(samples[i].x, samples[i].y);
        var biomeTotal = (float)(DateTime.Now - now).TotalMilliseconds;

        now = DateTime.Now;
        for (int i = 0; i < COUNT; i++)
            worldGen.GetBiomeHeight(biomes[i], samples[i].x, samples[i].y, out _);
        var heightTotal = (float)(DateTime.Now - now).TotalMilliseconds;

        Plugin.Log($"GetBiome(): {biomeTotal * 1000_000.0 / COUNT:0.} ns/call");
        Plugin.Log($"GetBiomeHeight(): {heightTotal * 1000_000.0 / COUNT:0.00} ns/call");
        Plugin.Log($"Combined: {(biomeTotal + heightTotal) * 1000_000.0 / COUNT:0.00} ns/call");
    }
}
