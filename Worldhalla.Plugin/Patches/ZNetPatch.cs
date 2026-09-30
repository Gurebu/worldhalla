using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Worldhalla.API;
using Worldhalla.API.Settings;
using Worldhalla.Plugin.Extensions;
using Worldhalla.Plugin.Internal;

namespace Worldhalla.Plugin.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(ZNet))]
public static class ZNetPatch
{
    /*
     * TODO I don't like this comment, but what to write?
     * 
     * Peers only have to agree on Worldhalla when the shared world is a Worldhalla world, so nothing
     * Worldhalla sends may change what a vanilla peer reads: vanilla's own packages go out untouched, and
     * both RPCs below are unknown to a vanilla peer, which drops them unread.
     *
     * A client never reads the world's .fwl - it builds its World purely from the server's peer info - so
     * the worldgen and its settings have to travel over the wire too, or the client silently generates a
     * different world than the one the server loaded.
     *
     * WorldhallaHandshake, client to server, sent in reply to ClientHandshake and so ahead of the client's
     * PeerInfo:
     * - [string][1-255 bytes] plugin version
     * - [int][4 bytes] number of installed generators, then for each:
     *   - [string][1-255 bytes] custom worldgen ID
     *   - [string][1-255 bytes] worldgen version
     *
     * WorldhallaWorld, server to client, directly ahead of the server's PeerInfo and only for a Worldhalla
     * world:
     * - [string][1-255 bytes] plugin version
     * - [string][1-255 bytes] custom worldgen ID
     * - [int][4 bytes] worldhalla settings blob length
     * - [variable] worldhalla settings blob, as in the .fwl
     * - [int][4 bytes] generator settings blob length
     * - [variable] generator settings blob, as in the .fwl
     *
     * WorldhallaRefused, server to client, directly ahead of the Error that refuses a Worldhalla client, so
     * the client can tell the player why:
     * - [string][1-255 bytes] plugin version
     * - [int][4 bytes] refusal reason
     * - [string][1-255 bytes] custom worldgen ID
     * - [string][1-255 bytes] worldgen name
     * - [string][1-255 bytes] worldgen version
     *
     * Peers are compatible when the major and minor plugin versions match. The RPC names and the leading
     * plugin version are fixed forever; the rest of a payload may only change with a minor version bump.
     *
     * Both must be sent before the sender's PeerInfo: a connection delivers in order, which is what makes
     * them arrive before the PeerInfo they qualify, and a PlayFab socket starts compressing once PeerInfo
     * has been handled (ISocket.VersionMatch).
     */

    private const string HANDSHAKE_RPC = "WorldhallaHandshake";
    private const string WORLD_RPC = "WorldhallaWorld";
    private const string REFUSED_RPC = "WorldhallaRefused";

    private enum Refusal
    {
        WorldhallaVersion,
        GeneratorMissing,
        GeneratorVersion,
    }

    private static readonly System.Version LocalVersion = new(PluginInfo.PLUGIN_VERSION);

    private class PeerHandshake
    {
        public string pluginVersion = "";
        public readonly Dictionary<string, string> generatorVersions = new();
    }

    private static readonly ConditionalWeakTable<ZRpc, PeerHandshake> PeerHandshakes = new();
    private static readonly ConditionalWeakTable<ZRpc, ZPackage> ServerWorlds = new();

    private static string? refusalMessage;

    [HarmonyPriority(Priority.VeryHigh), HarmonyPrefix, HarmonyPatch(nameof(ZNet.OnNewConnection))]
    private static void OnNewConnection(ZNet __instance, ZNetPeer peer)
    {
        if (__instance.IsServer())
            peer.m_rpc.Register<ZPackage>(HANDSHAKE_RPC, RPC_WorldhallaHandshake);
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

        rpc.Invoke(HANDSHAKE_RPC, handshake);
    }

    private static void RPC_WorldhallaHandshake(ZRpc rpc, ZPackage package)
    {
        var handshake = new PeerHandshake { pluginVersion = package.ReadString() };

        if (IsCompatible(handshake.pluginVersion))
        {
            var count = package.ReadInt();
            for (var i = 0; i < count; i++)
                handshake.generatorVersions[package.ReadString()] = package.ReadString();
        }

        PeerHandshakes.AddOrUpdate(rpc, handshake);
    }

    private static bool IsCompatible(string remoteVersion)
    {
        return System.Version.TryParse(remoteVersion, out var version)
               && version.Major == LocalVersion.Major
               && version.Minor == LocalVersion.Minor;
    }

    private static Refusal? FindRefusal(PeerHandshake handshake, string guid, IWorldGenerator worldgen)
    {
        if (!IsCompatible(handshake.pluginVersion))
            return Refusal.WorldhallaVersion;

        if (!handshake.generatorVersions.TryGetValue(guid, out var version))
            return Refusal.GeneratorMissing;

        return version == worldgen.Version ? null : Refusal.GeneratorVersion;
    }

    [HarmonyPriority(Priority.VeryHigh), HarmonyPrefix, HarmonyPatch(nameof(ZNet.RPC_PeerInfo))]
    private static bool RPC_PeerInfo(ZNet __instance, ZRpc rpc)
    {
        var world = ZNet.World;
        if (!__instance.IsServer() || world.GetWorldgen() is not { } worldgen)
            return true;

        var guid = world.GetWorldgenGuid()!;
        var peer = rpc.GetSocket().GetEndPointString();
        var refusing = $"Refusing peer {peer}: world '{world.m_name}' is created with '{guid}' {worldgen.Version}";

        if (!PeerHandshakes.TryGetValue(rpc, out var handshake))
        {
            Logger.LogWarning($"{refusing} and the peer does not run Worldhalla");
        }
        else if (FindRefusal(handshake, guid, worldgen) is { } refusal)
        {
            var problem = refusal switch
            {
                Refusal.WorldhallaVersion => $"runs Worldhalla {handshake.pluginVersion}, local is {LocalVersion}",
                Refusal.GeneratorMissing => "does not have it installed",
                _ => $"has version {handshake.generatorVersions[guid]}",
            };

            Logger.LogWarning($"{refusing} and the peer {problem}");
            SendRefusal(rpc, refusal, guid, worldgen);
        }
        else
        {
            Logger.LogInfo($"Peer {peer} runs Worldhalla {handshake.pluginVersion}");
            return true;
        }

        rpc.Invoke("Error", (int)ZNet.ConnectionStatus.ErrorVersion);

        return false;
    }

    private static void SendRefusal(ZRpc rpc, Refusal refusal, string guid, IWorldGenerator worldgen)
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
            Refusal.GeneratorMissing => $"This world was created with {name} {version}. Install it to join.",
            Refusal.GeneratorVersion when Registry.FindByGuid(guid, out var local) =>
                $"This world needs {name} {version}. You have {local.Version}.",
            Refusal.GeneratorVersion => $"This world needs {name} {version}.",
            _ => WorldhallaVersionMessage(serverVersion),
        };
    }

    private static string WorldhallaVersionMessage(string serverVersion)
    {
        return $"The server runs Worldhalla {serverVersion}. You have {LocalVersion}.";
    }

    internal static string? TakeRefusalMessage()
    {
        var message = refusalMessage;
        refusalMessage = null;

        return message;
    }

    [HarmonyPriority(Priority.VeryHigh), HarmonyPrefix, HarmonyPatch(nameof(ZNet.SendPeerInfo))]
    private static void SendPeerInfo(ZNet __instance, ZRpc rpc)
    {
        var world = ZNet.World;
        if (!__instance.IsServer() || world.GetWorldgen() is not { } worldgen)
            return;

        var package = new ZPackage();

        package.Write(PluginInfo.PLUGIN_VERSION);
        package.Write(world.GetWorldgenGuid()!);
        package.Write(new ZPackage().GetArray());
        package.Write(world.SaveSettings());

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
                "The server's world has no seed.");

        var worldgenType = package.ReadString();
        if (!Registry.FindByGuid(worldgenType, out var worldgen))
            return RefuseWorld(rpc, $"Unknown custom worldgen '{worldgenType}'",
                $"This world was created with '{worldgenType}', which you don't have installed.");

        Logger.LogInfo($"Joining world '{world.m_name}' created with '{worldgenType}'");

        package.ReadByteArray();

        ISettings? settings;

        try
        {
            settings = worldgen.LoadSettings(package.ReadByteArray());
        }
        catch (SettingsException e)
        {
            return RefuseWorld(rpc, $"World settings rejected by '{worldgenType}': {e.Message}",
                $"{worldgen.Name} can't load this world's settings: {e.Message}");
        }
        catch (Exception e)
        {
            return RefuseWorld(rpc, $"'{worldgenType}' failed to load the world settings: {e}",
                $"{worldgen.Name} failed to load this world's settings. See the log for details.");
        }

        world.SetWorldgen(worldgenType, worldgen);
        world.SetSettings(settings);

        return true;
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
            .CreateLabel(out var label)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_1))
            .InsertAndAdvance(CodeInstruction.Call(typeof(ZNetPatch), nameof(ApplyServerWorld)))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brtrue_S, label))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ret))
            .InstructionEnumeration();
    }
}
