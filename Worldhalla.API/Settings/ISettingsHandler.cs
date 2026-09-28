namespace Worldhalla.API.Settings;

/**
 * A companion interface for a class that is annotated as a WorldhallaPlugin
 *
 * Unlike the IWorldGenerator interface, this interface is optional, if your
 * generator doesn't take any settings, you don't have to implement this at all.
 * In case you don't implement ISettingsHandler, IWorldGenerator.Generate will
 * be called with a null value for ISettings.
 */
public interface ISettingsHandler
{
    /**
     * Default setting object value for a new world.
     * These settings will load into the world creation dialog in single
     * player or will be used to start a new server world if no
     * command line has been provided.
     */
    ISettings DefaultSettings { get; }

    /**
     * Reconstruct settings object value from the save file or
     * a network packet. If the data is malformed, should throw
     * a SettingsException with a clear reason.
     */
    ISettings BuildSettingsFromBinaryBlob(byte[] data);

    /**
     * Reconstruct settings object value from the command line.
     * Command line parameter is invoked like '-wh settings "$SETTINGS"'.
     * This method will receive the value of $SETTINGS.
     * If $SETTINGS can't be parsed or encode an invalid configuration,
     * should throw a SettingsException with a clear reason.
     */
    ISettings BuildSettingsFromCommandLine(string settings);
    
    /**
     * Provide an instruction on command line usage. Worldhalla will display it
     * in case the command line settings value provided by the user
     * failed to parse.
     */
    string CommandLineUsage { get; }
    
    // TODO settings dialog
}
