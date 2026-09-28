namespace Worldhalla.API.Settings.Controls;

/**
 * A control in a settings form. The set of controls is fixed by Worldhalla, since it
 * has to know how to draw each of them, so controls can't be subclassed outside
 * this assembly.
 */
public abstract class Control
{
    private protected Control(string label, string? tooltip)
    {
        Label = label;
        Tooltip = tooltip;
    }

    public string Label { get; }

    public string? Tooltip { get; }

    /**
     * Raised whenever the control's value changes, whether the user changed it or
     * the form did.
     */
    public event Action? Changed;

    private protected void OnChanged()
    {
        Changed?.Invoke();
    }
}
