namespace Worldhalla.Plugin.Core;

public static class WorldgenPayload
{
    public static void Write(ZPackage package, WorldgenBinding worldgen)
    {
        package.Write(worldgen.Guid);
        package.Write(new ZPackage().GetArray());
        package.Write(worldgen.SaveSettings());
    }

    public static string ReadGuid(ZPackage package)
    {
        return package.ReadString();
    }

    public static byte[] ReadSettings(ZPackage package)
    {
        package.ReadByteArray();

        return SettingsBlob.Read(package);
    }
}
