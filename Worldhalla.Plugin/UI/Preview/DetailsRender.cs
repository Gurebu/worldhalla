using UnityEngine;
using Worldhalla.API.World;
using Worldhalla.Plugin.Internal;

namespace Worldhalla.Plugin.UI.Preview;

// Passes go from coarse blocks to single pixels, each block painted from the sample at its south-west corner. Every
// pixel remembers the block size it was painted at, and a pass only repaints pixels coarser than itself. Before any of
// them, pixels the cache already has finished are copied, and then those its samples are enough for are painted, so
// revisited ground never shows up coarse
internal sealed class DetailsRender
{
    private const byte UNPAINTED = byte.MaxValue;
    public const int COARSEST_BLOCK = 16;

    private readonly CancellationTokenSource cancellation = new();
    private readonly GeneratedWorld world;
    private readonly PreviewCache cache;
    private readonly IPreviewStyle style;
    private readonly int coarsestBlock;
    private readonly byte[] blocks;
    private volatile int revision;
    private volatile bool ready;
    private volatile bool complete;

    public readonly PreviewView view;
    public readonly Color32[] pixels;

    public int Revision => revision;

    // Set once the render starts waiting on the generator, so it never shows what the cache could have filled in
    public bool IsReady => ready;

    public bool IsComplete => complete;

    public Exception? Error { get; private set; }

    private DetailsRender(GeneratedWorld world, PreviewCache cache, PreviewView view, int coarsestBlock, IPreviewStyle style)
    {
        this.coarsestBlock = coarsestBlock;
        this.world = world;
        this.cache = cache;
        this.view = view;
        this.style = style;
        pixels = new Color32[PreviewView.SIZE * PreviewView.SIZE];
        blocks = new byte[PreviewView.SIZE * PreviewView.SIZE];
        for (var i = 0; i < blocks.Length; i++)
            blocks[i] = UNPAINTED;
    }

    public static DetailsRender Start(GeneratedWorld world, PreviewCache cache, PreviewView view, int coarsestBlock, IPreviewStyle style)
    {
        var render = new DetailsRender(world, cache, view, coarsestBlock, style);
        var token = render.cancellation.Token;
        Task.Run(() => render.Render(token), token);
        return render;
    }

    public void Cancel()
    {
        cancellation.Cancel();
    }

    private void Render(CancellationToken token)
    {
        try
        {
            // A full-size sweep that finds nothing would hold up the first coarse blocks
            if (!cache.IsEmpty)
            {
                CopyCachedPixels();
                revision++;

                if (!RenderPass(1, false, token))
                    return;
            }

            for (var block = coarsestBlock; block >= 1; block /= 2)
            {
                if (!RenderPass(block, true, token))
                    return;
            }

            complete = true;
        }
        catch (Exception e)
        {
            Error = e;
        }
        finally
        {
            ready = true;
        }
    }

    private void CopyCachedPixels()
    {
        var cached = cache.Pixels(view.cellsPerPixel);
        var origin = view.origin / view.cellsPerPixel;

        Parallel.For(0, PreviewView.SIZE, row =>
        {
            for (var i = 0; i < PreviewView.SIZE; i++)
            {
                if (!cached.TryGet(origin.x + i, origin.y + row, out var pixel))
                    continue;

                pixels[row * PreviewView.SIZE + i] = pixel;
                blocks[row * PreviewView.SIZE + i] = 1;
            }
        });
    }

    // The grid has a sample per block plus a margin of one on each side, since styles look at neighbouring samples
    private bool RenderPass(int block, bool query, CancellationToken token)
    {
        var count = PreviewView.SIZE / block;
        var stale = FindStaleBlocks(block, count);
        if (stale == null)
            return true;

        var spacing = block * view.cellsPerPixel;
        var gridOrigin = view.origin - spacing * Vector2Int.one;
        var grid = cache.NewGrid(count + 2, gridOrigin, spacing);
        var wanted = WantedPoints(stale, count);
        var known = new bool[grid.size * grid.size];

        for (var row = grid.size - 1; row >= 0; row--)
        {
            if (token.IsCancellationRequested)
                return false;

            SampleRow(grid, row, gridOrigin, spacing, wanted, known, query);

            if (row < count)
            {
                PaintRow(grid, row, block, count, stale, known);
                revision++;
            }
        }

        return true;
    }

    private bool[]? FindStaleBlocks(int block, int count)
    {
        var stale = new bool[count * count];
        var any = false;

        for (var row = 0; row < PreviewView.SIZE; row++)
        for (var i = 0; i < PreviewView.SIZE; i++)
        {
            if (blocks[row * PreviewView.SIZE + i] <= block)
                continue;

            stale[row / block * count + i / block] = true;
            any = true;
        }

        return any ? stale : null;
    }

    private static bool[] WantedPoints(bool[] stale, int count)
    {
        var gridSize = count + 2;
        var wanted = new bool[gridSize * gridSize];

        for (var row = 0; row < count; row++)
        for (var i = 0; i < count; i++)
        {
            if (!stale[row * count + i])
                continue;

            for (var dy = 0; dy < 3; dy++)
            for (var dx = 0; dx < 3; dx++)
                wanted[(row + dy) * gridSize + i + dx] = true;
        }

        return wanted;
    }

    private void SampleRow(PreviewGrid grid, int row, Vector2Int gridOrigin, int spacing, bool[] wanted, bool[] known, bool query)
    {
        Parallel.For(0, grid.size, i =>
        {
            var index = row * grid.size + i;
            if (!wanted[index])
                return;

            var cell = gridOrigin + new Vector2Int(i, row) * spacing;
            if (!cache.TryGetSample(cell, out var height, out var biome, out var lava))
            {
                if (!query)
                    return;

                ready = true;
                var position = cache.Position(cell);
                var point = world.RenderPoint(position.x, position.y);
                (height, biome, lava) = (point.height, point.biome, point.lava);
                cache.SetSample(cell, height, biome, lava);
            }

            grid.heights[index] = height;
            grid.biomes[index] = biome;
            grid.lava[index] = lava;
            known[index] = true;
        });
    }

    private void PaintRow(PreviewGrid grid, int blockRow, int block, int count, bool[] stale, bool[] known)
    {
        var pixel = view.cellsPerPixel * cache.step;
        var origin = cache.Position(view.origin) - 0.5f * cache.step * Vector2.one;
        var cached = cache.Pixels(view.cellsPerPixel);
        var cachedOrigin = view.origin / view.cellsPerPixel;

        Parallel.For(0, count, blockI =>
        {
            if (!stale[blockRow * count + blockI] || !IsKnownAround(known, grid.size, blockI + 1, blockRow + 1))
                return;

            var color = style.Paint(grid, blockI + 1, blockRow + 1);

            for (var row = blockRow * block; row < (blockRow + 1) * block; row++)
            for (var i = blockI * block; i < (blockI + 1) * block; i++)
            {
                var index = row * PreviewView.SIZE + i;
                if (blocks[index] <= block)
                    continue;

                var position = origin + new Vector2(i + 0.5f, row + 0.5f) * pixel;
                var alpha = Mathf.Clamp01((cache.radius - position.magnitude) / pixel + 0.5f);
                Color32 painted = alpha > 0f ? new Color(color.r, color.g, color.b, alpha) : default;
                var final = block == 1 || alpha <= 0f;
                pixels[index] = painted;
                blocks[index] = final ? (byte)1 : (byte)block;

                if (final)
                    cached.Set(cachedOrigin.x + i, cachedOrigin.y + row, painted);
            }
        });
    }

    private static bool IsKnownAround(bool[] known, int gridSize, int i, int row)
    {
        for (var dy = -1; dy <= 1; dy++)
        for (var dx = -1; dx <= 1; dx++)
        {
            if (!known[(row + dy) * gridSize + i + dx])
                return false;
        }

        return true;
    }
}
