using UnityEngine;

namespace Worldhalla.Plugin.UI.Preview;

// A view is a square of SIZE pixels laid over the sample lattice, each pixel covering cellsPerPixel² cells. Its origin
// stays a multiple of cellsPerPixel, so the cells one zoom samples are a subset of those the next zoom in samples
internal readonly struct PreviewView : IEquatable<PreviewView>
{
    public const int SIZE = 1024;
    public const int MAX_ZOOM = 4;
    public const int CELLS = SIZE * MAX_ZOOM;

    public static readonly PreviewView Whole = new(Vector2Int.zero, MAX_ZOOM);

    public readonly Vector2Int origin;
    public readonly int cellsPerPixel;

    private PreviewView(Vector2Int origin, int cellsPerPixel)
    {
        this.origin = origin;
        this.cellsPerPixel = cellsPerPixel;
    }

    public bool IsWhole => cellsPerPixel == MAX_ZOOM;

    public PreviewView Panned(Vector2 pixels)
    {
        return new PreviewView(origin - Vector2Int.RoundToInt(pixels) * cellsPerPixel, cellsPerPixel).Fitted();
    }

    public PreviewView Zoomed(Vector2 aroundPixels, bool zoomIn)
    {
        var zoomed = zoomIn ? Mathf.Max(1, cellsPerPixel / 2) : Mathf.Min(MAX_ZOOM, cellsPerPixel * 2);
        var anchor = (Vector2)origin + aroundPixels * cellsPerPixel;
        return new PreviewView(Vector2Int.RoundToInt(anchor - aroundPixels * zoomed), zoomed).Fitted();
    }

    // Where this view lands within the shown one, in the shown one's normalised coordinates
    public Rect PlacementIn(PreviewView shown)
    {
        var extent = (float)SIZE * shown.cellsPerPixel;
        return new Rect((Vector2)(origin - shown.origin) / extent, Vector2.one * ((float)cellsPerPixel / shown.cellsPerPixel));
    }

    public Vector2 Normalized(Vector2 position, float radius)
    {
        var cell = (position + radius * Vector2.one) * (CELLS / (2f * radius));
        return (cell - origin) / ((float)SIZE * cellsPerPixel);
    }

    private PreviewView Fitted()
    {
        var limit = CELLS - SIZE * cellsPerPixel;
        return new PreviewView(new Vector2Int(Fit(origin.x, limit), Fit(origin.y, limit)), cellsPerPixel);
    }

    private int Fit(int coordinate, int limit)
    {
        return Mathf.Clamp(Mathf.RoundToInt((float)coordinate / cellsPerPixel) * cellsPerPixel, 0, limit);
    }

    public bool Equals(PreviewView other) => origin == other.origin && cellsPerPixel == other.cellsPerPixel;

    public override bool Equals(object? obj) => obj is PreviewView other && Equals(other);

    public override int GetHashCode() => origin.GetHashCode() * 31 + cellsPerPixel;
}
