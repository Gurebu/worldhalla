using UnityEngine;

namespace Worldhalla.Plugin.UI.Util;

public static class Shapes
{
    public static Texture2D NewTexture(int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
    }

    public static Texture2D Disc(int size, Color color)
    {
        return Paint(size, (distance, _) => Mathf.Clamp01(0.5f * size - distance + 0.5f), color);
    }

    public static Texture2D Ring(int size, float width, Color color)
    {
        return Paint(size, (distance, _) => RingCoverage(size, width, distance), color);
    }

    public static Texture2D Arc(int size, float width, float sweepDegrees, Color color)
    {
        return Paint(size, (distance, angle) => angle <= sweepDegrees ? RingCoverage(size, width, distance) : 0f, color);
    }

    private static float RingCoverage(int size, float width, float distance)
    {
        var outer = 0.5f * size;
        return Mathf.Clamp01(outer - distance + 0.5f) * Mathf.Clamp01(distance - (outer - width) + 0.5f);
    }

    private static Texture2D Paint(int size, Func<float, float, float> coverage, Color color)
    {
        var texture = NewTexture(size);
        var pixels = new Color32[size * size];
        var center = 0.5f * size;

        for (var j = 0; j < size; j++)
        for (var i = 0; i < size; i++)
        {
            var dx = i + 0.5f - center;
            var dy = j + 0.5f - center;
            var angle = (Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 360f) % 360f;
            var pixel = color;
            pixel.a *= coverage(Mathf.Sqrt(dx * dx + dy * dy), angle);
            pixels[j * size + i] = pixel;
        }

        texture.SetPixels32(pixels);
        texture.Apply(false);
        return texture;
    }
}
