using Worldhalla.API.Settings;

namespace Worldhalla.Plugin.Core;

public sealed class WorldPayload(string guid, byte[] worldhallaSettings, byte[] generatorSettings)
{
    public const int MAX_SETTINGS_SIZE = 64 * 1024;

    public string Guid { get; } = guid;

    public byte[] WorldhallaSettings { get; } = worldhallaSettings;

    public byte[] GeneratorSettings { get; } = generatorSettings;

    public static WorldPayload Of(GeneratorBinding generator)
    {
        return new WorldPayload(generator.Guid, generator.SaveWorldhallaSettings(), generator.SaveGeneratorSettings());
    }

    public static void CheckSettings(ISettings settings)
    {
        CheckSettingsSize(settings.SaveToBinaryBlob().Length);
    }

    public static WorldPayload Read(ZPackage package)
    {
        var guid = package.ReadString();
        var worldhallaSettings = package.ReadByteArray();

        var settingsSize = package.ReadInt();
        CheckSettingsSize(settingsSize);

        return new WorldPayload(guid, worldhallaSettings, package.ReadByteArray(settingsSize));
    }

    public void Write(ZPackage package)
    {
        package.Write(Guid);
        package.Write(WorldhallaSettings);
        package.Write(GeneratorSettings);
    }

    private static void CheckSettingsSize(int size)
    {
        if (size < 0 || size > MAX_SETTINGS_SIZE)
            throw new SettingsException($"Settings take {size} bytes, the limit is {MAX_SETTINGS_SIZE}");
    }
}
