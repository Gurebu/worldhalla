using UnityEngine;
using Worldhalla.Plugin.UI.Preview.Styles;

namespace Worldhalla.Plugin.UI.Preview.Rendering;

public sealed class PreviewRender
{
    private readonly CancellationTokenSource cancellation = new();
    private volatile int paintedRows;

    public readonly int size;
    public readonly Color32[] pixels;

    public int PaintedRows => paintedRows;
    public bool IsComplete => paintedRows == size;
    public Exception? Error { get; private set; }

    private PreviewRender(int size)
    {
        this.size = size;
        pixels = new Color32[size * size];
    }

    public static PreviewRender Start(WorldGenerator generator, float radius, int size, int supersampling, IPreviewStyle style)
    {
        var render = new PreviewRender(size);
        var token = render.cancellation.Token;
        Task.Run(() => render.Render(generator, radius, supersampling, style, token), token);
        return render;
    }

    public void Cancel()
    {
        cancellation.Cancel();
    }

    // The grid holds supersampling² samples per pixel. Rows are painted top (north) first; a pixel row is painted once
    // the sample row below its own samples is taken, since styles may look at both neighbours of a sample
    private void Render(WorldGenerator generator, float radius, int supersampling, IPreviewStyle style, CancellationToken token)
    {
        try
        {
            var gridSize = size * supersampling;
            var grid = new PreviewGrid(gridSize, radius);

            for (var row = size - 1; row >= 0; row--)
            {
                for (var sample = supersampling - 1; sample >= 0; sample--)
                {
                    if (token.IsCancellationRequested)
                        return;

                    SampleRow(generator, row * supersampling + sample, radius, supersampling, grid);
                }

                if (row < size - 1)
                    PaintRow(row + 1, radius, supersampling, grid, style);
            }

            PaintRow(0, radius, supersampling, grid, style);
        }
        catch (Exception e)
        {
            Error = e;
        }
    }

    private void SampleRow(WorldGenerator generator, int row, float radius, int supersampling, PreviewGrid grid)
    {
        var step = grid.step;
        var z = -radius + (row + 0.5f) * step;
        var sampledRadius = radius + 2f * supersampling * step;

        Parallel.For(0, grid.size, i =>
        {
            var x = -radius + (i + 0.5f) * step;
            if (x * x + z * z > sampledRadius * sampledRadius)
                return;

            var (height, biome, lava) = PreviewSampling.Sample(generator, x, z);
            grid.heights[row * grid.size + i] = height;
            grid.biomes[row * grid.size + i] = biome;
            grid.lava[row * grid.size + i] = lava;
        });
    }

    private void PaintRow(int row, float radius, int supersampling, PreviewGrid grid, IPreviewStyle style)
    {
        var center = 0.5f * size;
        var pixelRadius = radius / (grid.step * supersampling);

        for (var i = 0; i < size; i++)
        {
            var dx = i + 0.5f - center;
            var dy = row + 0.5f - center;
            var alpha = Mathf.Clamp01(pixelRadius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
            if (alpha <= 0f)
            {
                pixels[row * size + i] = default;
                continue;
            }

            var color = Color.clear;
            for (var sy = 0; sy < supersampling; sy++)
            for (var sx = 0; sx < supersampling; sx++)
                color += style.Paint(grid, i * supersampling + sx, row * supersampling + sy);

            color /= supersampling * supersampling;
            color.a = alpha;
            pixels[row * size + i] = color;
        }

        paintedRows++;
    }
}
