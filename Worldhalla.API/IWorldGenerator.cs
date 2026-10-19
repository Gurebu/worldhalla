using JetBrains.Annotations;
using Worldhalla.API.Settings;
using Worldhalla.API.World;

namespace Worldhalla.API;

/**
 * Primary interface for your generator.
 *
 * Annotate it with [WorldhallaWorldGenerator(GUID)] to make it visible to Worldhalla
 */
[PublicAPI]
public interface IWorldGenerator
{
    [PublicAPI]
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
    
    /**
     * Human-readable name for your generator. Best localized.
     */
    string Name { get; }
    
    /**
     * A short identifier for your generator to be displayed in the world list.
     * Up to three capitalized letters, like an airport identifier.
     */
    string Moniker { get; }
    
    /**
     * A description of what kind of worlds your generator produces.
     * Aim for not much more than 120 characters.
     */
    string Description { get; }
    
    /**
     * Used for networking. If peers with your generator have different values of
     * this field, they can not play with each other.
     */
    string Version { get; }

    /**
     * Features your generator implements (see the enum above)
     */
    Feature[] Features { get; }
    
    /**
     * A long synchronous call that results in an IWorldState that can be queried for height and biome of any point.
     * Do all the worldwide preprocessing in here.
     * 
     * Progress is reported from 0 to 1; a generator that never reports it is shown as busy without a percentage.
     * Cancellation is requested when the result is no longer wanted, such as a world preview whose seed has changed.
     * Throwing OperationCanceledException then skips the remaining work; ignoring the token is allowed but wasteful.
     *
     * This method might be called on a background thread so avoid access to global state if you can.
     * Don't use UnityEngine.Random because it doesn't work on a background thread at all.
     */
    IWorldState Generate(
        int seed,
        ISettings? settings,
        IProgress<float> progress,
        CancellationToken cancellation);
}
