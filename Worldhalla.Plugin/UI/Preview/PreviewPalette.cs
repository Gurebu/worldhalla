using UnityEngine;
using Worldhalla.API.World;

namespace Worldhalla.Plugin.UI.Preview;

// One sRGB colour per biome and one for water, shared by the small and the detailed preview. Biomes without a colour
// of their own are drawn as meadows. Biome.Ocean is the colour of the ground under the sea
public static class PreviewPalette
{
    public static readonly Color Water = Hex(0x193256);

    private static readonly Dictionary<Biome, Color> Biomes = new()
    {
        [Biome.Meadows] = Hex(0x92a85c),
        [Biome.Swamp] = Hex(0x915231),
        [Biome.Mountain] = Hex(0xd9dee6),
        [Biome.BlackForest] = Hex(0x6b753f),
        [Biome.Plains] = Hex(0xd7b46e),
        [Biome.AshLands] = Hex(0x7a342d),
        [Biome.DeepNorth] = Hex(0xe6eef7),
        [Biome.Ocean] = Hex(0xc2b280),
        [Biome.Mistlands] = Hex(0x7c6a88),
    };

    public static Color Of(Biome biome)
    {
        return Biomes.TryGetValue(biome, out var color) ? color : Biomes[Biome.Meadows];
    }

    public static Color Hex(int rgb)
    {
        return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }

    public static float ToLinear(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

    public static float ToSrgb(float c) => c <= 0.0031308f ? 12.92f * c : 1.055f * Mathf.Pow(c, 1f / 2.4f) - 0.055f;

    public static Vector3 Linear(Color c) => new(ToLinear(c.r), ToLinear(c.g), ToLinear(c.b));
}
