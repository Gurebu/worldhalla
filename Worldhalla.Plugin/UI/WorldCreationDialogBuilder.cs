using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Worldhalla.Plugin.UI.Preview;

namespace Worldhalla.Plugin.UI;

public static class WorldCreationDialogBuilder
{
    private const string PANEL_NAME = "WorldhallaPanel";
    private const string DETAILS_NAME = "WorldhallaPreviewDetails";
    private const string SETTINGS_NAME = "WorldhallaSettings";

    private const float DESCRIPTION_HEIGHT = 56f;
    private const float DESCRIPTION_ROW = DESCRIPTION_HEIGHT + ROW_GAP;

    // The preview is square and as tall as column 1, so the panel grows as wide as column 1 grows tall
    private const float PANEL_WIDTH = 860f + DESCRIPTION_ROW;
    private const float PANEL_HEIGHT = 540f + DESCRIPTION_ROW;
    private const float CONTENT_TOP = 215f + DESCRIPTION_ROW / 2f;
    private const float COLUMN1_X = -220f - DESCRIPTION_ROW / 2f;
    private const float COLUMN1_WIDTH = 360f;
    private const float COLUMN2_RIGHT = 395f + DESCRIPTION_ROW / 2f;
    private const float LABEL_HEIGHT = 26f;
    private const float FIELD_HEIGHT = 35f;
    private const float SUMMARY_HEIGHT = 85f;
    private const float MAIN_BUTTON_HEIGHT = 45f;
    private const float ROW_GAP = 10f;
    private const float BUTTON_GAP = 8f;
    private const float CLOSE_BUTTON_WIDTH = 136f;
    private const float LOCATIONS_BUTTON_WIDTH = 200f;
    private const float EDIT_BUTTON_WIDTH = 80f;
    private const float ICON_BUTTON_SIZE = FIELD_HEIGHT;
    private const int OVERLAY_GROUP_PRIORITY = 2;
    private const float SELECTION_RING_WIDTH = 3f * RING_WIDTH;
    private const string GAMEPAD_BACK = "ButtonB";
    private const string GAMEPAD_CONFIRM = "ButtonX";
    private const string GAMEPAD_ACTION = "ButtonY";

    private const float DETAILS_MAP_SIZE = 1024f;
    private const float DETAILS_PADDING = 12f;
    private const float DETAILS_TITLE_HEIGHT = 30f;
    private const float DETAILS_WIDTH = DETAILS_MAP_SIZE + 2f * DETAILS_PADDING;
    private const float DETAILS_HEIGHT = DETAILS_PADDING + DETAILS_TITLE_HEIGHT + BUTTON_GAP + DETAILS_MAP_SIZE + BUTTON_GAP + MAIN_BUTTON_HEIGHT + DETAILS_PADDING;

    private const int SPINNER_SIZE = 64;
    private const float SPINNER_DISPLAY_SIZE = 32f;
    private const int PROGRESS_SIZE = 96;
    private const float PROGRESS_DISPLAY_SIZE = 48f;
    private const float PROGRESS_FONT_SCALE = 0.6f;
    private const float RING_WIDTH = 3f;

    private static readonly Color DiscColor = new(0.05f, 0.07f, 0.08f, 0.92f);
    private static readonly Color RingColor = new(0.64f, 0.48f, 0.21f, 1f);
    private static readonly Color SelectionColor = new(0.94f, 0.70f, 0.29f, 1f);
    private static readonly Color SpinnerColor = new(0.94f, 0.70f, 0.29f, 1f);
    private static readonly Color ProgressTrackColor = new(0.94f, 0.70f, 0.29f, 0.2f);
    private static readonly Color OverlayColor = new(0f, 0f, 0f, 0.6f);
    private static readonly Color ErrorColor = new(0.93f, 0.45f, 0.35f, 1f);
    private const float ERROR_FONT_SCALE = 0.8f;

    public static WorldCreationDialog Build(FejdStartup startup, VanillaCreateWorldPanel vanilla, GuiElementPalette palette)
    {
        var root = startup.m_createWorldPanel.transform;
        var nameField = startup.m_newWorldName;
        var seedField = startup.m_newWorldSeed;
        var done = startup.m_newWorldDone;
        var cancel = vanilla.Cancel;

        var panel = vanilla.Frame(root, PANEL_NAME);
        panel.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);
        vanilla.HideOriginal();

        var dialog = panel.gameObject.AddComponent<WorldCreationDialog>();
        dialog.startup = startup;
        dialog.palette = palette;
        dialog.nameField = nameField;
        dialog.seedField = seedField;
        dialog.doneButton = done;
        dialog.cancelButton = cancel;

        var y = CONTENT_TOP;

        PlaceLabel(palette.Label(panel, "name", vanilla.NameLabelKey).transform, ref y);
        if (vanilla.NameTip(panel) is { } nameTip)
        {
            Place(nameTip.transform, COLUMN1_X, y + LABEL_HEIGHT, COLUMN1_WIDTH, LABEL_HEIGHT);
            nameTip.horizontalAlignment = HorizontalAlignmentOptions.Right;
        }
        PlaceField(nameField.transform, panel, FIELD_HEIGHT, ref y);

        PlaceLabel(palette.Label(panel, "seed", vanilla.SeedLabelKey).transform, ref y);
        seedField.transform.SetParent(panel, false);
        var seedWidth = COLUMN1_WIDTH - ICON_BUTTON_SIZE - BUTTON_GAP;
        Place(seedField.transform, COLUMN1_X - (COLUMN1_WIDTH - seedWidth) / 2f, y, seedWidth, FIELD_HEIGHT);
        dialog.newSeedButton = palette.IconButton(panel, "NewSeed");
        Place(dialog.newSeedButton.transform, COLUMN1_X + (COLUMN1_WIDTH - ICON_BUTTON_SIZE) / 2f, y, ICON_BUTTON_SIZE, ICON_BUTTON_SIZE);
        y -= FIELD_HEIGHT + ROW_GAP;

        PlaceLabel(palette.Label(panel, "generator", "$wh_generator").transform, ref y);
        dialog.generatorDropdown = palette.Dropdown(panel, "GeneratorDropdown");
        var dropdownRect = (RectTransform)dialog.generatorDropdown.transform;
        PlaceField(dropdownRect, panel, dropdownRect.sizeDelta.y, ref y);
        dialog.generatorDescription = NewDescription(palette, panel, ref y);

        dialog.worldSettingsButton = NewEditableLabel(palette, panel, "worldSettings", "$wh_world_settings", ref y);
        dialog.worldSettingsSummary = NewSummary(palette, panel, "WorldSettingsSummary", ref y);

        dialog.generatorSettingsButton = NewEditableLabel(palette, panel, "generatorSettings", "$wh_generator_settings", ref y);
        dialog.generatorSettingsSummary = NewSummary(palette, panel, "GeneratorSettingsSummary", ref y);
        var contentBottom = y + ROW_GAP;

        // Column 2 ends level with column 1, and the preview takes whatever height its buttons leave over
        const float controlsHeight = ROW_GAP + MAIN_BUTTON_HEIGHT;
        var previewSize = Mathf.FloorToInt(CONTENT_TOP - contentBottom - controlsHeight);
        var column2Width = (float)previewSize;
        var column2X = COLUMN2_RIGHT - column2Width / 2f;

        y = CONTENT_TOP;
        BuildPreview(dialog, panel, palette, column2X, previewSize, ref y);

        var previewBottom = y + ROW_GAP;
        dialog.hideButton = palette.IconButton(panel, "HidePreview", Icons.CrossedEye);
        Place(dialog.hideButton.transform, column2X - (column2Width - ICON_BUTTON_SIZE) / 2f, previewBottom + ICON_BUTTON_SIZE,
            ICON_BUTTON_SIZE, ICON_BUTTON_SIZE);
        dialog.detailsButton = palette.IconButton(panel, "PreviewDetails", Icons.Magnifier);
        Place(dialog.detailsButton.transform, column2X + (column2Width - ICON_BUTTON_SIZE) / 2f, previewBottom + ICON_BUTTON_SIZE,
            ICON_BUTTON_SIZE, ICON_BUTTON_SIZE);

        var halfWidth = (column2Width - BUTTON_GAP) / 2f;
        var halfOffset = (halfWidth + BUTTON_GAP) / 2f;

        done.transform.SetParent(panel, false);
        cancel.transform.SetParent(panel, false);
        Place(done.transform, column2X - halfOffset, y, halfWidth, MAIN_BUTTON_HEIGHT);
        Place(cancel.transform, column2X + halfOffset, y, halfWidth, MAIN_BUTTON_HEIGHT);

        if (panel.GetComponent<UIGroupHandler>() is { } group)
            group.m_defaultElement = nameField.gameObject;

        BuildDetails(dialog, root, palette);
        dialog.settingsDialog = BuildSettingsDialog(root, palette);

        dialog.Initialize();
        return dialog;
    }

    private static void BuildPreview(WorldCreationDialog dialog, RectTransform panel, GuiElementPalette palette, float x, int size, ref float y)
    {
        dialog.previewSize = size;
        dialog.discTexture = Shapes.Disc(size, DiscColor);

        var preview = NewRawImage(panel, "Preview", dialog.discTexture, true);
        Place(preview.transform, x, y, size, size);
        y -= size + ROW_GAP;

        dialog.previewImage = preview;
        dialog.previewButton = preview.gameObject.AddComponent<Button>();

        var ring = NewRawImage(preview.transform, "Ring", Shapes.Ring(size, RING_WIDTH, RingColor), false);
        Stretch(ring.rectTransform);

        var selection = NewRawImage(preview.transform, "Selection", Shapes.Ring(size, SELECTION_RING_WIDTH, SelectionColor), false);
        Stretch(selection.rectTransform);
        dialog.previewButton.targetGraphic = selection;
        dialog.previewButton.colors = ShownWhenSelected(dialog.previewButton.colors);

        var spinner = NewRawImage(preview.transform, "Spinner", Shapes.Arc(SPINNER_SIZE, 4f, 270f, SpinnerColor), false);
        spinner.rectTransform.sizeDelta = new Vector2(SPINNER_DISPLAY_SIZE, SPINNER_DISPLAY_SIZE);
        spinner.rectTransform.anchoredPosition = new Vector2(0f, SPINNER_DISPLAY_SIZE);
        dialog.spinner = spinner.rectTransform;

        BuildProgress(dialog, preview.transform, palette);

        var label = palette.Label(preview.transform, "Label", "");
        label.horizontalAlignment = HorizontalAlignmentOptions.Center;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.raycastTarget = false;
        Stretch(label.rectTransform);
        dialog.previewLabel = label;
    }

    private static void BuildProgress(WorldCreationDialog dialog, Transform parent, GuiElementPalette palette)
    {
        var progress = new GameObject("Progress", typeof(RectTransform));
        progress.transform.SetParent(parent, false);
        var rect = (RectTransform)progress.transform;
        rect.sizeDelta = new Vector2(PROGRESS_DISPLAY_SIZE, PROGRESS_DISPLAY_SIZE);
        rect.anchoredPosition = new Vector2(0f, PROGRESS_DISPLAY_SIZE);
        progress.SetActive(false);
        dialog.progress = progress;

        var track = NewRawImage(rect, "Track", Shapes.Ring(PROGRESS_SIZE, 6f, ProgressTrackColor), false);
        Stretch(track.rectTransform);

        var fillTexture = Shapes.Ring(PROGRESS_SIZE, 6f, SpinnerColor);
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        fill.transform.SetParent(rect, false);
        fill.sprite = Sprite.Create(fillTexture, new Rect(0f, 0f, PROGRESS_SIZE, PROGRESS_SIZE), new Vector2(0.5f, 0.5f));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = true;
        fill.fillAmount = 0f;
        fill.raycastTarget = false;
        Stretch(fill.rectTransform);
        dialog.progressFill = fill;

        var percent = palette.Label(rect, "Percent", "");
        percent.enableAutoSizing = false;
        percent.fontSize *= PROGRESS_FONT_SCALE;
        percent.horizontalAlignment = HorizontalAlignmentOptions.Center;
        percent.verticalAlignment = VerticalAlignmentOptions.Middle;
        percent.raycastTarget = false;
        Stretch(percent.rectTransform);
        dialog.progressPercent = percent;
    }

    private static void BuildDetails(WorldCreationDialog dialog, Transform root, GuiElementPalette palette)
    {
        var overlay = new GameObject(DETAILS_NAME, typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(root, false);
        Stretch((RectTransform)overlay.transform);
        overlay.GetComponent<Image>().color = OverlayColor;

        var frame = new GameObject("Frame", typeof(RectTransform));
        var frameRect = (RectTransform)frame.transform;
        frameRect.SetParent(overlay.transform, false);
        frameRect.sizeDelta = new Vector2(DETAILS_WIDTH, DETAILS_HEIGHT);

        palette.Background(frameRect);

        var y = DETAILS_HEIGHT / 2f - DETAILS_PADDING;

        var title = palette.Label(frameRect, "topic", "$wh_preview_title");
        title.horizontalAlignment = HorizontalAlignmentOptions.Center;
        Place(title.transform, 0f, y, DETAILS_MAP_SIZE, DETAILS_TITLE_HEIGHT);
        y -= DETAILS_TITLE_HEIGHT + BUTTON_GAP;

        var map = new GameObject("Map", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        map.transform.SetParent(frameRect, false);
        map.GetComponent<Image>().color = Color.clear;
        Place(map.transform, 0f, y, DETAILS_MAP_SIZE, DETAILS_MAP_SIZE);
        var whole = NewRawImage(map.transform, "Whole", Texture2D.blackTexture, false);
        Stretch(whole.rectTransform);
        var view = NewRawImage(map.transform, "View", Shapes.Disc(PreviewView.SIZE, DiscColor), false);
        Stretch(view.rectTransform);
        var ring = NewRawImage(map.transform, "Ring", Shapes.Ring(PreviewView.SIZE, RING_WIDTH * 2f, RingColor), false);
        Stretch(ring.rectTransform);
        y -= DETAILS_MAP_SIZE + BUTTON_GAP;

        var close = palette.Button(frameRect, "Close", "$wh_preview_close", GAMEPAD_BACK);
        var locations = palette.Button(frameRect, "Locations", "$wh_preview_locations", GAMEPAD_ACTION);
        close.navigation = locations.navigation = new Navigation { mode = Navigation.Mode.None };
        dialog.detailsGroup = NewOverlayGroup(overlay);
        Place(locations.transform, -(LOCATIONS_BUTTON_WIDTH + BUTTON_GAP) / 2f, y, LOCATIONS_BUTTON_WIDTH, MAIN_BUTTON_HEIGHT);
        Place(close.transform, (CLOSE_BUTTON_WIDTH + BUTTON_GAP) / 2f, y, CLOSE_BUTTON_WIDTH, MAIN_BUTTON_HEIGHT);

        var note = palette.Label(frameRect, "LocationsNote", "$wh_preview_locations_lpa");
        note.horizontalAlignment = HorizontalAlignmentOptions.Right;
        note.verticalAlignment = VerticalAlignmentOptions.Middle;
        note.textWrappingMode = TextWrappingModes.Normal;
        note.enableAutoSizing = false;
        note.fontSize *= GuiElementPalette.SMALL_FONT_SCALE;
        note.raycastTarget = false;
        var noteRight = -(LOCATIONS_BUTTON_WIDTH + BUTTON_GAP) / 2f - LOCATIONS_BUTTON_WIDTH / 2f - BUTTON_GAP;
        var noteWidth = DETAILS_MAP_SIZE / 2f + noteRight;
        Place(note.transform, noteRight - noteWidth / 2f, y, noteWidth, MAIN_BUTTON_HEIGHT);

        dialog.detailsOverlay = overlay;
        dialog.detailsFrame = frameRect;
        dialog.detailsImage = view;
        dialog.detailsWholeImage = whole;
        dialog.detailsInput = map.AddComponent<DragAndScroll>();
        dialog.detailsRing = ring.gameObject;
        dialog.detailsCloseButton = close;
        dialog.locationsButton = locations;
        dialog.locationsNote = note.gameObject;

        overlay.SetActive(false);
    }

    private static SettingsDialog BuildSettingsDialog(Transform root, GuiElementPalette palette)
    {
        var overlay = new GameObject(SETTINGS_NAME, typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(root, false);
        Stretch((RectTransform)overlay.transform);
        overlay.GetComponent<Image>().color = OverlayColor;

        var frame = (RectTransform)new GameObject("Frame", typeof(RectTransform)).transform;
        frame.SetParent(overlay.transform, false);

        palette.Background(frame);

        var rows = (RectTransform)new GameObject("Rows", typeof(RectTransform)).transform;
        rows.SetParent(frame, false);
        Stretch(rows);

        var title = palette.Label(frame, "topic", "");
        title.horizontalAlignment = HorizontalAlignmentOptions.Center;

        var error = palette.Label(frame, "error", "");
        error.horizontalAlignment = HorizontalAlignmentOptions.Center;
        error.verticalAlignment = VerticalAlignmentOptions.Middle;
        error.textWrappingMode = TextWrappingModes.Normal;
        error.color = ErrorColor;
        error.fontSize *= ERROR_FONT_SCALE;

        var rowLabel = palette.Label(frame, "LabelTemplate", "");
        rowLabel.horizontalAlignment = HorizontalAlignmentOptions.Left;
        rowLabel.gameObject.SetActive(false);

        var dropdown = palette.Dropdown(frame, "DropdownTemplate");
        dropdown.gameObject.SetActive(false);

        var textField = palette.TextField(frame, "TextField");
        textField.gameObject.SetActive(false);

        var settings = overlay.AddComponent<SettingsDialog>();
        settings.frame = frame;
        settings.title = title;
        settings.rows = rows;
        settings.error = error;
        settings.okButton = palette.Button(frame, "Ok", "$wh_settings_ok", GAMEPAD_CONFIRM);
        settings.cancelButton = palette.Button(frame, "Cancel", "$wh_settings_cancel", GAMEPAD_BACK);
        settings.group = NewOverlayGroup(overlay);
        settings.labelTemplate = rowLabel;
        settings.dropdownTemplate = dropdown;
        settings.textFieldTemplate = textField;
        settings.Initialize();
        return settings;
    }

    private static void PlaceLabel(Transform label, ref float y)
    {
        Place(label, COLUMN1_X, y, COLUMN1_WIDTH, LABEL_HEIGHT);
        label.GetComponent<TMP_Text>().horizontalAlignment = HorizontalAlignmentOptions.Left;
        y -= LABEL_HEIGHT;
    }

    // Created after its label so it draws above it and gets the clicks the full-width label would otherwise take
    private static Button NewEditableLabel(GuiElementPalette palette, RectTransform panel, string name, string text, ref float y)
    {
        var top = y;
        var label = palette.Label(panel, name, text);
        PlaceLabel(label.transform, ref y);

        var button = palette.Button(panel, name + "Edit", "$wh_edit");
        Place(button.transform, COLUMN1_X + (COLUMN1_WIDTH - EDIT_BUTTON_WIDTH) / 2f, top, EDIT_BUTTON_WIDTH, LABEL_HEIGHT);

        var buttonText = button.GetComponentInChildren<TMP_Text>(true);
        Stretch(buttonText.rectTransform);
        buttonText.margin = Vector4.zero;
        buttonText.enableAutoSizing = false;
        buttonText.fontSize = label.fontSize;
        buttonText.textWrappingMode = TextWrappingModes.NoWrap;
        buttonText.overflowMode = TextOverflowModes.Overflow;
        buttonText.verticalAlignment = VerticalAlignmentOptions.Middle;
        return button;
    }

    private static void PlaceField(Transform field, RectTransform panel, float height, ref float y)
    {
        field.SetParent(panel, false);
        Place(field, COLUMN1_X, y, COLUMN1_WIDTH, height);
        y -= height + ROW_GAP;
    }

    private static RectTransform Place(Transform transform, float x, float top, float width, float height)
    {
        var rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, top);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static RawImage NewRawImage(Transform parent, string name, Texture texture, bool raycastTarget)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        gameObject.transform.SetParent(parent, false);

        var image = gameObject.GetComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private static ColorBlock ShownWhenSelected(ColorBlock colors)
    {
        colors.normalColor = colors.disabledColor = Color.clear;
        colors.highlightedColor = colors.selectedColor = colors.pressedColor = Color.white;
        colors.colorMultiplier = 1f;
        return colors;
    }

    // Outranks the create-world panel's group, which then turns off its hotkeys and stops taking focus
    private static UIGroupHandler NewOverlayGroup(GameObject overlay)
    {
        overlay.AddComponent<CanvasGroup>();
        var group = overlay.AddComponent<UIGroupHandler>();
        group.m_groupPriority = OVERLAY_GROUP_PRIORITY;
        return group;
    }

    private static TMP_Text NewSummary(GuiElementPalette palette, RectTransform panel, string name, ref float y)
    {
        var text = palette.TextBox(panel, name);
        PlaceBox(panel, text, SUMMARY_HEIGHT, ref y);
        return text;
    }

    private static ScrollRect NewDescription(GuiElementPalette palette, RectTransform panel, ref float y)
    {
        var scroll = palette.ScrollingTextBox(panel, "GeneratorDescription");
        var text = scroll.content.GetComponent<TMP_Text>();
        text.richText = false;
        PlaceBox(panel, text, DESCRIPTION_HEIGHT, ref y);
        return scroll;
    }

    private static void PlaceBox(Transform panel, Component content, float height, ref float y)
    {
        var box = content.transform;
        while (box.parent != panel)
            box = box.parent;

        Place(box, COLUMN1_X, y, COLUMN1_WIDTH, height);
        y -= height + ROW_GAP;
    }
}
