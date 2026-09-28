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
    // TODO rethink handshake
    
    private static readonly System.Version LocalVersion = new(PluginInfo.PLUGIN_VERSION);
    private static readonly ConditionalWeakTable<ZRpc, System.Version> PeerVersions = new();

    private static bool VersionIsCompatible(System.Version remoteVersion)
    {
        return LocalVersion.Major == remoteVersion.Major && LocalVersion.Minor == remoteVersion.Minor;
    }

    private static void RPC_AssertModVersion(ZRpc sender, string versionString)
    {
        var version = new System.Version(versionString);

        PeerVersions.Add(sender, version);
        if (VersionIsCompatible(version))
            return;

        Logger.LogWarning($"Worldhalla plugin version mismatch! Local version: {LocalVersion}, remote: {version}");
        sender.Invoke("Error", ZNet.ConnectionStatus.ErrorVersion);
    }

    [HarmonyPriority(Priority.VeryHigh), HarmonyPrefix, HarmonyPatch(nameof(ZNet.OnNewConnection))]
    private static void OnNewConnection(ZNet __instance, ZNetPeer peer)
    {
        peer.m_rpc.Register<string>("AssertWorldhallaVersion", RPC_AssertModVersion);
    }

    [HarmonyPriority(Priority.VeryHigh), HarmonyPrefix, HarmonyPatch(nameof(ZNet.RPC_ClientHandshake))]
    private static void RPC_ClientHandshake(ZNet __instance, ZRpc rpc)
    {
        rpc.Invoke("AssertWorldhallaVersion", PluginInfo.PLUGIN_VERSION);
    }

    [HarmonyPriority(Priority.VeryHigh), HarmonyPrefix, HarmonyPatch(nameof(ZNet.RPC_ServerHandshake))]
    private static void RPC_ServerHandshake(ZNet __instance, ZRpc rpc)
    {
        rpc.Invoke("AssertWorldhallaVersion", PluginInfo.PLUGIN_VERSION);
    }

    [HarmonyPriority(Priority.VeryHigh), HarmonyPrefix, HarmonyPatch(nameof(ZNet.SendPeerInfo))]
    private static bool SendPeerInfo(ZNet __instance, ZRpc rpc)
    {
        if (ZNet.instance.IsServer())
            return true;

        if (PeerVersions.TryGetValue(rpc, out var version) && VersionIsCompatible(version))
            return true;

        Logger.LogWarning($"Worldhalla plugin version mismatch! Local version: {LocalVersion}, remote: {version}");
        rpc.Invoke("Disconnect");

        return false;
    }

    [HarmonyPriority(Priority.VeryHigh), HarmonyPrefix, HarmonyPatch(nameof(ZNet.RPC_PeerInfo))]
    private static bool RPC_PeerInfo(ZNet __instance, ZRpc rpc)
    {
        if (!ZNet.instance.IsServer())
            return true;

        if (PeerVersions.TryGetValue(rpc, out var version) && VersionIsCompatible(version))
            return true;

        Logger.LogWarning($"Worldhalla plugin version mismatch! Local version: {LocalVersion}, remote: {version}");
        rpc.Invoke("Error", ZNet.ConnectionStatus.ErrorVersion);

        return false;
    }

    /*
     * A client never reads the world's .fwl - it builds its World purely from the peer info
     * package - so the custom header and world settings have to travel over the wire too, or
     * the client silently falls back to whatever the current defaults happen to be and
     * generates a different world than the one the server loaded.
     *
     * The server appends to its peer info, directly after m_worldGenVersion (vanilla's
     * equivalent of our preset version, transmitted there for the very same reason):
     * ----------------- START OF CUSTOM HEADER -------------------
     * - [bool][1 byte] custom worldgen
     * - [string][1-255 bytes] custom worldgen ID (only when custom worldgen)
     * ------------------ END OF CUSTOM HEADER --------------------
     * - [int][1-2 bytes] number of settings, then each setting as in the .fwl
     *   (only when custom worldgen)
     */

    private static void WriteWorldData(ZPackage package)
    {
        var world = ZNet.World;
        var worldgen = world.GetWorldgen();

        package.Write(worldgen != null);

        if (worldgen == null)
            return;

        package.Write(worldgen.Guid);
        package.Write(world.SaveSettings());
    }

    private static bool ReadWorldData(ZPackage package, ZRpc rpc)
    {
        var world = ZNet.World;

        if (world.m_seed == 0)
            return RefuseWorld(rpc, $"Server world '{world.m_name}' has no seed");

        if (!package.ReadBool())
        {
            Logger.LogInfo("Server world does not use custom worldgen, deferring to vanilla generation");
            return true;
        }

        var worldgenType = package.ReadString();
        if (!Registry.FindByGuid(worldgenType, out var worldgen))
            return RefuseWorld(rpc, $"Unknown custom worldgen '{worldgenType}'");

        Logger.LogInfo($"Joining world '{world.m_name}' created with '{worldgen.Guid}'");

        ISettings? settings;

        try
        {
            settings = worldgen.LoadSettings(package.ReadByteArray());
        }
        catch (SettingsException e)
        {
            return RefuseWorld(rpc, $"World settings rejected by '{worldgen.Guid}': {e.Message}");
        }
        catch (Exception e)
        {
            return RefuseWorld(rpc, $"'{worldgen.Guid}' failed to load the world settings: {e}");
        }

        world.SetWorldgen(worldgen);
        world.SetSettings(settings);

        return true;
    }

    private static bool RefuseWorld(ZRpc rpc, string reason)
    {
        Logger.LogError($"Refusing to join server world: {reason}");

        ZNet.SetExternalError(ZNet.ConnectionStatus.ErrorVersion);
        rpc.Invoke("Disconnect");

        return false;
    }

    [HarmonyTranspiler, HarmonyPatch(nameof(ZNet.SendPeerInfo))]
    internal static IEnumerable<CodeInstruction> TranspileSendPeerInfo(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .AdvanceToAfterZPackageConstructor(out var ld_ZPackage)
            .AdvanceToAfterWorldGenVersionWrite()
            .InsertAndAdvance(ld_ZPackage)
            .InsertAndAdvance(CodeInstruction.Call(typeof(ZNetPatch), nameof(WriteWorldData)))
            .InstructionEnumeration();
    }

    [HarmonyTranspiler, HarmonyPatch(nameof(ZNet.RPC_PeerInfo))]
    internal static IEnumerable<CodeInstruction> TranspileRpcPeerInfo(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        return new CodeMatcher(instructions, generator)
            .AdvanceToAfterWorldGenVersionRead()
            .CreateLabel(out var label)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_2))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_1))
            .InsertAndAdvance(CodeInstruction.Call(typeof(ZNetPatch), nameof(ReadWorldData)))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brtrue_S, label))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ret))
            .InstructionEnumeration();
    }
}
