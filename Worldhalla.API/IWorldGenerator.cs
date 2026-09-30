using Worldhalla.API.Settings;
using Worldhalla.API.World;

namespace Worldhalla.API;
    
public interface IWorldGenerator
{
    enum Feature
    {
        /**
         * The generator blends biome borders itself, see IWorldState.RenderPoint.
         *
         * If this feature is missing, biome blending will be done during chunk generation
         * by the game.
         */
        BiomeBlending,
    }
    
    string Guid { get; }

    string Name { get; }
    
    string Moniker { get; }
    
    string Version { get; }

    Feature[] Features { get; }
    
    IWorldState Generate(int seed, ISettings? settings);
}
