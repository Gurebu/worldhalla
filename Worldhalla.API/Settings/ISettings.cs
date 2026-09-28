namespace Worldhalla.API.Settings;

/**
 * Implement this interface if your generator also implements ISettingsHandler.
 *
 * This interface is designed to wrap your settings object and facilitate
 * its storage, transport over the network and UI display.
 */
public interface ISettings
{
    /**
     * Produce a binary representation that is loadable
     * by ISettingsHandler.BuildSettingsFromBinaryBlob().
     */
    byte[] SaveToBinaryBlob();

    /**
     * A text representation of the settings to be written
     * in the game's UI tooltip.
     */
    string Summary();
}
