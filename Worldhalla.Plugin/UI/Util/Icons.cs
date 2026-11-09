using UnityEngine;

namespace Worldhalla.Plugin.UI.Util;

// Drawn to sit beside the game's own button icons (refresh_icon): 20 px, solid white, strokes about a sixth of the size
public static class Icons
{
    private const int SIZE = 20;
    private const int SUBSAMPLES = 6;

    private const float LENS_X = 8f;
    private const float LENS_Y = 8f;
    private const float LENS_RADIUS = 5.6f;
    private const float LENS_WIDTH = 2.9f;
    private const float HANDLE_HALF_WIDTH = 2.05f;
    private const float HANDLE_END = 17.3f;

    private const float EYE_CENTER = 10f;
    private const float EYE_HALF_WIDTH = 9.2f;
    private const float EYE_HALF_HEIGHT = 5.4f;
    private const float EYE_OUTLINE_WIDTH = 2.3f;
    private const float PUPIL_RADIUS = 4f;
    private const float SLASH_WIDTH = 2.4f;
    private const float SLASH_GAP = 1.2f;
    private static readonly Vector2 SlashStart = new(3f, 3f);
    private static readonly Vector2 SlashEnd = new(17f, 17f);

    private static Sprite? magnifier;
    private static Sprite? crossedEye;

    public static Sprite Magnifier => magnifier ??= Draw(MagnifierCovers);

    public static Sprite CrossedEye => crossedEye ??= Draw(CrossedEyeCovers);

    // Coordinates are in pixels from the top left, as the icon is drawn
    private static bool MagnifierCovers(float x, float y)
    {
        var fromLens = new Vector2(x - LENS_X, y - LENS_Y).magnitude;
        if (Mathf.Abs(fromLens - LENS_RADIUS) <= LENS_WIDTH / 2f)
            return true;

        var end = new Vector2(HANDLE_END, HANDLE_END);
        var direction = (end - new Vector2(LENS_X, LENS_Y)).normalized;
        var start = new Vector2(LENS_X, LENS_Y) + direction * (LENS_RADIUS + LENS_WIDTH / 2f - 0.3f);
        return DistanceToSegment(new Vector2(x, y), start, end) <= HANDLE_HALF_WIDTH;
    }

    // The slash cuts a gap into the eye on one side only, which keeps more of the pupil at this size
    private static bool CrossedEyeCovers(float x, float y)
    {
        var point = new Vector2(x, y);
        var toSlash = DistanceToSegment(point, SlashStart, SlashEnd);
        if (toSlash <= SLASH_WIDTH / 2f)
            return true;

        var slash = SlashEnd - SlashStart;
        var side = slash.x * (y - SlashStart.y) - slash.y * (x - SlashStart.x);
        if (toSlash <= SLASH_WIDTH / 2f + SLASH_GAP && side < 0f)
            return false;

        var outline = InsideEye(point, EYE_OUTLINE_WIDTH / 2f) && !InsideEye(point, -EYE_OUTLINE_WIDTH / 2f);
        return outline || (point - new Vector2(EYE_CENTER, EYE_CENTER)).magnitude <= PUPIL_RADIUS;
    }

    // The eye is the lens shape where two circles overlap, meeting at its corners and grown or shrunk by margin
    private static bool InsideEye(Vector2 point, float margin)
    {
        var offset = (EYE_HALF_WIDTH * EYE_HALF_WIDTH - EYE_HALF_HEIGHT * EYE_HALF_HEIGHT) / (2f * EYE_HALF_HEIGHT);
        var radius = offset + EYE_HALF_HEIGHT + margin;
        return (point - new Vector2(EYE_CENTER, EYE_CENTER + offset)).magnitude <= radius
               && (point - new Vector2(EYE_CENTER, EYE_CENTER - offset)).magnitude <= radius;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);
        return (point - (start + t * segment)).magnitude;
    }

    private static Sprite Draw(Func<float, float, bool> covers)
    {
        var texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var pixels = new Color32[SIZE * SIZE];
        for (var row = 0; row < SIZE; row++)
        for (var column = 0; column < SIZE; column++)
        {
            var covered = 0;
            for (var sy = 0; sy < SUBSAMPLES; sy++)
            for (var sx = 0; sx < SUBSAMPLES; sx++)
            {
                var x = column + (sx + 0.5f) / SUBSAMPLES;
                var y = SIZE - row - 1 + (sy + 0.5f) / SUBSAMPLES;
                if (covers(x, y))
                    covered++;
            }

            pixels[row * SIZE + column] = new Color32(255, 255, 255, (byte)(255 * covered / (SUBSAMPLES * SUBSAMPLES)));
        }

        texture.SetPixels32(pixels);
        texture.Apply(false);
        return Sprite.Create(texture, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f));
    }
}
