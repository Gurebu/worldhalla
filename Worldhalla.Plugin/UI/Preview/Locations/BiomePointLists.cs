using System.Runtime.Serialization;

namespace Worldhalla.Plugin.UI.Preview.Locations;

/*
 * A copy of the per-biome point lists AltBiomeWorldData builds over a world, which locations draw their zones from.
 * Draws index into the lists, so their order is the game's: Ash Lands, Deep North and Ocean points row by row, every
 * other biome as its flood fill visits it, the fill's first point left out as the game leaves it out.
 *
 * Only the lists are built. Picking from them goes through the game's own code on an otherwise empty
 * AltBiomeWorldData, which never constructs the full-size arrays nobody here reads.
 */
internal static class BiomePointLists
{
    private const int SIZE = AltBiomeWorldData.c_textureSize;
    private const float WORLD_RADIUS_SQUARED = 110250000f;
    private const float SEA_LEVEL = 30f;

    public static AltBiomeWorldData Build(WorldGenerator generator, Action<float> report, CancellationToken token)
    {
        var biomes = new Heightmap.Biome[SIZE * SIZE];
        var aboveSeaLevel = new bool[SIZE * SIZE];
        var rowsDone = 0;

        Parallel.For(0, SIZE, new ParallelOptions { CancellationToken = token }, y =>
        {
            for (var x = 0; x < SIZE; x++)
            {
                var wx = AltBiomeWorldData.MapSpaceToWorldSpace(x);
                var wy = AltBiomeWorldData.MapSpaceToWorldSpace(y);
                if (wx * wx + wy * wy > WORLD_RADIUS_SQUARED)
                {
                    biomes[y * SIZE + x] = Heightmap.Biome.Ocean;
                    continue;
                }

                var biome = generator.GetBiome(wx, wy);
                biomes[y * SIZE + x] = biome;
                aboveSeaLevel[y * SIZE + x] = generator.GetBiomeHeight(biome, wx, wy, out _) >= SEA_LEVEL;
            }

            report((float)Interlocked.Increment(ref rowsDone) / SIZE);
        });

        var data = (AltBiomeWorldData)FormatterServices.GetUninitializedObject(typeof(AltBiomeWorldData));
        data.Biomes = new Dictionary<Heightmap.Biome, BiomeTypeInfo>();
        foreach (Heightmap.Biome biome in Enum.GetValues(typeof(Heightmap.Biome)))
            data.Biomes[biome] = new BiomeTypeInfo(biome);

        var visited = new bool[SIZE * SIZE];

        for (var y = 0; y < SIZE; y++)
        for (var x = 0; x < SIZE; x++)
        {
            var biome = biomes[y * SIZE + x];
            if (biome is Heightmap.Biome.AshLands or Heightmap.Biome.DeepNorth or Heightmap.Biome.Ocean)
            {
                visited[y * SIZE + x] = true;
                Add(data, biome, x, y, aboveSeaLevel[y * SIZE + x]);
            }
        }

        var open = new Stack<(int x, int y)>(1024);

        for (var y = 0; y < SIZE; y++)
        for (var x = 0; x < SIZE; x++)
        {
            if (visited[y * SIZE + x])
                continue;

            visited[y * SIZE + x] = true;
            open.Push((x, y));

            while (open.Count > 0)
            {
                var (px, py) = open.Pop();
                var biome = biomes[py * SIZE + px];
                Fill(data, biomes, aboveSeaLevel, visited, open, biome, px + 1, py);
                Fill(data, biomes, aboveSeaLevel, visited, open, biome, px - 1, py);
                Fill(data, biomes, aboveSeaLevel, visited, open, biome, px, py + 1);
                Fill(data, biomes, aboveSeaLevel, visited, open, biome, px, py - 1);
            }
        }

        return data;
    }

    private static void Fill(AltBiomeWorldData data, Heightmap.Biome[] biomes, bool[] aboveSeaLevel, bool[] visited,
        Stack<(int, int)> open, Heightmap.Biome biome, int x, int y)
    {
        if (x < 0 || y < 0 || x >= SIZE || y >= SIZE || visited[y * SIZE + x] || biomes[y * SIZE + x] != biome)
            return;

        visited[y * SIZE + x] = true;
        Add(data, biome, x, y, aboveSeaLevel[y * SIZE + x]);
        open.Push((x, y));
    }

    private static void Add(AltBiomeWorldData data, Heightmap.Biome biome, int x, int y, bool aboveSeaLevel)
    {
        var point = new BiomePointCoordinate((short)x, (short)y);
        data.Biomes[biome].AllPoints.Add(point);
        if (aboveSeaLevel)
            data.Biomes[biome].AllPointsAboveSeaLevel.Add(point);
    }
}
