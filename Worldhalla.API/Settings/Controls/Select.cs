namespace Worldhalla.API.Settings.Controls;

/**
 * A choice of one option out of a list, shown as a dropdown. The options are the
 * texts shown to the user; the form maps the selected index back to its own values.
 */
public sealed class Select : Control
{
    private int selected;

    public Select(string label, IReadOnlyList<string> options, int selected = 0, string? tooltip = null)
        : base(label, tooltip)
    {
        if (options.Count == 0)
            throw new ArgumentException("A select needs at least one option", nameof(options));

        Options = options;
        this.selected = CheckIndex(selected);
    }

    public IReadOnlyList<string> Options { get; }

    public int Selected
    {
        get => selected;
        set
        {
            if (CheckIndex(value) == selected)
                return;

            selected = value;
            OnChanged();
        }
    }

    private int CheckIndex(int index)
    {
        if (index < 0 || index >= Options.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Select has {Options.Count} options");

        return index;
    }
}
