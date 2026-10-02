namespace Worldhalla.Plugin.Compat;

public static class RiverheimMigration
{
    public const byte FORMAT_VERSION = 0xff;
    public const string GUID_RELEASE_STABLE = "dev.gurebu.riverheim.stable";
    public const string GUID_BETA = "dev.gurebu.riverheim";
    private const string GUID_RELEASE_STABLE_TO = "dev.gurebu.riverheim";
    
    private const int SETTING_COUNT = 2;
    private const string SETTING_PRESET = "preset";
    private const string SETTING_PRESET_VERSION = "preset.version";
    
    public static (string, byte[]) ReleaseMigratoryRead(ZPackage package)
    {
        var count = package.ReadNumItems();
        if (count != SETTING_COUNT)
            throw new Exception($"legacy world holds {count} settings, expected {SETTING_COUNT}");

        var namePreset = package.ReadString();
        if (namePreset != SETTING_PRESET)
            throw new Exception($"legacy world holds setting '{namePreset}' where '{SETTING_PRESET}' was expected");
        var preset = package.ReadString();

        var namePresetVersion = package.ReadString();
        if (namePresetVersion != SETTING_PRESET_VERSION)
            throw new Exception($"legacy world holds setting '{namePresetVersion}' where '{SETTING_PRESET_VERSION}' was expected");
        var presetVersion = package.ReadInt();

        var settings = new ZPackage();

        settings.WriteNumItems(SETTING_COUNT);
        settings.Write(SETTING_PRESET);
        settings.Write(preset);
        settings.Write(SETTING_PRESET_VERSION);
        settings.Write(presetVersion);

        return (GUID_RELEASE_STABLE_TO, settings.GetArray());
    }
}
