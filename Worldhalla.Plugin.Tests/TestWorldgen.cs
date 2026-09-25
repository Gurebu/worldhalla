using Worldhalla.API;

namespace Worldhalla.Plugin.Tests;

/*
 * The patches only ever reach settings through a registered generator, so the tests bring their own
 * rather than depending on any particular one.
 */
internal sealed record TestSettings(string Payload) : ISettings
{
    public byte[] Serialize() => System.Text.Encoding.UTF8.GetBytes(Payload);
}

internal sealed class TestWorldgen(string guid) : IWorldGenerator
{
    public const string MALFORMED = "malformed";

    public string Guid { get; } = guid;

    public string Moniker => "TEST";

    public string Name => "Test";

    public IWorldGenerator.Feature[] Features => System.Array.Empty<IWorldGenerator.Feature>();

    public ISettings DefaultSettings => new TestSettings("default");

    public ISettings DeserializeSettings(byte[] data, out string errorMessage)
    {
        var payload = System.Text.Encoding.UTF8.GetString(data);
        if (payload == MALFORMED)
            throw new System.Exception("settings are malformed");

        errorMessage = null;

        return new TestSettings(payload);
    }

    public IWorldState Generate(int seed, ISettings settings) => throw new System.NotImplementedException();
}
