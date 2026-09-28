using Worldhalla.API.Settings.Controls;

namespace Worldhalla.API.Settings;

/**
 * A generator's settings, laid out as controls. Worldhalla shows the controls in the
 * order given and writes the user's input into them. The form may also change its
 * controls itself, for example in response to another control's Changed event, and
 * Worldhalla will show the new state.
 *
 * All calls happen on the main thread.
 */
public interface ISettingsForm
{
    IReadOnlyList<Control> Controls { get; }

    /**
     * Build settings from the current state of the controls. Worldhalla calls this
     * after every change, to tell whether the settings can be accepted, and once more
     * when the user confirms. It must not have side effects.
     *
     * If the controls describe settings that can't be accepted, throw a
     * SettingsException with a reason the user can act on.
     */
    ISettings Build();
}
