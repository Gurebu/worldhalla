namespace Worldhalla.API.Settings.Controls;

/**
 * A single line of free text, shown as an input field. The form parses the text itself,
 * and rejects what it can't parse from ISettingsForm.Build.
 */
public sealed class TextField : Control
{
    private string text;

    public TextField(string label, string text = "", string? tooltip = null)
        : base(label, tooltip)
    {
        this.text = text;
    }

    public string Text
    {
        get => text;
        set
        {
            if (value == text)
                return;

            text = value;
            OnChanged();
        }
    }
}
