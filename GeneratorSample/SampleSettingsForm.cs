using System.Globalization;
using Worldhalla.API.Settings;
using Worldhalla.API.Settings.Controls;

namespace GeneratorSample;

public class SampleSettingsForm : ISettingsForm
{
    private readonly TextField islandScale;

    public SampleSettingsForm(SampleSettings current)
    {
        islandScale = new TextField("Island scale", current.IslandScale.ToString(CultureInfo.InvariantCulture));
        Controls = [islandScale];
    }

    public IReadOnlyList<Control> Controls { get; }

    public ISettings Build()
    {
        if (!float.TryParse(islandScale.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            throw new SettingsException("Island scale must be a number");

        return SampleSettings.Checked(value);
    }
}
