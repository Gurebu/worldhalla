using System.Globalization;
using Worldhalla.API;
using Worldhalla.API.Attributes;
using Worldhalla.API.Settings;
using Worldhalla.API.World;

namespace GeneratorSample;

[WorldhallaPlugin(GeneratorSamplePlugin.GUID)]
public class SampleGenerator : IWorldGenerator, ISettingsHandler
{
    public string Name => GeneratorSamplePlugin.NAME;

    public string Moniker => "GSP";

    public string Version => GeneratorSamplePlugin.VERSION;

    public IWorldGenerator.Feature[] Features => [];

    public ISettings DefaultSettings => new SampleSettings(SampleSettings.DEFAULT_ISLAND_SCALE);

    public string CommandLineUsage => $"scale <island scale, {SampleSettings.MIN_ISLAND_SCALE} to {SampleSettings.MAX_ISLAND_SCALE}>";

    public ISettings BuildSettingsFromBinaryBlob(byte[] data)
    {
        return SampleSettings.Load(data);
    }

    public ISettings BuildSettingsFromCommandLine(string settings)
    {
        var words = settings.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length != 2 || words[0] != "scale" ||
            !float.TryParse(words[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var islandScale))
            throw new SettingsException($"Expected 'scale <island scale>', got '{settings}'");

        return SampleSettings.Checked(islandScale);
    }

    public ISettingsForm CreateForm(ISettings current)
    {
        return new SampleSettingsForm((SampleSettings)current);
    }

    public IWorldState Generate(int seed, ISettings? settings, IProgress<float> progress, CancellationToken cancellation)
    {
        var islandScale = (settings as SampleSettings)?.IslandScale ?? SampleSettings.DEFAULT_ISLAND_SCALE;
        var world = new SampleWorld(seed, islandScale);
        progress.Report(1f);
        return world;
    }
}
