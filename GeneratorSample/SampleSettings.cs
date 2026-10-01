using System.Globalization;
using Worldhalla.API.Settings;

namespace GeneratorSample;

public class SampleSettings(float islandScale) : ISettings
{
    public const float DEFAULT_ISLAND_SCALE = 1f;
    public const float MIN_ISLAND_SCALE = 0.25f;
    public const float MAX_ISLAND_SCALE = 4f;

    public float IslandScale { get; } = islandScale;

    public static SampleSettings Checked(float islandScale)
    {
        if (float.IsNaN(islandScale) || islandScale < MIN_ISLAND_SCALE || islandScale > MAX_ISLAND_SCALE)
            throw new SettingsException($"Island scale must be between {MIN_ISLAND_SCALE} and {MAX_ISLAND_SCALE}");

        return new SampleSettings(islandScale);
    }

    public static SampleSettings Load(byte[] data)
    {
        if (data.Length != sizeof(float))
            throw new SettingsException("Unrecognized Generator Sample settings");

        return Checked(BitConverter.ToSingle(data, 0));
    }

    public byte[] SaveToBinaryBlob()
    {
        return BitConverter.GetBytes(IslandScale);
    }

    public string Summary()
    {
        return $"Island scale: {IslandScale.ToString("0.##", CultureInfo.InvariantCulture)}";
    }
}
