using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using HarmonyLib;
using Worldhalla.API.Settings;
using Worldhalla.Plugin.Compat;
using Worldhalla.Plugin.Core.Extensions;

namespace Worldhalla.Plugin.Core.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(World))]
public static class WorldPatch
{
    private const uint CUSTOM_WORLDGEN_FLAG = 0x80000000;
    private const uint FORMAT_VERSION_MASK = 0x000000ff;
    public const byte FORMAT_VERSION = 0x00;

    public struct FormatInfo
    {
        public bool isModded;
        public byte version;

        public readonly bool IsLegacyRiverheim() => version == RiverheimMigration.FORMAT_VERSION;
    }

    internal static void WriteHeader(ZPackage package, World world)
    {
        if (world.GetWorldgen() == null)
            return;

        package.Write(CUSTOM_WORLDGEN_FLAG | FORMAT_VERSION);
    }

    internal static void WriteWorldData(ZPackage package, World world)
    {
        if (world.GetWorldgen() is not { } worldgen)
            return;

        var payload = new ZPackage();
        WorldgenPayload.Write(payload, worldgen);
        package.Write(payload.GetArray());

        if (worldgen is InstalledWorldgen installed)
            installed.LegacyFormat = false;
    }

    internal static bool ReadHeader(string name, ZPackage package, out FormatInfo formatInfo)
    {
        formatInfo = new FormatInfo { isModded = true, version = FORMAT_VERSION };
        var headerStart = package.GetPos();
        
        var header = package.ReadUInt();
        if ((header & CUSTOM_WORLDGEN_FLAG) == 0)
        {
            package.SetPos(headerStart);
            formatInfo.isModded = false;
            
            Logger.LogInfo($"World '{name}' seems to be a vanilla world");
            return true;
        }

        formatInfo.version = (byte)(header & FORMAT_VERSION_MASK);
        if (formatInfo.IsLegacyRiverheim())
        {
            Logger.LogInfo($"World '{name}' is a legacy riverheim world, preparing for migratory read");
            
            var worldgenGuid = package.ReadString();
            if (worldgenGuid != RiverheimMigration.GUID_RELEASE_STABLE)
            {
                Logger.LogInfo($"Found a legacy world '{name}', but it has incorrect worldgen GUID '{worldgenGuid}'");
                return false;
            }

            return true;
        }

        if (formatInfo.version > FORMAT_VERSION)
        {
            Logger.LogInfo($"World '{name}': unsupported worldhalla format version {formatInfo.version}, current version is {FORMAT_VERSION}");
            return false;
        }
        
        Logger.LogInfo($"World '{name}': reading as a worldhalla world");
        
        return true;
    }

    internal static bool ReadWorldData(string name, ZPackage package, World world, in FormatInfo formatInfo)
    {
        if (!formatInfo.isModded)
            return true;
        
        string worldgenGuid;
        byte[] settings;

        try
        {
            var migrate = formatInfo.IsLegacyRiverheim();
            (worldgenGuid, settings) = migrate
                ? RiverheimMigration.ReleaseMigratoryRead(package)
                : ReadPayload(package);
        }
        catch (Exception e)
        {
            Logger.LogInfo($"Error reading world '{name}': {e.Message}, skipping");
            return false;
        }
        
        if (!Registry.FindByGuid(worldgenGuid, out var worldgen))
        {
            Logger.LogInfo($"Missing generator for guid '{worldgenGuid}', marking world '{name}' unloadable");
            world.SetWorldgen(new MissingWorldgen(worldgenGuid, settings));
            world.m_dataError = World.SaveDataError.BadVersion;
            return true;
        }

        try
        {
            world.SetWorldgen(new InstalledWorldgen(worldgen, worldgen.LoadSettings(settings))
            {
                LegacyFormat = formatInfo.IsLegacyRiverheim(),
            });
        }
        catch (SettingsException e)
        {
            Logger.LogInfo($"World '{name}': settings rejected by '{worldgenGuid}': {e.Message}, skipping");
            return false;
        }
        catch (Exception e)
        {
            Logger.LogError($"World '{name}': '{worldgenGuid}' failed to load its settings, skipping: {e}");
            return false;
        }

        return true;
    }

    private static (string, byte[]) ReadPayload(ZPackage package)
    {
        var payload = new ZPackage(package.ReadByteArray());

        return (WorldgenPayload.ReadGuid(payload), WorldgenPayload.ReadSettings(payload));
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
        var formatInfo = generator.DeclareLocal(typeof(FormatInfo));
        var ld_FormatInfo = new CodeInstruction(OpCodes.Ldloca, formatInfo);
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
            .InsertAndAdvance(ld_FormatInfo)
            .InsertAndAdvance(CodeInstruction.Call(typeof(WorldPatch), nameof(ReadHeader)))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brfalse_S, label))
            .AdvanceToAfterDefaultWorldConstructor(out var ld_World)
            .InsertAndAdvance(ld_WorldName)
            .InsertAndAdvance(ld_ZPackage)
            .InsertAndAdvance(ld_World)
            .InsertAndAdvance(ld_FormatInfo)
            .InsertAndAdvance(CodeInstruction.Call(typeof(WorldPatch), nameof(ReadWorldData)))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brfalse_S, label))
            .InstructionEnumeration();
    }
}
