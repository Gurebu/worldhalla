using Worldhalla.API;
using Worldhalla.API.Settings;

namespace Worldhalla.Plugin.Core;

public abstract class WorldgenBinding(string guid)
{
    public string Guid { get; } = guid;

    public abstract byte[] SaveSettings();
}

public sealed class InstalledWorldgen(string guid, IWorldGenerator generator, ISettings? settings) : WorldgenBinding(guid)
{
    private GeneratedWorld? pending;

    public IWorldGenerator Generator { get; } = generator;

    public ISettings? Settings { get; } = settings;

    public bool LegacyFormat { get; set; }

    public override byte[] SaveSettings() => Settings?.SaveToBinaryBlob() ?? [];

    // A client generates while handling the server's PeerInfo, before the game creates the WorldGenerator to attach it to
    public void HandOver(GeneratedWorld generated)
    {
        pending = generated;
    }

    public GeneratedWorld? TakeHandedOver()
    {
        var generated = pending;
        pending = null;

        return generated;
    }
}

// The settings stay as read, so saving the world without its generator installed keeps them
public sealed class MissingWorldgen(string guid, byte[] settings) : WorldgenBinding(guid)
{
    public override byte[] SaveSettings() => settings;
}
