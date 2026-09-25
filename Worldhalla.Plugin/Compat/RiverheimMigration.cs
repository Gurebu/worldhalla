namespace Worldhalla.Plugin.Compat;

public static class RiverheimMigration
{
    public const byte PROTOCOL_VERSION = 0xff;
    public const string GUID = "dev.gurebu.riverheim.stable";
    
    private const int SETTING_COUNT = 2;
    private const string SETTING_PRESET = "preset";
    private const string SETTING_PRESET_VERSION = "preset.version";
    
    public static (string, byte[]) MigratoryRead(ZPackage package)
    {
        var count = package.ReadNumItems();
        if (count != SETTING_COUNT)
            throw new Exception($"legacy world holds {count} settings, expected {SETTING_COUNT}");

        ReadLegacySettingName(package, SETTING_PRESET);
        var preset = package.ReadString();

        ReadLegacySettingName(package, SETTING_PRESET_VERSION);
        var presetVersion = package.ReadInt();

        var settings = new ZPackage();

        settings.WriteNumItems(SETTING_COUNT);
        settings.Write(SETTING_PRESET);
        settings.Write(preset);
        settings.Write(SETTING_PRESET_VERSION);
        settings.Write(presetVersion);

        return (GUID, settings.GetArray());
    }

    private static void ReadLegacySettingName(ZPackage package, string expected)
    {
        var name = package.ReadString();
        if (name != expected)
            throw new Exception($"legacy world holds setting '{name}' where '{expected}' was expected");
    }
}
