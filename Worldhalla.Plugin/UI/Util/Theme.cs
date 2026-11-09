using UnityEngine;

namespace Worldhalla.Plugin.UI.Util;

public static class Theme
{
    public const float LABEL_HEIGHT = 26f;
    public const float FIELD_HEIGHT = 35f;
    public const float BUTTON_WIDTH = 136f;
    public const float BUTTON_HEIGHT = 45f;
    public const float ROW_GAP = 10f;
    public const float BUTTON_GAP = 8f;
    public const float RING_WIDTH = 3f;
    public const float SMALL_FONT_SCALE = 0.75f;
    public const float ERROR_FONT_SCALE = 0.8f;
    public const float OVERLAY_SCREEN_FRACTION = 0.95f;

    public static readonly Color Disc = new(0.05f, 0.07f, 0.08f, 0.92f);
    public static readonly Color Ring = new(0.64f, 0.48f, 0.21f, 1f);
    public static readonly Color Accent = new(0.94f, 0.70f, 0.29f, 1f);
    public static readonly Color AccentFaint = new(0.94f, 0.70f, 0.29f, 0.2f);
    public static readonly Color Overlay = new(0f, 0f, 0f, 0.6f);
    public static readonly Color Error = new(0.93f, 0.45f, 0.35f, 1f);
}
