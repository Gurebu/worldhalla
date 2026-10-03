using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Worldhalla.API.Settings;
using Worldhalla.Plugin.Core.Extensions;
using Worldhalla.Plugin.Localization;

namespace Worldhalla.Plugin.Core.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(ZNet))]
public static class ZNetPatch
{
    private const string CLIENT_INFO_RPC = "WorldhallaClientInfo";
    private const string WORLD_RPC = "WorldhallaWorld";
    private const string REFUSED_RPC = "WorldhallaRefused";

    private enum Refusal
    {
        WorldhallaVersion,
        GeneratorMissing,
        GeneratorVersion,
    }

    private static readonly System.Version LocalVersion = new(PluginInfo.PLUGIN_VERSION);

    private class WorldhallaClientInfo
    {
        public string pluginVersion = "";
        public readonly Dictionary<string, string> generatorVersions = new();
    }

    private static readonly ConditionalWeakTable<ZRpc, WorldhallaClientInfo> ClientInfos = new();
    private static readonly ConditionalWeakTable<ZRpc, ZPackage> ServerWorlds = new();

    private static string? refusalMessage;

    [HarmonyPriority(Priority.VeryHigh), HarmonyPrefix, HarmonyPatch(nameof(ZNet.OnNewConnection))]
    private static void OnNewConnection(ZNet __instance, ZNetPeer peer)
    {
        if (__instance.IsServer())
            peer.m_rpc.Register<ZPackage>(CLIENT_INFO_RPC, RPC_ReceiveClientInfo);
        else
        {
            refusalMessage = null;
            peer.m_rpc.Register<ZPackage>(WORLD_RPC, RPC_WorldhallaWorld);
            peer.m_rpc.Register<ZPackage>(REFUSED_RPC, RPC_WorldhallaRefused);
        }
    }

    [HarmonyPriority(Priority.VeryHigh), HarmonyPrefix, HarmonyPatch(nameof(ZNet.RPC_ClientHandshake))]
    private static void RPC_ClientHandshake(ZRpc rpc)
    {
        var handshake = new ZPackage();

        handshake.Write(PluginInfo.PLUGIN_VERSION);
        handshake.Write(Registry.Generators.Count);

        foreach (var (guid, generator) in Registry.Generators)
        {
            handshake.Write(guid);
            handshake.Write(generator.Version);
        }

        rpc.Invoke(CLIENT_INFO_RPC, handshake);
    }

    private static void RPC_ReceiveClientInfo(ZRpc rpc, ZPackage package)
    {
        var clientInfo = new WorldhallaClientInfo { pluginVersion = package.ReadString() };

        if (IsCompatible(clientInfo.pluginVersion))
        {
            var count = package.ReadInt();
            for (var i = 0; i < count; i++)
                clientInfo.generatorVersions[package.ReadString()] = package.ReadString();
        }

        ClientInfos.AddOrUpdate(rpc, clientInfo);
    }

    private static bool IsCompatible(string remoteVersion)
    {
        return System.Version.TryParse(remoteVersion, out var version)
               && version.Major == LocalVersion.Major
               && version.Minor == LocalVersion.Minor;
    }

    private static Refusal? FindRefusal(WorldhallaClientInfo info, string guid, RegisteredWorldgen worldgen)
    {
        if (!IsCompatible(info.pluginVersion))
            return Refusal.WorldhallaVersion;

        if (!info.generatorVersions.TryGetValue(guid, out var version))
            return Refusal.GeneratorMissing;

        return version == worldgen.Version ? null : Refusal.GeneratorVersion;
    }

    [HarmonyPriority(Priority.VeryHigh), HarmonyPrefix, HarmonyPatch(nameof(ZNet.RPC_PeerInfo))]
    private static bool RPC_PeerInfo(ZNet __instance, ZRpc rpc)
    {
        var world = ZNet.World;
        if (!__instance.IsServer() || world.GetWorldgen() is not InstalledWorldgen { Guid: var guid, Generator: var worldgen })
            return true;

        var peer = rpc.GetSocket().GetEndPointString();
        var refusing = $"Refusing peer {peer}: world '{world.m_name}' is created with '{guid}' {worldgen.Version}";

        if (!ClientInfos.TryGetValue(rpc, out var clientInfo))
        {
            Logger.LogWarning($"{refusing} and the peer does not run Worldhalla");
        }
        else if (FindRefusal(clientInfo, guid, worldgen) is { } refusal)
        {
            var problem = refusal switch
            {
                Refusal.WorldhallaVersion => $"runs Worldhalla {clientInfo.pluginVersion}, local is {LocalVersion}",
                Refusal.GeneratorMissing => "does not have it installed",
                _ => $"has version {clientInfo.generatorVersions[guid]}",
            };

            Logger.LogWarning($"{refusing} and the peer {problem}");
            SendRefusal(rpc, refusal, guid, worldgen);
        }
        else
        {
            Logger.LogInfo($"Peer {peer} runs Worldhalla {clientInfo.pluginVersion}");
            return true;
        }

        rpc.Invoke("Error", (int)ZNet.ConnectionStatus.ErrorVersion);

        return false;
    }

    private static void SendRefusal(ZRpc rpc, Refusal refusal, string guid, RegisteredWorldgen worldgen)
    {
        var package = new ZPackage();

        package.Write(PluginInfo.PLUGIN_VERSION);
        package.Write((int)refusal);
        package.Write(guid);
        package.Write(worldgen.Name);
        package.Write(worldgen.Version);

        rpc.Invoke(REFUSED_RPC, package);
    }

    private static void RPC_WorldhallaRefused(ZRpc rpc, ZPackage package)
    {
        var serverVersion = package.ReadString();
        if (!IsCompatible(serverVersion))
        {
            refusalMessage = WorldhallaVersionMessage(serverVersion);
            return;
        }

        var refusal = (Refusal)package.ReadInt();
        var guid = package.ReadString();
        var name = package.ReadString();
        var version = package.ReadString();

        refusalMessage = refusal switch
        {
            Refusal.GeneratorMissing => Translations.Get("$wh_refused_generator_missing", name, version),
            Refusal.GeneratorVersion when Registry.FindByGuid(guid, out var local) =>
                Translations.Get("$wh_refused_generator_version_local", name, version, local.Version),
            Refusal.GeneratorVersion => Translations.Get("$wh_refused_generator_version", name, version),
            _ => WorldhallaVersionMessage(serverVersion),
        };
    }

    private static string WorldhallaVersionMessage(string serverVersion)
    {
        return Translations.Get("$wh_refused_worldhalla_version", serverVersion, LocalVersion.ToString());
    }

    public static string? TakeRefusalMessage()
    {
        var message = refusalMessage;
        refusalMessage = null;

        return message;
    }

    [HarmonyPriority(Priority.VeryHigh), HarmonyPrefix, HarmonyPatch(nameof(ZNet.SendPeerInfo))]
    private static void SendPeerInfo(ZNet __instance, ZRpc rpc)
    {
        if (!__instance.IsServer() || ZNet.World.GetWorldgen() is not InstalledWorldgen worldgen)
            return;

        var package = new ZPackage();

        package.Write(PluginInfo.PLUGIN_VERSION);
        WorldgenPayload.Write(package, worldgen);

        rpc.Invoke(WORLD_RPC, package);
    }

    private static void RPC_WorldhallaWorld(ZRpc rpc, ZPackage package)
    {
        ServerWorlds.AddOrUpdate(rpc, package);
    }

    private static bool ApplyServerWorld(ZRpc rpc)
    {
        var world = ZNet.World;

        if (!ServerWorlds.TryGetValue(rpc, out var package))
        {
            Logger.LogInfo("Server world does not use custom worldgen, deferring to vanilla generation");
            return true;
        }

        var serverVersion = package.ReadString();
        if (!IsCompatible(serverVersion))
            return RefuseWorld(rpc, $"Server runs Worldhalla {serverVersion}, local is {LocalVersion}",
                WorldhallaVersionMessage(serverVersion));

        if (world.m_seed == 0)
            return RefuseWorld(rpc, $"Server world '{world.m_name}' has no seed",
                Translations.Get("$wh_refused_no_seed"));

        var worldgenType = WorldgenPayload.ReadGuid(package);
        if (!Registry.FindByGuid(worldgenType, out var worldgen))
            return RefuseWorld(rpc, $"Unknown custom worldgen '{worldgenType}'",
                Translations.Get("$wh_refused_generator_unknown", worldgenType));

        Logger.LogInfo($"Joining world '{world.m_name}' created with '{worldgenType}'");

        ISettings? settings;

        try
        {
            settings = worldgen.LoadSettings(WorldgenPayload.ReadSettings(package));
        }
        catch (SettingsException e)
        {
            return RefuseWorld(rpc, $"World settings rejected by '{worldgenType}': {e.Message}",
                Translations.Get("$wh_refused_settings_rejected", worldgen.Name, e.Message));
        }
        catch (Exception e)
        {
            return RefuseWorld(rpc, $"'{worldgenType}' failed to load the world settings: {e}",
                Translations.Get("$wh_refused_settings_failed", worldgen.Name));
        }

        world.SetWorldgen(new InstalledWorldgen(worldgen, settings));

        return true;
    }

    private static bool RefuseUngeneratedWorld(ZRpc rpc)
    {
        if (ZNet.World.GetWorldgen() is not InstalledWorldgen worldgen || WorldGenerator.instance.GetWorld() != null)
            return true;

        return RefuseWorld(rpc, $"'{worldgen.Guid}' failed to generate the world",
            Translations.Get("$wh_generation_failed", worldgen.Generator.Name));
    }

    private static bool RefuseWorld(ZRpc rpc, string reason, string message)
    {
        Logger.LogError($"Refusing to join server world: {reason}");

        refusalMessage = message;

        ZNet.SetExternalError(ZNet.ConnectionStatus.ErrorVersion);
        rpc.Invoke("Disconnect");

        return false;
    }

    [HarmonyTranspiler, HarmonyPatch(nameof(ZNet.RPC_PeerInfo))]
    internal static IEnumerable<CodeInstruction> TranspileRpcPeerInfo(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        return new CodeMatcher(instructions, generator)
            .AdvanceToAfterWorldGenVersionRead()
            .CreateLabel(out var applied)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_1))
            .InsertAndAdvance(CodeInstruction.Call(typeof(ZNetPatch), nameof(ApplyServerWorld)))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brtrue_S, applied))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ret))
            .AdvanceToAfterWorldGeneratorInitialize()
            .CreateLabel(out var generated)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_1))
            .InsertAndAdvance(CodeInstruction.Call(typeof(ZNetPatch), nameof(RefuseUngeneratedWorld)))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brtrue_S, generated))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ret))
            .InstructionEnumeration();
    }
}
