namespace Worldhalla.API.World;

public interface IWorldState
{
    struct PointData
    {
        public Biome biome;
        public float height;
        public float fertility;
        public float lava;
        public float snow;
        public float dirt;
        public float pave;
    }
    
    Biome GetBiome(float x, float y);

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
