using UnityEngine;
using Worldhalla.API.World;

namespace Worldhalla.Plugin.UI.Preview;

internal sealed class SimplePreviewStyle : IPreviewStyle
{
    private const float DEEP_WATER = -20f;
    private const float SWAMP_WATER = -2f;

    private static readonly Color ShallowWater = Rgb(30, 58, 96);
    private static readonly Color DeepWater = Rgb(25, 50, 86);

    public Color Paint(PreviewGrid grid, int i, int row)
    {
        var height = grid.Height(i, row);
        var biome = grid.Biome(i, row);

        if (height <= 0f && !(biome == Biome.Swamp && height > SWAMP_WATER))
            return height < DEEP_WATER ? DeepWater : ShallowWater;

        return biome switch
        {
            Biome.Meadows => Rgb(146, 168, 92),
            Biome.BlackForest => Rgb(107, 117, 63),
            Biome.Swamp => Rgb(163, 115, 89),
            Biome.Mountain => Rgb(217, 222, 230),
            Biome.Plains => Rgb(222, 196, 120),
            Biome.Mistlands => Rgb(124, 106, 136),
            Biome.AshLands => Rgb(122, 52, 45),
            Biome.DeepNorth => Rgb(230, 238, 247),
            _ => Rgb(146, 168, 92),
        };
    }

    private static Color Rgb(byte r, byte g, byte b) => new Color32(r, g, b, 255);
}
