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

    PointData RenderPoint(float x, float y);
}
