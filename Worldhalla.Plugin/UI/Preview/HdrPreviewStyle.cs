using UnityEngine;
using Worldhalla.API.World;

namespace Worldhalla.Plugin.UI.Preview;

/*
 * Lit in linear light from one palette colour per biome: the sun shades slopes relative to flat ground, height sets the
 * exposure in stops, water absorbs each channel by depth over the ground beneath it, lava glows, and the result is tone
 * mapped back to sRGB. Contour lines are traced by marching squares in the cells around the sample.
 */
public sealed class HdrPreviewStyle : IPreviewStyle
{
    private static readonly float[] Isolines = [20f, 40f, 60f, 100f, 140f, 180f, 220f, 260f, 300f, 340f, 380f, 420f, 460f];
    private static readonly Vector3 Luminance = new(0.2126f, 0.7152f, 0.0722f);

    private readonly HdrPreviewSettings settings;
    private readonly Dictionary<Biome, Vector3> albedo = new();
    private readonly Vector3 meadows;
    private readonly Vector3 light;
    private readonly Vector3 water;
    private readonly Vector3 sky;
    private readonly Vector3 fog;
    private readonly Vector3 lava;

    public HdrPreviewStyle() : this(new HdrPreviewSettings())
    {
    }

    public HdrPreviewStyle(HdrPreviewSettings settings)
    {
        this.settings = settings;
        foreach (Biome biome in Enum.GetValues(typeof(Biome)))
            albedo[biome] = PreviewPalette.Linear(PreviewPalette.Of(biome));
        meadows = albedo[Biome.Meadows];

        var azimuth = settings.sunAzimuth * Mathf.Deg2Rad;
        var elevation = settings.sunElevation * Mathf.Deg2Rad;
        light = new Vector3(Mathf.Sin(azimuth) * Mathf.Cos(elevation), Mathf.Cos(azimuth) * Mathf.Cos(elevation), Mathf.Sin(elevation));

        var exposure = Mathf.Pow(2f, settings.exposure);
        water = PreviewPalette.Linear(PreviewPalette.Water) * exposure;
        sky = PreviewPalette.Linear(settings.sky) * exposure;
        fog = PreviewPalette.Linear(settings.fogColor) * exposure;
        lava = PreviewPalette.Linear(settings.lavaColor);
    }

    public Color Paint(PreviewGrid grid, int i, int row)
    {
        var height = grid.Height(i, row);
        var ground = albedo.TryGetValue(grid.Biome(i, row), out var color) ? color : meadows;

        var gradient = grid.Gradient(i, row) * settings.slopeExaggeration;
        var normal = new Vector3(-gradient.x, -gradient.y, 1f).normalized;
        var direct = Mathf.Max(0f, Vector3.Dot(normal, light)) / light.z;
        var stops = settings.exposure + settings.lowStops +
                    (settings.highStops - settings.lowStops) * (1f - Mathf.Exp(-Mathf.Max(height, 0f) / settings.heightScale));
        var lit = ground * ((settings.shadowFill + (1f - settings.shadowFill) * direct) * Mathf.Pow(2f, stops));

        Vector3 radiance;
        if (height < 0f)
        {
            var depth = -height;
            var transmitted = new Vector3(
                Mathf.Exp(-settings.absorption.x * depth),
                Mathf.Exp(-settings.absorption.y * depth),
                Mathf.Exp(-settings.absorption.z * depth));
            radiance = Vector3.Scale(lit, transmitted) + Vector3.Scale(water * Mathf.Exp(-depth / settings.abyssScale), Vector3.one - transmitted);
            radiance = Vector3.Lerp(radiance, sky, settings.reflection);
        }
        else
        {
            radiance = lit + grid.Lava(i, row) * settings.lavaIntensity * lava;
            radiance = Vector3.Lerp(radiance, fog, settings.fogAmount * Mathf.Exp(-height / settings.fogHeight));
        }

        var mapped = ToneMap(radiance);
        var luminance = Vector3.Dot(mapped, Luminance);
        var result = new Color(
            PreviewPalette.ToSrgb(Mathf.Clamp01(luminance + (mapped.x - luminance) * settings.saturation)),
            PreviewPalette.ToSrgb(Mathf.Clamp01(luminance + (mapped.y - luminance) * settings.saturation)),
            PreviewPalette.ToSrgb(Mathf.Clamp01(luminance + (mapped.z - luminance) * settings.saturation)));

        var coast = settings.coastline * Coverage(settings.coastlineWidth, ContourDistance(grid, i, row, 0f));
        var isoline = settings.isolines * Coverage(settings.isolineWidth, IsolineDistance(grid, i, row));
        result *= (1f - coast) * (1f - isoline);
        result.a = 1f;

        return WorldGridOverlay.Apply(grid, i, row, result, settings.grid);
    }

    private Vector3 ToneMap(Vector3 c)
    {
        return settings.toneMapper switch
        {
            HdrPreviewSettings.ToneMapper.Aces => new Vector3(Aces(c.x), Aces(c.y), Aces(c.z)),
            HdrPreviewSettings.ToneMapper.Clamp => new Vector3(Mathf.Clamp01(c.x), Mathf.Clamp01(c.y), Mathf.Clamp01(c.z)),
            _ => Neutral(c),
        };
    }

    // Khronos PBR Neutral: colours below the compression start come out as they went in
    private static Vector3 Neutral(Vector3 c)
    {
        const float start = 0.8f - 0.04f;
        const float desaturation = 0.15f;

        var x = Mathf.Min(c.x, Mathf.Min(c.y, c.z));
        var offset = x < 0.08f ? x - 6.25f * x * x : 0.04f;
        c -= new Vector3(offset, offset, offset);

        var peak = Mathf.Max(c.x, Mathf.Max(c.y, c.z));
        if (peak < start)
            return c;

        const float d = 1f - start;
        var newPeak = 1f - d * d / (peak + d - start);
        c *= newPeak / peak;
        var g = 1f - 1f / (desaturation * (peak - newPeak) + 1f);
        return Vector3.Lerp(c, new Vector3(newPeak, newPeak, newPeak), g);
    }

    private static float Aces(float x) => Mathf.Clamp01(x * (2.51f * x + 0.03f) / (x * (2.43f * x + 0.59f) + 0.14f));

    private static float Coverage(float width, float distance) => Mathf.Clamp01(width / 2f + 0.5f - distance);

    private static float IsolineDistance(PreviewGrid grid, int i, int row)
    {
        var min = float.PositiveInfinity;
        var max = float.NegativeInfinity;
        for (var dy = -1; dy <= 1; dy++)
        for (var dx = -1; dx <= 1; dx++)
        {
            var h = grid.Height(i + dx, row + dy);
            min = Mathf.Min(min, h);
            max = Mathf.Max(max, h);
        }

        var best = float.PositiveInfinity;
        foreach (var level in Isolines)
        {
            if (level >= min && level < max)
                best = Mathf.Min(best, ContourDistance(grid, i, row, level));
        }

        return best;
    }

    private static float ContourDistance(PreviewGrid grid, int i, int row, float level)
    {
        var best = float.PositiveInfinity;
        for (var cy = row - 1; cy <= row; cy++)
        for (var cx = i - 1; cx <= i; cx++)
            best = Mathf.Min(best, CellDistance(grid, cx, cy, level, new Vector2(i, row)));

        return best;
    }

    // Marching squares over one cell: crossings in edge order, joined pairwise
    private static float CellDistance(PreviewGrid grid, int cx, int cy, float level, Vector2 point)
    {
        Span<Vector2> crossings = stackalloc Vector2[4];
        var count = 0;
        Cross(grid, cx, cy, cx + 1, cy, level, crossings, ref count);
        Cross(grid, cx + 1, cy, cx + 1, cy + 1, level, crossings, ref count);
        Cross(grid, cx + 1, cy + 1, cx, cy + 1, level, crossings, ref count);
        Cross(grid, cx, cy + 1, cx, cy, level, crossings, ref count);

        var best = float.PositiveInfinity;
        for (var s = 0; s + 1 < count; s += 2)
            best = Mathf.Min(best, SegmentDistance(point, crossings[s], crossings[s + 1]));

        return best;
    }

    private static void Cross(PreviewGrid grid, int x1, int y1, int x2, int y2, float level, Span<Vector2> crossings, ref int count)
    {
        var h1 = grid.Height(x1, y1);
        var h2 = grid.Height(x2, y2);
        if (h1 > level == h2 > level)
            return;

        var t = (level - h1) / (h2 - h1);
        crossings[count++] = new Vector2(x1 + (x2 - x1) * t, y1 + (y2 - y1) * t);
    }

    private static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var length = ab.sqrMagnitude;
        var t = length > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / length) : 0f;
        return Vector2.Distance(point, a + ab * t);
    }
}
