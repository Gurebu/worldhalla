using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Worldhalla.Plugin.UI.Preview;
using Worldhalla.Plugin.UI.Util;

namespace Worldhalla.Plugin.UI.CreateWorld;

public static class DetailsOverlayBuilder
{
    private const string NAME = "WorldhallaPreviewDetails";

    private const float MAP_SIZE = 1024f;
    private const float PADDING = 12f;
    private const float TITLE_HEIGHT = 30f;
    private const float WIDTH = MAP_SIZE + 2f * PADDING;
    private const float HEIGHT = PADDING + TITLE_HEIGHT + Theme.BUTTON_GAP + MAP_SIZE + Theme.BUTTON_GAP + Theme.BUTTON_HEIGHT + PADDING;
    private const float LOCATIONS_BUTTON_WIDTH = 200f;

    // locationsHost stays active while the overlay is closed, so a running simulation survives closing it
    public static DetailsOverlay Build(GuiElementPalette palette, Transform root, GameObject locationsHost)
    {
        var overlay = palette.Overlay(root, NAME);
        var frame = palette.Frame(overlay);
        frame.sizeDelta = new Vector2(WIDTH, HEIGHT);

        var y = HEIGHT / 2f - PADDING;

        var title = palette.Label(frame, "topic", "$wh_preview_title");
        title.horizontalAlignment = HorizontalAlignmentOptions.Center;
        Rects.Place(title.transform, 0f, y, MAP_SIZE, TITLE_HEIGHT);
        y -= TITLE_HEIGHT + Theme.BUTTON_GAP;

        var map = new GameObject("Map", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        map.transform.SetParent(frame, false);
        map.GetComponent<Image>().color = Color.clear;
        Rects.Place(map.transform, 0f, y, MAP_SIZE, MAP_SIZE);
        var whole = GuiElementPalette.RawImage(map.transform, "Whole", Texture2D.blackTexture);
        Rects.Stretch(whole.rectTransform);
        var view = GuiElementPalette.RawImage(map.transform, "View", Shapes.Disc(PreviewView.SIZE, Theme.Disc));
        Rects.Stretch(view.rectTransform);
        var ring = GuiElementPalette.RawImage(map.transform, "Ring", Shapes.Ring(PreviewView.SIZE, Theme.RING_WIDTH * 2f, Theme.Ring));
        Rects.Stretch(ring.rectTransform);
        y -= MAP_SIZE + Theme.BUTTON_GAP;

        var close = palette.Button(frame, "Close", "$wh_preview_close", GuiElementPalette.GAMEPAD_BACK);
        var locationsButton = palette.Button(frame, "Locations", "$wh_preview_locations", GuiElementPalette.GAMEPAD_ACTION);
        close.navigation = locationsButton.navigation = new Navigation { mode = Navigation.Mode.None };
        var group = GuiElementPalette.OverlayGroup(overlay);
        Rects.Place(locationsButton.transform, -(LOCATIONS_BUTTON_WIDTH + Theme.BUTTON_GAP) / 2f, y, LOCATIONS_BUTTON_WIDTH, Theme.BUTTON_HEIGHT);
        Rects.Place(close.transform, (Theme.BUTTON_WIDTH + Theme.BUTTON_GAP) / 2f, y, Theme.BUTTON_WIDTH, Theme.BUTTON_HEIGHT);

        var note = palette.Label(frame, "LocationsNote", "$wh_preview_locations_lpa");
        note.horizontalAlignment = HorizontalAlignmentOptions.Right;
        note.verticalAlignment = VerticalAlignmentOptions.Middle;
        note.textWrappingMode = TextWrappingModes.Normal;
        note.enableAutoSizing = false;
        note.fontSize *= Theme.SMALL_FONT_SCALE;
        note.raycastTarget = false;
        var noteRight = -(LOCATIONS_BUTTON_WIDTH + Theme.BUTTON_GAP) / 2f - LOCATIONS_BUTTON_WIDTH / 2f - Theme.BUTTON_GAP;
        var noteWidth = MAP_SIZE / 2f + noteRight;
        Rects.Place(note.transform, noteRight - noteWidth / 2f, y, noteWidth, Theme.BUTTON_HEIGHT);

        var locations = locationsHost.AddComponent<LocationsLayer>();
        locations.iconParent = (RectTransform)map.transform;
        locations.button = locationsButton;
        locations.note = note.gameObject;
        locations.palette = palette;

        var details = overlay.AddComponent<DetailsOverlay>();
        details.frame = frame;
        details.image = view;
        details.wholeImage = whole;
        details.input = map.AddComponent<DragAndScroll>();
        details.group = group;
        details.ring = ring.gameObject;
        details.closeButton = close;
        details.locations = locations;
        details.Initialize();
        return details;
    }
}
