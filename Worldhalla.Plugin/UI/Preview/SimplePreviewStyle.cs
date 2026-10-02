using UnityEngine;
using Worldhalla.API.World;

namespace Worldhalla.Plugin.UI.Preview;

public sealed class SimplePreviewStyle : IPreviewStyle
{
    private const float DEEP_WATER = -20f;
    private const float SWAMP_WATER = -2f;
    private const float SHALLOW_WATER_STOPS = 0.25f;

    private static readonly Color DeepWater = PreviewPalette.Water;
    private static readonly Color ShallowWater = Brighter(PreviewPalette.Water, SHALLOW_WATER_STOPS);

    public Color Paint(PreviewGrid grid, int i, int row)
    {
        var height = grid.Height(i, row);
        var biome = grid.Biome(i, row);

        if (height <= 0f && !(biome == Biome.Swamp && height > SWAMP_WATER))
            return height < DEEP_WATER ? DeepWater : ShallowWater;

        return PreviewPalette.Of(biome);
    }

    private static Color Brighter(Color color, float stops)
    {
        var linear = PreviewPalette.Linear(color) * Mathf.Pow(2f, stops);
        return new Color(
            PreviewPalette.ToSrgb(Mathf.Clamp01(linear.x)),
            PreviewPalette.ToSrgb(Mathf.Clamp01(linear.y)),
            PreviewPalette.ToSrgb(Mathf.Clamp01(linear.z)));
    }
}
