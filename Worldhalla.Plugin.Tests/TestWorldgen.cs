using Worldhalla.API;
using Worldhalla.API.Settings;
using Worldhalla.API.World;

namespace Worldhalla.Plugin.Tests;

/*
 * The patches only ever reach settings through a registered generator, so the tests bring their own
 * rather than depending on any particular one.
 */
internal sealed record TestSettings(string Payload) : ISettings
{
    public byte[] SaveToBinaryBlob() => System.Text.Encoding.UTF8.GetBytes(Payload);

    public string Summary() => Payload;
}

internal sealed class SettinglessTestWorldgen(string guid) : IWorldGenerator
{
    public string Guid { get; } = guid;

    public string Moniker => "BARE";

    public string Name => "Settingless test";

    public IWorldGenerator.Feature[] Features => System.Array.Empty<IWorldGenerator.Feature>();

    public IWorldState Generate(int seed, ISettings settings) => throw new System.NotImplementedException();
}

internal sealed class TestWorldgen(string guid) : IWorldGenerator, ISettingsHandler
{
    public const string MALFORMED = "malformed";

    public string Guid { get; } = guid;

    public string Moniker => "TEST";

    public string Name => "Test";

    public IWorldGenerator.Feature[] Features => System.Array.Empty<IWorldGenerator.Feature>();

    public ISettings DefaultSettings => new TestSettings("default");

    public ISettings BuildSettingsFromBinaryBlob(byte[] data)
    {
        var payload = System.Text.Encoding.UTF8.GetString(data);
        if (payload == MALFORMED)
            throw new SettingsException("settings are malformed");

        return new TestSettings(payload);
    }

    public ISettings BuildSettingsFromCommandLine(string settings) => new TestSettings(settings);

    public string CommandLineUsage => "any string";

    public IWorldState Generate(int seed, ISettings settings) => throw new System.NotImplementedException();
}
