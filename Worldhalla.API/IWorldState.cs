namespace Worldhalla.API;

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
        
    // TODO various global things (ashlands & deepnorth etc)
        
    float WorldSize { get; }
        
    Biome GetBiome(float x, float y);
        
    float GetForestation(float x, float y);

    PointData RenderPoint(float x, float y);
}
