using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using UnityEngine;
using Worldhalla.API;
using Worldhalla.API.Settings;
using Worldhalla.Plugin.Core.Extensions;

namespace Worldhalla.Plugin.Core.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch]
public static class ServerStartupPatch
{
    private static CommandLineOptions ParseCommandLine() => CommandLine.Parse(Environment.GetCommandLineArgs());

    private static (string guid, IWorldGenerator worldgen, ISettings? settings)? ResolveRequest(CommandLineOptions options)
    {
        if (options.worldgenGuid == null)
            return null;

        if (!Registry.FindByGuid(options.worldgenGuid, out var worldgen))
        {
            var installed = string.Join(", ", Registry.Generators.Keys);
            throw new ServerStartupException($"Unknown worldgen '{options.worldgenGuid}', installed: [{installed}]");
        }

        return (options.worldgenGuid, worldgen, ParseSettings(options.worldgenGuid, worldgen, options.settings));
    }

    private static ISettings? ParseSettings(string guid, IWorldGenerator worldgen, string? text)
    {
        if (worldgen is not ISettingsHandler handler)
        {
            return text == null
                ? null
                : throw new ServerStartupException($"Worldgen '{guid}' takes no settings, but '{CommandLine.PREFIX} settings' was given");
        }

        if (text == null)
            return handler.DefaultSettings;

        try
        {
            var settings = handler.BuildSettingsFromCommandLine(text);
            SettingsBlob.Check(settings);

            return settings;
        }
        catch (SettingsException e)
        {
            throw new ServerStartupException($"Invalid settings '{text}' for worldgen '{guid}': {e.Message}\n{handler.CommandLineUsage}");
        }
        catch (Exception e)
        {
            throw new ServerStartupException($"Worldgen '{guid}' failed to parse settings '{text}': {e}");
        }
    }

    internal static void PrepareCreatedWorld(World world)
    {
        if (SaveSystem.TryGetSaveByName(world.m_name, SaveDataType.World, out var save) && !save.IsDeleted)
            throw new ServerStartupException($"World '{world.m_name}' exists but could not be loaded, refusing to replace it with a new one");

        if (ResolveRequest(ParseCommandLine()) is not var (guid, worldgen, settings))
            return;

        world.SetWorldgen(guid, worldgen);
        world.SetSettings(settings);

        Logger.LogInfo($"Creating world '{world.m_name}' with worldgen '{guid}'");
    }

    [HarmonyPrefix, HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.ParseServerArguments))]
    private static void ParseServerArgumentsPrefix()
    {
        // FejdStartup.Awake is not guaranteed to come after WorldhallaPlugin.Start, and both the
        // requested worldgen and an existing world's worldgen are looked up in the registry from here
        Registry.DiscoverGenerators();
        ResolveRequest(ParseCommandLine());
    }

    [HarmonyFinalizer, HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.ParseServerArguments))]
    private static Exception? ParseServerArgumentsFinalizer(Exception? __exception, ref bool __result)
    {
        if (__exception is not ServerStartupException)
            return __exception;

        Logger.LogError(__exception.Message);
        Application.Quit();
        __result = false;

        return null;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(World), nameof(World.GetCreateWorld))]
    private static void GetCreateWorldPrefix(string name)
    {
        if (ParseCommandLine().worldgenGuid == null)
            return;

        if (SaveSystem.TryGetSaveByName(name, SaveDataType.World, out var save) && !save.IsDeleted)
            Logger.LogWarning($"World '{name}' already exists and keeps the worldgen it was created with, '{CommandLine.PREFIX}' options are ignored");
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(World), nameof(World.GetCreateWorld))]
    internal static IEnumerable<CodeInstruction> TranspileGetCreateWorld(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .AdvanceToAfterNewWorldStore(out var ld_World)
            .InsertAndAdvance(ld_World)
            .InsertAndAdvance(CodeInstruction.Call(typeof(ServerStartupPatch), nameof(PrepareCreatedWorld)))
            .InstructionEnumeration();
    }
}
