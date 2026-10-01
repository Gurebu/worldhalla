using UnityEngine;
using Worldhalla.API.World;

namespace Worldhalla.Plugin.UI.Preview;

// Samples on a fixed lattice of cells over the whole world, and finished pixels for every zoom, addressed by the view's
// own pixel grid at that zoom
internal sealed class PreviewCache
{
    private const float OUTSIDE_MARGIN_CELLS = 2f;

    private readonly TileCache<(float height, Biome biome)> samples;
    private readonly Dictionary<int, TileCache<Color32>> pixels = new();
    private volatile bool empty = true;

    public readonly int cells;
    public readonly float radius;
    public readonly float step;

    public PreviewCache(float radius, int cells)
    {
        this.radius = radius;
        this.cells = cells;
        step = 2f * radius / cells;
        samples = new TileCache<(float, Biome)>(cells);

        for (var cellsPerPixel = 1; cellsPerPixel <= PreviewView.MAX_ZOOM; cellsPerPixel *= 2)
            pixels[cellsPerPixel] = new TileCache<Color32>(cells / cellsPerPixel);
    }

    public bool IsEmpty => empty;

    public Vector2 Position(Vector2Int cell)
    {
        return new Vector2(-radius + (cell.x + 0.5f) * step, -radius + (cell.y + 0.5f) * step);
    }

    public PreviewGrid NewGrid(int size, Vector2Int originCell, int spacing)
    {
        return new PreviewGrid(size, radius, Position(originCell) - 0.5f * spacing * step * Vector2.one, spacing * step);
    }

    // Cells outside the world read as known and empty
    public bool TryGetSample(Vector2Int cell, out float height, out Biome biome)
    {
        height = 0f;
        biome = default;

        if (IsOutside(cell))
            return true;

        if (!samples.TryGet(cell.x, cell.y, out var sample))
            return false;

        (height, biome) = sample;
        return true;
    }

    public void SetSample(Vector2Int cell, float height, Biome biome)
    {
        samples.Set(cell.x, cell.y, (height, biome));
        empty = false;
    }

    public TileCache<Color32> Pixels(int cellsPerPixel) => pixels[cellsPerPixel];

    private bool IsOutside(Vector2Int cell)
    {
        return cell.x < 0 || cell.y < 0 || cell.x >= cells || cell.y >= cells ||
               Position(cell).magnitude > radius + OUTSIDE_MARGIN_CELLS * step;
    }
}
