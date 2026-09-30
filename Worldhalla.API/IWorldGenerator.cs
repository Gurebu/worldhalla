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
    
    /**
     * Progress is reported from 0 to 1; a generator that never reports it is shown as busy without a percentage.
     * Cancellation is requested when the result is no longer wanted, such as a world preview whose seed has changed.
     * Throwing OperationCanceledException then skips the remaining work; ignoring the token is allowed but wasteful.
     */
    IWorldState Generate(
        int seed,
        ISettings? settings,
        IProgress<float> progress,
        CancellationToken cancellation);
}
