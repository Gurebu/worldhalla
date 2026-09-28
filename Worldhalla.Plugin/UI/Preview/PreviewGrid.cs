using UnityEngine;
using Worldhalla.API.World;

namespace Worldhalla.Plugin.UI.Preview;

internal sealed class PreviewGrid
{
    public readonly int size;
    public readonly float radius;
    public readonly float step;
    public readonly float[] heights;
    public readonly Biome[] biomes;

    public PreviewGrid(int size, float radius)
    {
        this.size = size;
        this.radius = radius;
        step = 2f * radius / size;
        heights = new float[size * size];
        biomes = new Biome[size * size];
    }

    public Vector2 Position(int i, int row)
    {
        return new Vector2(-radius + (i + 0.5f) * step, -radius + (row + 0.5f) * step);
    }

    public float Height(int i, int row)
    {
        return heights[Mathf.Clamp(row, 0, size - 1) * size + Mathf.Clamp(i, 0, size - 1)];
    }

    public Biome Biome(int i, int row)
    {
        return biomes[row * size + i];
    }

    public Vector2 Gradient(int i, int row)
    {
        return new Vector2(
            Height(i + 1, row) - Height(i - 1, row),
            Height(i, row + 1) - Height(i, row - 1)) / (2f * step);
    }
}
