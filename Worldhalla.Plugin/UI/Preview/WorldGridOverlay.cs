using UnityEngine;

namespace Worldhalla.Plugin.UI.Preview;

internal static class WorldGridOverlay
{
    private const float RING_SPACING = 1000f;
    private const float LINE_WIDTH_PIXELS = 1f;
    private const float STRENGTH = 0.5f;
    private static readonly Color LineColor = new Color32(245, 245, 220, 255);

    public static Color Apply(PreviewGrid grid, int i, int row, Color color)
    {
        var coverage = LineCoverage(grid.Position(i, row), grid.radius, LINE_WIDTH_PIXELS * grid.step);
        return Color.Lerp(color, LineColor, STRENGTH * coverage);
    }

    private static float LineCoverage(Vector2 position, float radius, float width)
    {
        var length = position.magnitude;
        var ring = Mathf.Round(length / RING_SPACING) * RING_SPACING;
        var ringDistance = ring > 0f && ring < radius ? Mathf.Abs(length - ring) : float.PositiveInfinity;

        var distance = Mathf.Min(
            length,
            ringDistance,
            Mathf.Abs(position.x),
            Mathf.Abs(position.y),
            Mathf.Abs(position.x - position.y) / Mathf.Sqrt(2f),
            Mathf.Abs(position.x + position.y) / Mathf.Sqrt(2f));

        return Mathf.Clamp01(1f - distance / width);
    }
}
