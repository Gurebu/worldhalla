using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using HarmonyLib;
using Worldhalla.Plugin.Compat;
using Worldhalla.Plugin.Extensions;
using Worldhalla.Plugin.Internal;

namespace Worldhalla.Plugin.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(World))]
public static class WorldPatch
{
    /*
     * Save a negative int marker before world version to identify modded worlds
     *
     * If a modded game tries to load an unmodded world, the first int will be world version which is not going to match the marker
     * If an unmodded game tries to load a modded world, first int will be the marker which is not a valid world version
     *
     * In both cases, worlds will fail to load without damage done
     *
     * The complete world format is:
     * - [int][4 bytes] .fwl size
     * ----------------- START OF CUSTOM HEADER -------------------
     * - [uint][4 bytes] worldhalla header (this is what the unmodded game reads as world version):
     *   - [bit 31] sign bit that doubles as the worldhalla world flag. Always on
     *   - [bits 30-8] padding
     *   - [bits 7-0] protocol version
     * ------------------ END OF CUSTOM HEADER --------------------
     * - [int][4 bytes] original world version
     * ---------------- START OF CUSTOM WORLD DATA ----------------
     * - [int][4 bytes] payload length
     * - [variable] payload, itself a ZPackage holding:
     *   - [string][1-255 bytes] custom worldgen ID
     *   - [int][4 bytes] settings blob length
     *   - [variable] settings blob, opaque - written and read by the generator alone
     * ----------------- END OF CUSTOM WORLD DATA -----------------
     * - [...] original world data
     *
     * The payload is length-prefixed so a reader that does not care about its contents can skip it
     * without parsing anything inside.
     *
     * Worlds written before the protocol version byte existed carry 0xff in its place, hold the
     * worldgen ID before the original world version rather than after it, and store the settings
     * inline with no length in front of them - see RiverheimMigration
     */

    private const uint CUSTOM_WORLDGEN_FLAG = 0x80000000;
    private const uint PROTOCOL_VERSION_MASK = 0x000000ff;
    internal const byte PROTOCOL_VERSION = 0x00;

    internal struct ProtocolInfo
    {
        public bool isModded;
        public byte version;

        public readonly bool IsLegacyRiverheim() => version == RiverheimMigration.PROTOCOL_VERSION;
    }

    internal static void WriteHeader(ZPackage package, World world)
    {
        if (world.IsVanillaWorld())
            return;
        
        package.Write(CUSTOM_WORLDGEN_FLAG | PROTOCOL_VERSION);
    }

    internal static void WriteWorldData(ZPackage package, World world)
    {
        if (world.IsVanillaWorld())
            return;
        
        var payload = new ZPackage();

        payload.Write(world.GetWorldgen()!.Guid);
        payload.Write(world.GetSettings().Serialize());

        package.Write(payload.GetArray());
    }

    internal static bool ReadHeader(string name, ZPackage package, out ProtocolInfo protocolInfo)
    {
        // TODO vanilla worlds

        protocolInfo = new ProtocolInfo
        {
            isModded = false,
            version = PROTOCOL_VERSION,
        };
        
        var header = package.ReadUInt();
        if ((header & CUSTOM_WORLDGEN_FLAG) == 0)
        {
            Logger.LogInfo($"World '{name}' doesn't have the custom worldgen flag, instead got world version: {(int)header}, skipping");
            return false;
        }

        protocolInfo.version = (byte)(header & PROTOCOL_VERSION_MASK);
        if (protocolInfo.IsLegacyRiverheim())
        {
            Logger.LogInfo($"World '{name}' is a legacy riverheim world, preparing for migratory read");
            
            var worldgenGuid = package.ReadString();
            if (worldgenGuid != RiverheimMigration.GUID)
            {
                Logger.LogInfo($"Found a legacy world '{name}', but it has incorrect worldgen GUID '{worldgenGuid}'");
                return false;
            }

            return true;
        }

        if (protocolInfo.version > PROTOCOL_VERSION)
        {
            Logger.LogInfo($"World '{name}': unsupported worldhalla protocol version {protocolInfo.version}, current version is {PROTOCOL_VERSION}");
            return false;
        }
        
        return true;
    }

    internal static bool ReadWorldData(string name, ZPackage package, World world, ProtocolInfo protocolInfo)
    {
        // TODO handle vanilla worlds
        
        string worldgenGuid;
        byte[] settings;

        try
        {
#pragma warning disable Harmony003
            var migrate = protocolInfo.IsLegacyRiverheim();
#pragma warning restore Harmony003
            (worldgenGuid, settings) = migrate
                ? RiverheimMigration.MigratoryRead(package)
                : ReadPayload(package);
        }
        catch (Exception e)
        {
            Logger.LogInfo($"Error reading world '{name}': {e.Message}, skipping");
            return false;
        }
        
        if (!Registry.FindByGuid(worldgenGuid, out var worldgen))
        {
            Logger.LogInfo($"Unknown worldgen guid '{worldgenGuid}', skipping");
            return false;
        }

        try
        {
            // TODO maybe this is one method?
            world.SetWorldgen(worldgen);
            world.SetSettings(worldgen.DeserializeSettings(settings, out var message));
        }
        catch (Exception e) // TODO ermmms
        {
            Logger.LogInfo($"World settings malformed or unsupported: {e.Message}, skipping");
            return false;
        }

        return true;
    }

    private static (string, byte[]) ReadPayload(ZPackage package)
    {
        var payload = new ZPackage(package.ReadByteArray());
        
        var worldgenGuid = payload.ReadString();
        var settings = payload.ReadByteArray();

        return (worldgenGuid, settings);
    }

    [HarmonyTranspiler]
    [HarmonyPatch(nameof(World.SaveWorldFWLData),
        [typeof(DateTime), typeof(FileWriter)],
        [ArgumentType.Normal, ArgumentType.Out])]
    internal static IEnumerable<CodeInstruction> TranspileWorldSave(IEnumerable<CodeInstruction> instructions)
    {
        var ld_World = new CodeInstruction(OpCodes.Ldarg_0);
        
        return new CodeMatcher(instructions)
            .AdvanceToAfterZPackageConstructor(out var ld_ZPackage)
            .InsertAndAdvance(ld_ZPackage)
            .InsertAndAdvance(ld_World)
            .InsertAndAdvance(CodeInstruction.Call(typeof(WorldPatch), nameof(WriteHeader)))
            .AdvanceToAfterWorldVersionWrite()
            .InsertAndAdvance(ld_ZPackage)
            .InsertAndAdvance(ld_World)
            .InsertAndAdvance(CodeInstruction.Call(typeof(WorldPatch), nameof(WriteWorldData)))
            .InstructionEnumeration();
    }

    [HarmonyTranspiler]
    [HarmonyPatch(nameof(World.LoadWorld))]
    internal static IEnumerable<CodeInstruction> TranspileWorldLoad(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var protocolInfo = generator.DeclareLocal(typeof(ProtocolInfo));
        var worldName = generator.DeclareLocal(typeof(string));
        var ld_WorldName = new CodeInstruction(OpCodes.Ldloc, worldName);

        return new CodeMatcher(instructions, generator)
            .MatchBadVersionWorldConstructor(out var label)
            .Start()
            .AdvanceToAfterZPackageConstructor(out var ld_ZPackage)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(SaveWithBackups), nameof(SaveWithBackups.Name))))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Stloc, worldName))
            .InsertAndAdvance(ld_WorldName)
            .InsertAndAdvance(ld_ZPackage)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloca, protocolInfo))
            .InsertAndAdvance(CodeInstruction.Call(typeof(WorldPatch), nameof(ReadHeader)))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brfalse_S, label))
            .AdvanceToAfterDefaultWorldConstructor(out var ld_World)
            .InsertAndAdvance(ld_WorldName)
            .InsertAndAdvance(ld_ZPackage)
            .InsertAndAdvance(ld_World)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc, protocolInfo))
            .InsertAndAdvance(CodeInstruction.Call(typeof(WorldPatch), nameof(ReadWorldData)))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brfalse_S, label))
            .InstructionEnumeration();
    }
}
