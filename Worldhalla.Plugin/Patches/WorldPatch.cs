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
     * - [string][1-255 bytes] custom worldgen ID
     * - [int][4 bytes] settings blob length
     * - [variable] settings blob, opaque - written and read by the generator alone
     * ----------------- END OF CUSTOM WORLD DATA -----------------
     * - [...] original world data
     *
     * Worlds written before the protocol version byte existed carry 0xff in its place, hold the
     * worldgen ID before the original world version rather than after it, and store the settings
     * inline with no length in front of them - see RiverheimMigration
     */

    private const uint CUSTOM_WORLDGEN_FLAG = 0x80000000;
    private const uint PROTOCOL_VERSION_MASK = 0x000000ff;
    internal const byte PROTOCOL_VERSION = 0x00;

    private static void Log(string message) => Logger.LogInfo(message);

    internal static void WriteHeader(ZPackage package, World world)
    {
        // TODO handle vanilla world (need world instance lol)
        
        package.Write(CUSTOM_WORLDGEN_FLAG | PROTOCOL_VERSION);
    }

    internal static void WriteWorldData(ZPackage package, World world)
    {
        if (world.IsVanillaWorld())
            return;
        
        package.Write(world.GetWorldgen()!.Guid);
        package.Write(world.GetSettings().Serialize());
    }

    internal static bool ReadHeader(string name, ZPackage package, out byte protocolVersion)
    {
        Log($"Reading world '{name}'");

        // TODO vanilla worlds
        
        protocolVersion = PROTOCOL_VERSION;

        var header = package.ReadUInt();
        Log($"header byte is {header}");
        if ((header & CUSTOM_WORLDGEN_FLAG) == 0)
        {
            Log($"World '{name}' doesn't have the custom worldgen flag, instead got world version: {(int)header}, skipping");
            return false;
        }

        protocolVersion = (byte)(header & PROTOCOL_VERSION_MASK);
        if (protocolVersion == RiverheimMigration.PROTOCOL_VERSION)
        {
            Log($"World '{name}' is a legacy riverheim world, preparing for migratory read");
            
            var worldgenGuid = package.ReadString();
            if (worldgenGuid != RiverheimMigration.GUID)
            {
                Log($"Found a legacy world '{name}', but it has incorrect worldgen GUID '{worldgenGuid}'");
                return false;
            }

            return true;
        }

        if (protocolVersion > PROTOCOL_VERSION)
        {
            Log($"World '{name}': unsupported worldhalla protocol version {protocolVersion}, current version is {PROTOCOL_VERSION}");
            return false;
        }
        
        return true;
    }

    internal static bool ReadWorldData(string name, ZPackage package, World world, byte protocolVersion)
    {
        bool migrateRiverheimWorld = protocolVersion == RiverheimMigration.PROTOCOL_VERSION;
        
        // TODO handle vanilla worlds
        
        var worldgenGuid = migrateRiverheimWorld
            ? RiverheimMigration.GUID
            : package.ReadString();
        
        Log($"World '{name}' has worldgen guid '{worldgenGuid}'");
        
        if (!Registry.FindByGuid(worldgenGuid, out var worldgen))
        {
            Log($"Unknown worldgen guid '{worldgenGuid}', skipping");
            return false;
        }

        try
        {
            var payload = migrateRiverheimWorld
                ? RiverheimMigration.MigrateSettings(package)
                : package.ReadByteArray();

            // TODO maybe this is one method?
            world.SetWorldgen(worldgen);
            world.SetSettings(worldgen.DeserializeSettings(payload, out var message));
        }
        catch (Exception e) // TODO ermmms
        {
            Log($"World settings malformed or unsupported: {e.Message}, skipping");
            return false;
        }

        return true;
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
        var protocolVersion = generator.DeclareLocal(typeof(byte));
        var worldName = generator.DeclareLocal(typeof(string));
        var ld_WorldName = new CodeInstruction(OpCodes.Ldloc, worldName);

        return new CodeMatcher(instructions, generator)
            .MatchBadVersionWorldConstructor(out var label)
            .Start()
            .AdvanceToAfterZPackageConstructor(out var ld_ZPackage)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Callvirt,
                AccessTools.PropertyGetter(typeof(SaveWithBackups), nameof(SaveWithBackups.Name))))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Stloc, worldName))
            .InsertAndAdvance(ld_WorldName)
            .InsertAndAdvance(ld_ZPackage)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloca, protocolVersion))
            .InsertAndAdvance(CodeInstruction.Call(typeof(WorldPatch), nameof(ReadHeader)))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brfalse_S, label))
            .AdvanceToAfterDefaultWorldConstructor(out var ld_World)
            .InsertAndAdvance(ld_WorldName)
            .InsertAndAdvance(ld_ZPackage)
            .InsertAndAdvance(ld_World)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc, protocolVersion))
            .InsertAndAdvance(CodeInstruction.Call(typeof(WorldPatch), nameof(ReadWorldData)))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brfalse_S, label))
            .InstructionEnumeration();
    }
    
    // TODO you're suspicious
    
    [HarmonyPrefix, HarmonyPatch(nameof(World.GetCreateWorld))]
    private static void GetCreateWorld(ref World __result, string name, FileHelpers.FileSource source)
    {
        if (!SaveSystem.TryGetSaveByName(name, SaveDataType.World, out var save) || save.IsDeleted)
            return;

        __result = World.LoadWorld(save);
        if (__result.m_dataError == World.SaveDataError.None)
            return;

        throw new Exception($"Failed to load world with name \"{name}\", data error {__result.m_dataError}.");
    }
}
