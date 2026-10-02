using JetBrains.Annotations;

namespace Worldhalla.API.World;

/**
 * Representation of the generated read-only world state
 * that supports immediate queries of height, biome, forestation and other metadata.
 *
 * All methods here might be called on any thread, concurrently.
 * All methods here might be called with any input coordinates,
 * but are only expected to return reasonable values within world radius.
 */
[PublicAPI]
public interface IWorldState
{
    [PublicAPI]
    struct PointData
    {
        public Biome biome;
        
        /**
         * height in meters above sea level
         *
         * reasonable heights are between -50 and 1000 or so
         * extending this range requires further modding
         */
        public float height;
        
        /**
         * range [0.0 - 1.0]
         * return 1.0 in places that are possible to cultivate
         *
         * low values also result in rock texture in the mistlands
         */
        public float fertility;
        
        /**
         * range [0.0 - 1.0]
         * 
         * this will only work in the ashlands biome properly
         */
        public float lava;
        
        /**
         * range [0.0 - 1.0]
         * 
         * depth of deep north snow, in meters
         */
        public float snow;
        
        /**
         * range [0.0 - 1.0]
         * 
         * area with 1.0 will lack any grass, as if a hoe has been used
         * to build a dirt path
         */
        public float dirt;
        
        /**
         * range [0.0 - 1.0]
         * 
         * area with 1.0 will be covered in  stone patch texture
         */
        public float pave;
    }
    
    Biome GetBiome(float x, float y);

    /**
     * How forested an area is, in range of [0.0 - 1.0]
     *
     * Note that this is changed compared to Valheim's ForestFractal, which is non-normalized and inverted.
     * Biomes that support this without further modding are Meadows, Plains and Deep North.
     *
     * This value also regulates mist in the Mistlands.
     */
    float GetForestation(float x, float y);

    /**
     * Without the BiomeBlending feature the game blends biome borders: at chunks whose corners lie in different
     * biomes it renders every point once per corner biome and interpolates between the results, so the point has to
     * be rendered as if it belonged to the given biome.
     *
     * Generators with the BiomeBlending feature always receive Biome.None and render the point as it is.
     */
    PointData RenderPoint(float x, float y, Biome biome);
}
