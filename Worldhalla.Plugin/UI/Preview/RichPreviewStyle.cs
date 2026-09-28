using UnityEngine;
using Worldhalla.API.World;

namespace Worldhalla.Plugin.UI.Preview;

internal sealed class RichPreviewStyle : IPreviewStyle
{
    private const float SLOPE_EXAGGERATION = 20f;
    private const float AMBIENT_PART = 0.1f;
    private const float COASTLINE_STRENGTH = 0.3f;
    private const float ISOLINE_STRENGTH = 0.125f;
    private static readonly Vector3 LightDirection = new Vector3(-1f, 1f, 2.5f).normalized;

    private static readonly float[] Isolines = [20f, 40f, 60f, 100f, 140f, 180f, 220f, 260f, 300f, 340f, 380f, 420f, 460f];

    private static readonly Color DeepWater = Rgb(0, 0, 0);
    private static readonly Color MidWater = Rgb(70, 130, 180);
    private static readonly Color ShallowWater = Rgb(64, 224, 208);
    private static readonly Color SwampWater = Rgb(47, 79, 79);

    public Color Paint(PreviewGrid grid, int i, int row)
    {
        var height = grid.Height(i, row);
        var biome = grid.Biome(i, row);

        var color = height > 0f ? Land(biome, height) : Water(biome, height);

        var gradient = grid.Gradient(i, row) * SLOPE_EXAGGERATION;
        var normal = new Vector3(-gradient.x, -gradient.y, 1f).normalized;
        var lightness = 0.5f + 0.5f * Mathf.Max(-1f, Vector3.Dot(normal, LightDirection));
        var attenuation = height >= 0f ? 1f : Mathf.Exp(0.05f * height);
        color = Color.Lerp(color * lightness, color, Mathf.Lerp(AMBIENT_PART, 1f, 1f - attenuation));

        if (CrossesBoundary(grid, i, row, h => h > 0f ? 1 : 0))
            color = Color.Lerp(color, Color.black, COASTLINE_STRENGTH);
        else if (CrossesBoundary(grid, i, row, IsolineBand))
            color = Color.Lerp(color, Color.black, ISOLINE_STRENGTH);

        return WorldGridOverlay.Apply(grid, i, row, color);
    }

    // Comparing against the right and upper neighbours only keeps every line one pixel wide
    private static bool CrossesBoundary(PreviewGrid grid, int i, int row, Func<float, int> band)
    {
        var own = band(grid.Height(i, row));
        return own != band(grid.Height(i + 1, row)) || own != band(grid.Height(i, row + 1));
    }

    private static int IsolineBand(float height)
    {
        var band = 0;
        while (band < Isolines.Length && height >= Isolines[band])
            band++;
        return band;
    }

    private static Color Land(Biome biome, float height)
    {
        var (low, high) = biome switch
        {
            Biome.Meadows => (Rgb(50, 205, 50), Rgb(152, 251, 152)),
            Biome.BlackForest => (Rgb(85, 107, 47), Rgb(154, 205, 50)),
            Biome.Swamp => (Rgb(139, 69, 19), Rgb(205, 133, 63)),
            Biome.Mountain => (Rgb(175, 238, 238), Rgb(240, 255, 255)),
            Biome.Plains => (Rgb(218, 165, 32), Rgb(240, 230, 140)),
            Biome.Mistlands => (Rgb(138, 43, 226), Rgb(221, 160, 221)),
            Biome.AshLands => (Rgb(56, 52, 52), Rgb(146, 99, 99)),
            Biome.DeepNorth => (Rgb(255, 222, 173), Rgb(255, 248, 220)),
            _ => (Rgb(50, 205, 50), Rgb(152, 251, 152)),
        };

        var t = biome == Biome.Mountain
            ? Mathf.InverseLerp(50f, 400f, height)
            : Mathf.InverseLerp(0f, 70f, height);
        return Color.Lerp(low, high, t);
    }

    private static Color Water(Biome biome, float height)
    {
        var color = height < -30f
            ? Color.Lerp(DeepWater, MidWater, Mathf.InverseLerp(-300f, -30f, height))
            : Color.Lerp(MidWater, ShallowWater, Mathf.InverseLerp(-30f, 0f, height));

        if (biome == Biome.Swamp)
            color = Color.Lerp(color, SwampWater, Mathf.InverseLerp(-2.5f, -1f, height));

        return color;
    }

    private static Color Rgb(byte r, byte g, byte b) => new Color32(r, g, b, 255);
}
