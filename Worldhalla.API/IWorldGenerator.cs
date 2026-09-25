namespace Worldhalla.API;
    
// TODO public interface to produce IWorldState, get me a proper name
public interface IWorldGenerator
{
    enum Feature
    {
        BiomeBlending,
    }
    
    string Guid { get; }
    
    string Moniker { get; }
    
    Feature[] Features { get; }

    ISettings DefaultSettings { get; }

    ISettings DeserializeSettings(byte[] data, out string errorMessage);
    
    IWorldState Generate(int seed, ISettings settings);
        
    // TODO setting menu manipulation
}
