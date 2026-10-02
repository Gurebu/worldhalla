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
    public IWorldGenerator Generator { get; } = generator;

    public ISettings? Settings { get; } = settings;

    public bool LegacyFormat { get; set; }

    public override byte[] SaveSettings() => Settings?.SaveToBinaryBlob() ?? [];
}

public sealed class MissingWorldgen(string guid, byte[] settings) : WorldgenBinding(guid)
{
    public override byte[] SaveSettings() => settings;
}
