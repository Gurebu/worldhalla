using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Worldhalla.Plugin.UI.Util;

namespace Worldhalla.Plugin.UI.CreateWorld;

public static class PreviewTileBuilder
{
    private const float ICON_BUTTON_SIZE = Theme.FIELD_HEIGHT;
    private const float SELECTION_RING_WIDTH = 3f * Theme.RING_WIDTH;

    private const int SPINNER_SIZE = 64;
    private const float SPINNER_DISPLAY_SIZE = 32f;
    private const int PROGRESS_SIZE = 96;
    private const float PROGRESS_DISPLAY_SIZE = 48f;
    private const float PROGRESS_FONT_SCALE = 0.6f;

    // Its two buttons overlap the tile's bottom corners, so y ends up one gap below the tile
    public static PreviewTile Build(GuiElementPalette palette, RectTransform panel, float x, int size, ref float y)
    {
        var tile = panel.gameObject.AddComponent<PreviewTile>();
        tile.size = size;
        tile.discTexture = Shapes.Disc(size, Theme.Disc);

        var preview = GuiElementPalette.RawImage(panel, "Preview", tile.discTexture, true);
        Rects.Place(preview.transform, x, y, size, size);
        y -= size + Theme.ROW_GAP;

        tile.image = preview;
        tile.button = preview.gameObject.AddComponent<Button>();

        var ring = GuiElementPalette.RawImage(preview.transform, "Ring", Shapes.Ring(size, Theme.RING_WIDTH, Theme.Ring));
        Rects.Stretch(ring.rectTransform);

        var selection = GuiElementPalette.RawImage(preview.transform, "Selection", Shapes.Ring(size, SELECTION_RING_WIDTH, Theme.Accent));
        Rects.Stretch(selection.rectTransform);
        tile.button.targetGraphic = selection;
        tile.button.colors = GuiElementPalette.ShownWhenSelected(tile.button.colors);

        var spinner = GuiElementPalette.RawImage(preview.transform, "Spinner", Shapes.Arc(SPINNER_SIZE, 4f, 270f, Theme.Accent));
        spinner.rectTransform.sizeDelta = new Vector2(SPINNER_DISPLAY_SIZE, SPINNER_DISPLAY_SIZE);
        spinner.rectTransform.anchoredPosition = new Vector2(0f, SPINNER_DISPLAY_SIZE);
        tile.spinner = spinner.rectTransform;

        BuildProgress(tile, palette, preview.transform);

        var label = palette.Label(preview.transform, "Label", "");
        label.horizontalAlignment = HorizontalAlignmentOptions.Center;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.raycastTarget = false;
        Rects.Stretch(label.rectTransform);
        tile.label = label;

        var buttonsTop = y + Theme.ROW_GAP + ICON_BUTTON_SIZE;
        var buttonOffset = (size - ICON_BUTTON_SIZE) / 2f;
        tile.hideButton = palette.IconButton(panel, "HidePreview", Icons.CrossedEye);
        Rects.Place(tile.hideButton.transform, x - buttonOffset, buttonsTop, ICON_BUTTON_SIZE, ICON_BUTTON_SIZE);
        tile.detailsButton = palette.IconButton(panel, "PreviewDetails", Icons.Magnifier);
        Rects.Place(tile.detailsButton.transform, x + buttonOffset, buttonsTop, ICON_BUTTON_SIZE, ICON_BUTTON_SIZE);

        tile.Initialize();
        return tile;
    }

    private static void BuildProgress(PreviewTile tile, GuiElementPalette palette, Transform parent)
    {
        var progress = new GameObject("Progress", typeof(RectTransform));
        progress.transform.SetParent(parent, false);
        var rect = (RectTransform)progress.transform;
        rect.sizeDelta = new Vector2(PROGRESS_DISPLAY_SIZE, PROGRESS_DISPLAY_SIZE);
        rect.anchoredPosition = new Vector2(0f, PROGRESS_DISPLAY_SIZE);
        progress.SetActive(false);
        tile.progress = progress;

        var track = GuiElementPalette.RawImage(rect, "Track", Shapes.Ring(PROGRESS_SIZE, 6f, Theme.AccentFaint));
        Rects.Stretch(track.rectTransform);

        var fillTexture = Shapes.Ring(PROGRESS_SIZE, 6f, Theme.Accent);
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        fill.transform.SetParent(rect, false);
        fill.sprite = Sprite.Create(fillTexture, new Rect(0f, 0f, PROGRESS_SIZE, PROGRESS_SIZE), new Vector2(0.5f, 0.5f));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = true;
        fill.fillAmount = 0f;
        fill.raycastTarget = false;
        Rects.Stretch(fill.rectTransform);
        tile.progressFill = fill;

        var percent = palette.Label(rect, "Percent", "");
        percent.enableAutoSizing = false;
        percent.fontSize *= PROGRESS_FONT_SCALE;
        percent.horizontalAlignment = HorizontalAlignmentOptions.Center;
        percent.verticalAlignment = VerticalAlignmentOptions.Middle;
        percent.raycastTarget = false;
        Rects.Stretch(percent.rectTransform);
        tile.progressPercent = percent;
    }
}
