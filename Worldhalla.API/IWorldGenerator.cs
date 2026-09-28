using Worldhalla.API.Settings;
using Worldhalla.API.World;

namespace Worldhalla.API;
    
public interface IWorldGenerator
{
    enum Feature
    {
        BiomeBlending,
    }
    
    string Guid { get; }

    string Name { get; }
    
    string Moniker { get; }

    Feature[] Features { get; }
    
    IWorldState Generate(int seed, ISettings? settings);
}
