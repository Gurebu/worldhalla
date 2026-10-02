namespace Worldhalla.Plugin.UI.Preview;

// Square tiles allocated on first use. A value is written before its flag, so a reader on another thread sees it
// either whole or not at all
public sealed class TileCache<T> where T : struct
{
    private const int TILE_SIZE = 128;

    private sealed class Tile
    {
        public readonly T[] values = new T[TILE_SIZE * TILE_SIZE];
        public readonly bool[] known = new bool[TILE_SIZE * TILE_SIZE];
    }

    private readonly Tile?[] tiles;
    private readonly int tilesPerSide;

    public readonly int size;

    public TileCache(int size)
    {
        this.size = size;
        tilesPerSide = (size + TILE_SIZE - 1) / TILE_SIZE;
        tiles = new Tile?[tilesPerSide * tilesPerSide];
    }

    public bool TryGet(int x, int y, out T value)
    {
        value = default;
        if (x < 0 || y < 0 || x >= size || y >= size)
            return false;

        var tile = Volatile.Read(ref tiles[TileIndex(x, y)]);
        var index = IndexInTile(x, y);
        if (tile == null || !Volatile.Read(ref tile.known[index]))
            return false;

        value = tile.values[index];
        return true;
    }

    public void Set(int x, int y, T value)
    {
        var tileIndex = TileIndex(x, y);
        if (Volatile.Read(ref tiles[tileIndex]) == null)
            Interlocked.CompareExchange(ref tiles[tileIndex], new Tile(), null);

        var tile = Volatile.Read(ref tiles[tileIndex])!;
        var index = IndexInTile(x, y);
        tile.values[index] = value;
        Volatile.Write(ref tile.known[index], true);
    }

    private int TileIndex(int x, int y) => y / TILE_SIZE * tilesPerSide + x / TILE_SIZE;

    private static int IndexInTile(int x, int y) => y % TILE_SIZE * TILE_SIZE + x % TILE_SIZE;
}
