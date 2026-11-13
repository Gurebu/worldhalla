namespace Worldhalla.Plugin.Core;

public static class NewWorldDefaults
{
    public static string? WorldgenGuid { get; private set; }

    public static string? Settings { get; private set; }

    public static void Set(string worldgenGuid, string settings)
    {
        WorldgenGuid = string.IsNullOrWhiteSpace(worldgenGuid) ? null : worldgenGuid.Trim();
        Settings = string.IsNullOrEmpty(settings) ? null : settings;
    }

    public static CommandLineOptions ToOptions()
    {
        if (Settings != null && WorldgenGuid == null)
            throw new ServerStartupException("World creation settings are configured without a generator to apply to, set 'Generator' in the [World creation] config section");

        return new CommandLineOptions { worldgenGuid = WorldgenGuid, settings = Settings, origin = "the [World creation] config" };
    }
}
