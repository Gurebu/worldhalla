using UnityEngine;

namespace Worldhalla.Plugin.UI.Preview.Locations;

// Surrounds a map icon with a dark outline and a soft drop shadow, so it reads on whatever terrain the preview paints
internal static class IconOutline
{
    private const float OUTLINE = 0.04f;
    private const float SHADOW_BLUR = 0.06f;
    private const float SHADOW_OFFSET = 0.03f;
    private const float SHADOW_OPACITY = 0.6f;
    public static readonly Color Ink = new(0.06f, 0.05f, 0.03f);

    // Added on each side as a fraction of the icon's larger side, so an image showing the result grows by twice this
    public const float MARGIN = OUTLINE + SHADOW_BLUR + SHADOW_OFFSET;

    // Takes a readable texture
    public static Texture2D Apply(Texture2D icon)
    {
        var size = Mathf.Max(icon.width, icon.height);
        var pad = Mathf.CeilToInt(size * MARGIN);
        var width = icon.width + 2 * pad;
        var height = icon.height + 2 * pad;

        var source = icon.GetPixels32();
        var padded = new Color32[width * height];
        var alpha = new float[width * height];
        for (var y = 0; y < icon.height; y++)
        for (var x = 0; x < icon.width; x++)
        {
            var i = (y + pad) * width + x + pad;
            padded[i] = source[y * icon.width + x];
            alpha[i] = padded[i].a / 255f;
        }

        var outline = Dilate(alpha, width, height, Mathf.Max(1f, size * OUTLINE));
        var shadow = Blur(outline, width, height, Mathf.RoundToInt(size * SHADOW_BLUR));
        var offset = Mathf.RoundToInt(size * SHADOW_OFFSET);

        var pixels = new Color32[width * height];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var i = y * width + x;
            var ink = outline[i] + SHADOW_OPACITY * Sample(shadow, width, height, x - offset, y + offset) * (1f - outline[i]);
            pixels[i] = Over(padded[i], ink);
        }

        var result = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        result.SetPixels32(pixels);
        result.Apply(false);
        return result;
    }

    private static Color32 Over(Color32 top, float inkAlpha)
    {
        var color = (Color)top;
        var alpha = color.a + inkAlpha * (1f - color.a);
        if (alpha <= 0f)
            return new Color32(0, 0, 0, 0);

        var rgb = (color * color.a + Ink * (inkAlpha * (1f - color.a))) / alpha;
        rgb.a = alpha;
        return rgb;
    }

    private static float[] Dilate(float[] alpha, int width, int height, float radius)
    {
        var reach = Mathf.CeilToInt(radius);
        var result = new float[alpha.Length];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var best = 0f;
            for (var dy = -reach; dy <= reach; dy++)
            for (var dx = -reach; dx <= reach; dx++)
            {
                var a = Sample(alpha, width, height, x + dx, y + dy);
                if (a > best)
                    best = Mathf.Max(best, a * Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy)));
            }

            result[y * width + x] = best;
        }

        return result;
    }

    // Two box blurs in each direction, close enough to a gaussian for a shadow
    private static float[] Blur(float[] alpha, int width, int height, int radius)
    {
        var result = alpha;
        for (var pass = 0; radius > 0 && pass < 2; pass++)
            result = BoxBlur(BoxBlur(result, width, height, radius, 1, 0), width, height, radius, 0, 1);

        return result;
    }

    private static float[] BoxBlur(float[] alpha, int width, int height, int radius, int dx, int dy)
    {
        var result = new float[alpha.Length];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var sum = 0f;
            for (var k = -radius; k <= radius; k++)
                sum += Sample(alpha, width, height, x + k * dx, y + k * dy);

            result[y * width + x] = sum / (2 * radius + 1);
        }

        return result;
    }

    private static float Sample(float[] alpha, int width, int height, int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height ? alpha[y * width + x] : 0f;
    }
}
