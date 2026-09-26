namespace Worldhalla.API;
    
public interface IWorldGenerator
{
    enum Feature
    {
        BiomeBlending,
    }
    
    string Guid { get; }
    
    string Moniker { get; }

    string Name { get; }
    
    Feature[] Features { get; }

    ISettings DefaultSettings { get; }

    ISettings DeserializeSettings(byte[] data, out string errorMessage);

    ISettings? ParseCommandLineSettings(string settings, out string errorMessage);
    
    IWorldState Generate(int seed, ISettings settings);
        
    // TODO setting menu manipulation
}
