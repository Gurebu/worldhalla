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
     *
     * The blob may take at most 64 KiB. Settings that exceed it are
     * rejected as invalid, and worlds carrying them do not load.
     */
    byte[] SaveToBinaryBlob();

    /**
     * A text representation of the settings to be written
     * in the game's UI tooltip.
     */
    string Summary();
}
