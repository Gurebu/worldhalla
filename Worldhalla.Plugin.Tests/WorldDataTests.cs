using NUnit.Framework;
using Worldhalla.API;
using Worldhalla.API.Settings;
using Worldhalla.Plugin.Compat;
using Worldhalla.Plugin.Core;
using Worldhalla.Plugin.Core.Extensions;
using Worldhalla.Plugin.Core.Patches;

namespace Worldhalla.Plugin.Tests;

public class WorldDataTests
{
    private const string WORLDGEN = "dev.test.worldgen";
    private const string SETTINGLESS_WORLDGEN = "dev.test.settingless";
    private const string LEGACY_WORLDGEN = "dev.gurebu.riverheim.stable";
    private const string MIGRATED_WORLDGEN = "dev.gurebu.riverheim";
    private const string VANILLA_DATA = "the world name vanilla reads next";

    /*
     * The bytes a Riverheim world from before the format version holds inline, which the migration
     * transcribes into a blob. Pinned rather than round-tripped: these exist on disk and cannot be regenerated.
     */
    private const string LEGACY_SETTINGS_BYTES =
        "0206707265736574146275696C74696E2E6578706572696D656E74616C0E7072657365742E76657273696F6E00000000";

    private static readonly TestWorldgen Generator = new();
    private static readonly TestWorldgen LegacyGenerator = new();
    private static readonly SettinglessTestWorldgen SettinglessGenerator = new();

    private static string Hex(byte[] data) => System.BitConverter.ToString(data).Replace("-", "");

    [SetUp]
    public void Setup()
    {
        Registry.RegisterGenerator(WORLDGEN, Generator);
        Registry.RegisterGenerator(MIGRATED_WORLDGEN, LegacyGenerator);
        Registry.RegisterGenerator(SETTINGLESS_WORLDGEN, SettinglessGenerator);
    }

    [TearDown]
    public void TearDown()
    {
        Registry.PurgeAll();
    }

    private static World NewWorld(bool custom = true, ISettings settings = null)
    {
        var world = new World { m_name = "test" };
        if (custom)
            world.SetGenerator(new CompatibleGeneratorBinding(Registry.Generators[WORLDGEN],
                WorldhallaSettings.Default, settings ?? Generator.DefaultSettings));

        return world;
    }

    private static CompatibleGeneratorBinding Compatible(World world) => (CompatibleGeneratorBinding)world.GetGenerator();

    /*
     * A .fwl as worlds carried it before the format version byte existed: an all ones header, the
     * worldgen id, then the settings inline with no length in front of them.
     */
    private static ZPackage LegacyPackage(string settingName = "preset", string worldgenId = LEGACY_WORLDGEN)
    {
        var package = new ZPackage();

        package.Write((byte)0xff);
        package.Write((byte)0xff);
        package.Write((byte)0xff);
        package.Write((byte)0xff);
        package.Write(worldgenId);
        package.Write((int)Version.World.DeepNorth);

        package.WriteNumItems(2);
        package.Write(settingName);
        package.Write("builtin.experimental");
        package.Write("preset.version");
        package.Write(0);

        package.Write(VANILLA_DATA);
        package.SetPos(0);

        return package;
    }

    private static ZPackage CurrentPackage(ISettings settings = null, string worldgenId = null, byte[] worldhallaBlob = null)
    {
        settings ??= new TestSettings("written");
        var world = NewWorld(settings: settings);

        var package = new ZPackage();

        WorldPatch.WriteHeader(package, world);
        package.Write((int)Version.World.DeepNorth);

        if (worldgenId == null && worldhallaBlob == null)
        {
            WorldPatch.WriteWorldData(package, world);
        }
        else
        {
            var payload = new ZPackage();

            payload.Write(worldgenId ?? WORLDGEN);
            payload.Write(worldhallaBlob ?? WorldhallaSettings.Default.Save());
            payload.Write(settings.SaveToBinaryBlob());

            package.Write(payload.GetArray());
        }

        package.Write(VANILLA_DATA);
        package.SetPos(0);

        return package;
    }

    [Test, RequiresValheim]
    public void HeaderRoundTrips()
    {
        var package = new ZPackage();

        WorldPatch.WriteHeader(package, NewWorld());
        package.SetPos(0);

        Assert.That(WorldPatch.ReadHeader("world_name", package, out var formatInfo), Is.True);
        Assert.That(formatInfo.version, Is.EqualTo(WorldPatch.FORMAT_VERSION));
    }

    /*
     * The whole reason the header sits where the world version does: an unmodded game has to read
     * it as a version it refuses, rather than as a world it tries to load.
     */
    [Test, RequiresValheim]
    public void HeaderIsRefusedByAnUnmoddedGame()
    {
        var package = new ZPackage();

        WorldPatch.WriteHeader(package, NewWorld());
        package.SetPos(0);

        var version = (Version.World)package.ReadInt();

        Assert.That((int)version, Is.Negative);
        Assert.That(Version.IsWorldVersionCompatible(version), Is.False);
    }

    [Test, RequiresValheim]
    public void VanillaWorldIsReadAsVanilla()
    {
        var package = new ZPackage();

        package.Write((int)Version.World.DeepNorth);
        package.Write(VANILLA_DATA);
        package.SetPos(0);

        var world = NewWorld(custom: false);

        Assert.That(WorldPatch.ReadHeader("world_name", package, out var formatInfo), Is.True);
        Assert.That(formatInfo.isModded, Is.False);
        Assert.That(package.ReadInt(), Is.EqualTo((int)Version.World.DeepNorth));

        Assert.That(WorldPatch.ReadWorldData("world_name", package, world, formatInfo), Is.True);
        Assert.That(world.GetGenerator(), Is.Null);
        Assert.That(package.ReadString(), Is.EqualTo(VANILLA_DATA));
    }

    [Test, RequiresValheim]
    public void VanillaWorldIsWrittenWithoutWorldhallaData()
    {
        var world = NewWorld(custom: false);
        var package = new ZPackage();

        WorldPatch.WriteHeader(package, world);
        package.Write((int)Version.World.DeepNorth);
        WorldPatch.WriteWorldData(package, world);
        package.Write(VANILLA_DATA);
        package.SetPos(0);

        Assert.That(package.ReadInt(), Is.EqualTo((int)Version.World.DeepNorth));
        Assert.That(package.ReadString(), Is.EqualTo(VANILLA_DATA));
    }

    [Test, RequiresValheim]
    public void WorldDataRoundTripsAndLeavesTheCursorOnVanillaData()
    {
        var package = CurrentPackage(new TestSettings("round tripped"));
        var world = NewWorld();

        Assert.That(WorldPatch.ReadHeader("world_name", package, out var formatInfo), Is.True);
        package.ReadInt();

        Assert.That(WorldPatch.ReadWorldData("world_name", package, world, formatInfo), Is.True);
        Assert.That(Compatible(world).GeneratorSettings, Is.EqualTo(new TestSettings("round tripped")));
        Assert.That(Compatible(world).LegacyFormat, Is.False);
        Assert.That(package.ReadString(), Is.EqualTo(VANILLA_DATA));
    }

    [Test, RequiresValheim]
    public void SettinglessWorldRoundTripsAndLeavesTheCursorOnVanillaData()
    {
        var written = new World { m_name = "test" };
        written.SetGenerator(new CompatibleGeneratorBinding(Registry.Generators[SETTINGLESS_WORLDGEN], WorldhallaSettings.Default, null));

        var package = new ZPackage();

        WorldPatch.WriteHeader(package, written);
        package.Write((int)Version.World.DeepNorth);
        WorldPatch.WriteWorldData(package, written);
        package.Write(VANILLA_DATA);
        package.SetPos(0);

        var world = new World { m_name = "test" };

        Assert.That(WorldPatch.ReadHeader("world_name", package, out var formatInfo), Is.True);
        package.ReadInt();

        Assert.That(WorldPatch.ReadWorldData("world_name", package, world, formatInfo), Is.True);
        Assert.That(Compatible(world).Generator.Instance, Is.SameAs(SettinglessGenerator));
        Assert.That(Compatible(world).GeneratorSettings, Is.Null);
        Assert.That(package.ReadString(), Is.EqualTo(VANILLA_DATA));
    }

    /*
     * The reason the payload is one blob: a tool that does not care about worldhalla reads past it
     * with a single length-prefixed read and carries on with the vanilla fields.
     */
    [Test, RequiresValheim]
    public void PayloadCanBeSteppedOverWithoutParsingIt()
    {
        var package = CurrentPackage(new TestSettings("stepped over"));

        WorldPatch.ReadHeader("world_name", package, out _);
        package.ReadInt();

        package.ReadByteArray();

        Assert.That(package.ReadString(), Is.EqualTo(VANILLA_DATA));
    }

    [Test, RequiresValheim]
    public void PayloadHoldsWorldhallaSettingsBetweenGuidAndGeneratorSettings()
    {
        var package = CurrentPackage(new TestSettings("laid out"));

        WorldPatch.ReadHeader("world_name", package, out _);
        package.ReadInt();

        var payload = new ZPackage(package.ReadByteArray());

        Assert.That(payload.ReadString(), Is.EqualTo(WORLDGEN));
        Assert.That(payload.ReadByteArray(), Is.Empty);
        Assert.That(payload.ReadByteArray(), Is.EqualTo(new TestSettings("laid out").SaveToBinaryBlob()));
    }

    /*
     * A setting this version doesn't know may change the world, so the world is unloadable rather than loaded
     * without it, and its settings are kept as read
     */
    [Test, RequiresValheim]
    public void UnknownWorldhallaSettingMakesTheWorldIncompatibleAndIsWrittenBackUnchanged()
    {
        var blob = new ZPackage();
        blob.Write(1);
        blob.Write("fromTheFuture");
        blob.Write(1f);

        var package = CurrentPackage(new TestSettings("kept"), worldhallaBlob: blob.GetArray());
        var world = new World { m_name = "test" };

        WorldPatch.ReadHeader("world_name", package, out var formatInfo);
        package.ReadInt();

        Assert.That(WorldPatch.ReadWorldData("world_name", package, world, formatInfo), Is.True);
        Assert.That(world.m_dataError, Is.EqualTo(World.SaveDataError.BadVersion));
        Assert.That(world.GetGenerator(), Is.InstanceOf<IncompatibleGeneratorBinding>());
        Assert.That(((IncompatibleGeneratorBinding)world.GetGenerator()!).Reason, Is.EqualTo(Incompatibility.WorldhallaSettingsUnreadable));
        Assert.That(package.ReadString(), Is.EqualTo(VANILLA_DATA));

        var written = new ZPackage();
        WorldPatch.WriteWorldData(written, world);
        written.SetPos(0);
        var payload = WorldPayload.Read(new ZPackage(written.ReadByteArray()));

        Assert.That(payload.WorldhallaSettings, Is.EqualTo(blob.GetArray()));
        Assert.That(payload.GeneratorSettings, Is.EqualTo(new TestSettings("kept").SaveToBinaryBlob()));
    }

    [Test, RequiresValheim]
    public void UnregisteredWorldgenIsReadAsUnloadableAndLeavesTheCursorOnVanillaData()
    {
        var package = CurrentPackage(worldgenId: "dev.someone.else");
        var world = new World { m_name = "test" };

        Assert.That(WorldPatch.ReadHeader("world_name", package, out var formatInfo), Is.True);
        package.ReadInt();

        Assert.That(WorldPatch.ReadWorldData("world_name", package, world, formatInfo), Is.True);
        Assert.That(world.m_dataError, Is.EqualTo(World.SaveDataError.BadVersion));
        Assert.That(world.GetGenerator(), Is.InstanceOf<IncompatibleGeneratorBinding>());
        Assert.That(world.GetGenerator()!.Guid, Is.EqualTo("dev.someone.else"));
        Assert.That(package.ReadString(), Is.EqualTo(VANILLA_DATA));
    }

    [Test, RequiresValheim]
    public void WorldhallaPayloadRoundTrips()
    {
        var settings = new TestSettings("sent");
        var package = new ZPackage();

        WorldPayload.Of(new CompatibleGeneratorBinding(Registry.Generators[WORLDGEN], WorldhallaSettings.Default, settings)).Write(package);
        package.Write(VANILLA_DATA);
        package.SetPos(0);

        var payload = WorldPayload.Read(package);

        Assert.That(payload.Guid, Is.EqualTo(WORLDGEN));
        Assert.That(payload.WorldhallaSettings, Is.Empty);
        Assert.That(payload.GeneratorSettings, Is.EqualTo(settings.SaveToBinaryBlob()));
        Assert.That(package.ReadString(), Is.EqualTo(VANILLA_DATA));
    }

    [Test, RequiresValheim]
    public void UnregisteredWorldgenIsWrittenBackUnchanged()
    {
        var settings = new TestSettings("kept").SaveToBinaryBlob();
        var package = CurrentPackage(new TestSettings("kept"), worldgenId: "dev.someone.else");
        var world = new World { m_name = "test" };

        WorldPatch.ReadHeader("world_name", package, out var formatInfo);
        package.ReadInt();
        WorldPatch.ReadWorldData("world_name", package, world, formatInfo);

        var written = new ZPackage();
        WorldPatch.WriteHeader(written, world);
        WorldPatch.WriteWorldData(written, world);
        written.SetPos(0);

        Assert.That(world.GetGenerator(), Is.InstanceOf<IncompatibleGeneratorBinding>());
        Assert.That(WorldPatch.ReadHeader("world_name", written, out var writtenInfo), Is.True);
        Assert.That(writtenInfo.isModded, Is.True);

        var payload = new ZPackage(written.ReadByteArray());

        Assert.That(payload.ReadString(), Is.EqualTo("dev.someone.else"));
        Assert.That(payload.ReadByteArray(), Is.Empty);
        Assert.That(payload.ReadByteArray(), Is.EqualTo(settings));
    }

    [Test, RequiresValheim]
    public void SettingsAtTheSizeLimitAreRead()
    {
        var limit = new TestSettings(new string('x', WorldPayload.MAX_SETTINGS_SIZE));
        var package = CurrentPackage(limit);
        var world = NewWorld();

        WorldPatch.ReadHeader("world_name", package, out var formatInfo);
        package.ReadInt();

        Assert.That(WorldPatch.ReadWorldData("world_name", package, world, formatInfo), Is.True);
        Assert.That(Compatible(world).GeneratorSettings, Is.EqualTo(limit));
    }

    [Test, RequiresValheim]
    public void SettingsOverTheSizeLimitAreSkipped()
    {
        var package = CurrentPackage(new TestSettings(new string('x', WorldPayload.MAX_SETTINGS_SIZE + 1)));
        var world = NewWorld();

        WorldPatch.ReadHeader("world_name", package, out var formatInfo);
        package.ReadInt();

        Assert.That(WorldPatch.ReadWorldData("world_name", package, world, formatInfo), Is.False);
    }

    [Test, RequiresValheim]
    public void MalformedSettingsMakeTheWorldIncompatible()
    {
        var package = CurrentPackage(new TestSettings(TestWorldgen.MALFORMED));
        var world = new World { m_name = "test" };

        WorldPatch.ReadHeader("world_name", package, out var formatInfo);
        package.ReadInt();

        Assert.That(WorldPatch.ReadWorldData("world_name", package, world, formatInfo), Is.True);
        Assert.That(world.m_dataError, Is.EqualTo(World.SaveDataError.BadVersion));
        Assert.That(((IncompatibleGeneratorBinding)world.GetGenerator()!).Reason, Is.EqualTo(Incompatibility.GeneratorSettingsUnreadable));
        Assert.That(package.ReadString(), Is.EqualTo(VANILLA_DATA));
    }

    [Test, RequiresValheim]
    public void LegacyWorldIsReadAndLeavesTheCursorOnVanillaData()
    {
        var package = LegacyPackage();
        var world = NewWorld();

        Assert.That(WorldPatch.ReadHeader("world_name", package, out var formatInfo), Is.True);
        Assert.That(formatInfo.version, Is.EqualTo(RiverheimMigration.FORMAT_VERSION));
        package.ReadInt();

        Assert.That(WorldPatch.ReadWorldData("world_name", package, world, formatInfo), Is.True);
        Assert.That(package.ReadString(), Is.EqualTo(VANILLA_DATA));
    }

    /*
     * The transcription has to land on exactly the bytes the old format held, or the blob it hands
     * to a generator parses as something else.
     */
    [Test, RequiresValheim]
    public void LegacyWorldIsMarkedUntilSaved()
    {
        var package = LegacyPackage();
        var world = new World { m_name = "test" };

        WorldPatch.ReadHeader("world_name", package, out var formatInfo);
        package.ReadInt();
        WorldPatch.ReadWorldData("world_name", package, world, formatInfo);

        Assert.That(Compatible(world).LegacyFormat, Is.True);

        WorldPatch.WriteWorldData(new ZPackage(), world);

        Assert.That(Compatible(world).LegacyFormat, Is.False);
    }

    [Test, RequiresValheim]
    public void LegacySettingsTranscribeToTheSameBytes()
    {
        var package = LegacyPackage();

        WorldPatch.ReadHeader("world_name", package, out _);
        package.ReadInt();

        var payload = RiverheimMigration.ReleaseMigratoryRead(package);

        Assert.That(Hex(payload.GeneratorSettings), Is.EqualTo(LEGACY_SETTINGS_BYTES));
    }

    [Test, RequiresValheim]
    public void ForeignLegacyWorldgenIsSkipped()
    {
        var package = LegacyPackage(worldgenId: "dev.someone.else");

        Assert.That(WorldPatch.ReadHeader("world_name", package, out _), Is.False);
    }

    [Test, RequiresValheim]
    public void LegacyWorldWithUnexpectedSettingIsSkipped()
    {
        var package = LegacyPackage(settingName: "preset.renamed");
        var world = NewWorld();

        WorldPatch.ReadHeader("world_name", package, out var formatInfo);
        package.ReadInt();

        Assert.That(WorldPatch.ReadWorldData("world_name", package, world, formatInfo), Is.False);
    }
}
