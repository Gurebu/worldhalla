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
        if (world.GetGenerator() == null)
            return;

        package.Write(CUSTOM_WORLDGEN_FLAG | FORMAT_VERSION);
    }

    internal static void WriteWorldData(ZPackage package, World world)
    {
        if (world.GetGenerator() is not { } generator)
            return;

        var payload = new ZPackage();
        WorldPayload.Of(generator).Write(payload);
        package.Write(payload.GetArray());

        if (generator is CompatibleGeneratorBinding installed)
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
        
        WorldPayload payload;

        try
        {
            payload = formatInfo.IsLegacyRiverheim()
                ? RiverheimMigration.ReleaseMigratoryRead(package)
                : WorldPayload.Read(new ZPackage(package.ReadByteArray()));
        }
        catch (Exception e)
        {
            Logger.LogInfo($"Error reading world '{name}': {e.Message}, skipping");
            return false;
        }
        
        var worldgenGuid = payload.Guid;
        if (!Registry.FindByGuid(worldgenGuid, out var worldgen))
        {
            Logger.LogInfo($"Missing generator for guid '{worldgenGuid}', marking world '{name}' unloadable");
            MarkIncompatible(world, payload, Incompatibility.GeneratorMissing);
            return true;
        }

        WorldhallaSettings worldhallaSettings;

        try
        {
            worldhallaSettings = WorldhallaSettings.Load(payload.WorldhallaSettings);
        }
        catch (Exception e)
        {
            Logger.LogInfo($"World '{name}': unreadable Worldhalla settings: {e.Message}, marking it unloadable");
            MarkIncompatible(world, payload, Incompatibility.WorldhallaSettingsUnreadable);
            return true;
        }

        try
        {
            world.SetGenerator(new CompatibleGeneratorBinding(worldgen, worldhallaSettings, worldgen.LoadSettings(payload.GeneratorSettings))
            {
                LegacyFormat = formatInfo.IsLegacyRiverheim(),
            });
        }
        catch (SettingsException e)
        {
            Logger.LogInfo($"World '{name}': settings rejected by '{worldgenGuid}': {e.Message}, marking it unloadable");
            MarkIncompatible(world, payload, Incompatibility.GeneratorSettingsUnreadable);
        }
        catch (Exception e)
        {
            Logger.LogError($"World '{name}': '{worldgenGuid}' failed to load its settings, marking it unloadable: {e}");
            MarkIncompatible(world, payload, Incompatibility.GeneratorSettingsUnreadable);
        }

        return true;
    }

    private static void MarkIncompatible(World world, WorldPayload payload, Incompatibility reason)
    {
        world.SetGenerator(new IncompatibleGeneratorBinding(payload.Guid, reason, payload.WorldhallaSettings, payload.GeneratorSettings));
        world.m_dataError = World.SaveDataError.BadVersion;
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
