using Worldhalla.API.Settings;

namespace Worldhalla.Plugin.Core;

public sealed class WorldhallaSettings
{
    public static readonly WorldhallaSettings Default = new();

    private WorldhallaSettings()
    {
    }

    public static WorldhallaSettings Load(byte[] blob)
    {
        if (blob.Length == 0)
            return Default;

        var package = new ZPackage(blob);

        var count = package.ReadInt();
        for (var i = 0; i < count; i++)
        {
            var key = package.ReadString();
            switch (key)
            {
                default:
                    throw new SettingsException($"Unknown Worldhalla setting '{key}', the world was saved by a newer Worldhalla");
            }
        }

        return Default;
    }

    public byte[] Save()
    {
        return [];
    }
}
