namespace Worldhalla.API.World;

/**
 * Optional interface for world generators that modify the world's global
 * geographic properties. If you think you need this, it has to be implemented by
 * the same class that implements IWorldState.
 */
public interface IWorldGeography
{
    /**
     * As of worldhalla 1.0.0 world size is not supported out of the box, but might be supported
     * by other mods. This is the full world size, including the edge.
     *
     * Queries outside the world size are possible, but are not expected to return anything meaningful.
     */
    float WorldSize { get; }
    
    /**
     * Governs the behavior for lava and ashlands environment.
     */
    bool IsAshlands(float x, float y);

    /**
     * Governs deep north cultivation rules (cultivating within this zone produces snow instead
     * of fertile soil).
     */
    bool IsDeepNorth(float x, float y);

    /**
     * Governs character and vessel damage in ashlands water as well as red tint on the map.
     * Red water color in game is governed by the shader and currently doesn't work with worldhalla.
     *
     * Vanilla values are 1.0 for every 300 meters past the boundary.
     */
    float GetAshlandsOceanGradient(float x, float y);
    
    /**
     * TODO maybe rename to just wave suppression
     * 
     * Governs large wave suppression in northern oceans.
     * Values from [0..1]
     */
    float GetDeepNorthWaveFade(float x, float y);
}
