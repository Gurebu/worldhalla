using Worldhalla.API.Settings;

namespace Worldhalla.Plugin.Core;

public static class SettingsBlob
{
    public const int MAX_SIZE = 64 * 1024;

    public static void Check(ISettings settings)
    {
        Check(settings.SaveToBinaryBlob().Length);
    }

    public static byte[] Read(ZPackage package)
    {
        var length = package.ReadInt();
        Check(length);

        return package.ReadByteArray(length);
    }

    private static void Check(int length)
    {
        if (length < 0 || length > MAX_SIZE)
            throw new SettingsException($"Settings take {length} bytes, the limit is {MAX_SIZE}");
    }
}
