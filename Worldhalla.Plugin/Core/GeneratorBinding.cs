using Worldhalla.API.Settings;

namespace Worldhalla.Plugin.Core;

public abstract class GeneratorBinding(string guid)
{
    public string Guid { get; } = guid;

    public abstract byte[] SaveWorldhallaSettings();

    public abstract byte[] SaveGeneratorSettings();
}

public sealed class CompatibleGeneratorBinding(
    RegisteredGenerator generator,
    WorldhallaSettings worldhallaSettings,
    ISettings? generatorSettings)
    : GeneratorBinding(generator.Guid)
{
    public bool LegacyFormat { get; set; }

    public RegisteredGenerator Generator { get; } = generator;

    public WorldhallaSettings WorldhallaSettings { get; } = worldhallaSettings;

    public ISettings? GeneratorSettings { get; } = generatorSettings;

    public override byte[] SaveWorldhallaSettings() => WorldhallaSettings.Save();

    public override byte[] SaveGeneratorSettings() => GeneratorSettings?.SaveToBinaryBlob() ?? [];
}

public enum Incompatibility
{
    GeneratorMissing,
    WorldhallaSettingsUnreadable,
    GeneratorSettingsUnreadable,
}

public sealed class IncompatibleGeneratorBinding(
    string guid,
    Incompatibility reason,
    byte[] worldhallaSettings,
    byte[] generatorSettings)
    : GeneratorBinding(guid)
{
    public Incompatibility Reason { get; } = reason;

    public override byte[] SaveWorldhallaSettings() => worldhallaSettings;

    public override byte[] SaveGeneratorSettings() => generatorSettings;
}
