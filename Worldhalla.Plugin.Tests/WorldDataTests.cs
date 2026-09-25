using NUnit.Framework;
using Worldhalla.API;
using Worldhalla.Plugin.Compat;
using Worldhalla.Plugin.Extensions;
using Worldhalla.Plugin.Internal;
using Worldhalla.Plugin.Patches;

namespace Worldhalla.Plugin.Tests;

public class WorldDataTests
{
    private const string WORLDGEN = "dev.test.worldgen";
    private const string LEGACY_WORLDGEN = "dev.gurebu.riverheim.stable";
    private const string VANILLA_DATA = "the world name vanilla reads next";

    /*
     * The bytes a pre-protocol Riverheim world holds inline, which the migration transcribes into a
     * blob. Pinned rather than round-tripped: these exist on disk and cannot be regenerated.
     */
    private const string LEGACY_SETTINGS_BYTES =
        "0206707265736574146275696C74696E2E6578706572696D656E74616C0E7072657365742E76657273696F6E00000000";

    private static readonly TestWorldgen Generator = new(WORLDGEN);
    private static readonly TestWorldgen LegacyGenerator = new(LEGACY_WORLDGEN);

    private static string Hex(byte[] data) => System.BitConverter.ToString(data).Replace("-", "");

    [SetUp]
    public void Setup()
    {
        Registry.RegisterGenerator(WORLDGEN, Generator);
        Registry.RegisterGenerator(LEGACY_WORLDGEN, LegacyGenerator);
    }

    [TearDown]
    public void TearDown()
    {
        Registry.PurgeAll();
    }

    private static World NewWorld(bool custom = true)
    {
        var world = new World { m_name = "test" };
        if (custom)
            world.SetWorldgen(Generator);

        return world;
    }

    /*
     * A .fwl as worlds carried it before the protocol version byte existed: an all ones header, the
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

    private static ZPackage CurrentPackage(ISettings settings = null, string worldgenId = null)
    {
        var world = NewWorld();
        world.SetSettings(settings ?? new TestSettings("written"));

        var package = new ZPackage();

        WorldPatch.WriteHeader(package, world);
        package.Write((int)Version.World.DeepNorth);

        if (worldgenId == null)
        {
            WorldPatch.WriteWorldData(package, world);
        }
        else
        {
            var payload = new ZPackage();

            payload.Write(worldgenId);
            payload.Write(world.GetSettings().Serialize());

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

        Assert.That(WorldPatch.ReadHeader("world_name", package, out var protocolInfo), Is.True);
        Assert.That(protocolInfo.version, Is.EqualTo(WorldPatch.PROTOCOL_VERSION));
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
    public void UnmoddedWorldIsSkipped()
    {
        var package = new ZPackage();

        package.Write((int)Version.World.DeepNorth);
        package.SetPos(0);

        Assert.That(WorldPatch.ReadHeader("world_name", package, out _), Is.False);
    }

    [Test, RequiresValheim]
    public void WorldDataRoundTripsAndLeavesTheCursorOnVanillaData()
    {
        var package = CurrentPackage(new TestSettings("round tripped"));
        var world = NewWorld();

        Assert.That(WorldPatch.ReadHeader("world_name", package, out var protocolInfo), Is.True);
        package.ReadInt();

        Assert.That(WorldPatch.ReadWorldData("world_name", package, world, protocolInfo), Is.True);
        Assert.That(world.GetSettings(), Is.EqualTo(new TestSettings("round tripped")));
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
    public void UnregisteredWorldgenIsSkipped()
    {
        var package = CurrentPackage(worldgenId: "dev.someone.else");
        var world = NewWorld();

        Assert.That(WorldPatch.ReadHeader("world_name", package, out var protocolInfo), Is.True);
        package.ReadInt();

        Assert.That(WorldPatch.ReadWorldData("world_name", package, world, protocolInfo), Is.False);
    }

    [Test, RequiresValheim]
    public void MalformedSettingsAreSkipped()
    {
        var package = CurrentPackage(new TestSettings(TestWorldgen.MALFORMED));
        var world = NewWorld();

        WorldPatch.ReadHeader("world_name", package, out var protocolInfo);
        package.ReadInt();

        Assert.That(WorldPatch.ReadWorldData("world_name", package, world, protocolInfo), Is.False);
    }

    [Test, RequiresValheim]
    public void LegacyWorldIsReadAndLeavesTheCursorOnVanillaData()
    {
        var package = LegacyPackage();
        var world = NewWorld();

        Assert.That(WorldPatch.ReadHeader("world_name", package, out var protocolInfo), Is.True);
        Assert.That(protocolInfo.version, Is.EqualTo(RiverheimMigration.PROTOCOL_VERSION));
        package.ReadInt();

        Assert.That(WorldPatch.ReadWorldData("world_name", package, world, protocolInfo), Is.True);
        Assert.That(package.ReadString(), Is.EqualTo(VANILLA_DATA));
    }

    /*
     * The transcription has to land on exactly the bytes the old format held, or the blob it hands
     * to a generator parses as something else.
     */
    [Test, RequiresValheim]
    public void LegacySettingsTranscribeToTheSameBytes()
    {
        var package = LegacyPackage();

        WorldPatch.ReadHeader("world_name", package, out _);
        package.ReadInt();

        var (_, settings) = RiverheimMigration.MigratoryRead(package);

        Assert.That(Hex(settings), Is.EqualTo(LEGACY_SETTINGS_BYTES));
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

        WorldPatch.ReadHeader("world_name", package, out var protocolInfo);
        package.ReadInt();

        Assert.That(WorldPatch.ReadWorldData("world_name", package, world, protocolInfo), Is.False);
    }
}
