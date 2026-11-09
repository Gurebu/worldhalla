using UnityEngine;

namespace Worldhalla.Plugin.UI.Preview.Styles;

// The detailed preview's look, beyond the palette. Tools/PaletteLab edits the same values on recorded world data
public sealed class HdrPreviewSettings
{
    public enum ToneMapper
    {
        Neutral,
        Aces,
        Clamp,
    }

    public float sunAzimuth = 275f;
    public float sunElevation = 65f;
    public float slopeExaggeration = 2.5f;
    public float shadowFill = 0.12f;

    // Exposure in stops at sea level and far above it, approached over heightScale metres
    public float lowStops = -0.6f;
    public float highStops = -0.2f;
    public float heightScale = 169.8f;

    public Color fogColor = PreviewPalette.Hex(0xc64600);
    public float fogAmount = 0f;
    public float fogHeight = 5.24f;

    public float exposure = 0.95f;
    public float saturation = 1f;
    public ToneMapper toneMapper = ToneMapper.Neutral;

    public Color sky = PreviewPalette.Hex(0x62a0ea);
    public float reflection = 0.05f;
    public Vector3 absorption = new(4.592f, 0.7612f, 0.4302f);
    public float abyssScale = 15.29f;

    public Color lavaColor = PreviewPalette.Hex(0xff4f00);
    public float lavaIntensity = 0.8f;

    // Widths in pixels, at most one: a sample only sees the grid cells around it
    public float coastline = 0.35f;
    public float coastlineWidth = 1f;
    public float isolines = 0.20f;
    public float isolineWidth = 1f;
    public float grid = 0.3f;
}
