using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Worldhalla.Plugin.UI.Preview;
using Object = UnityEngine.Object;

namespace Worldhalla.Plugin.UI;

internal static class WorldCreationDialogBuilder
{
    private const string PANEL_NAME = "WorldhallaPanel";
    private const string DETAILS_NAME = "WorldhallaPreviewDetails";
    private const string SETTINGS_NAME = "WorldhallaSettings";

    private const float PANEL_WIDTH = 860f;
    private const float PANEL_HEIGHT = 540f;
    private const float CONTENT_TOP = 215f;
    private const float COLUMN1_X = -220f;
    private const float COLUMN1_WIDTH = 360f;
    private const float COLUMN2_RIGHT = 395f;
    private const float LABEL_HEIGHT = 26f;
    private const float FIELD_HEIGHT = 35f;
    private const float SUMMARY_HEIGHT = 85f;
    private const float MAIN_BUTTON_HEIGHT = 45f;
    private const float ROW_GAP = 10f;
    private const float BUTTON_GAP = 8f;
    private const float SUMMARY_FONT_SCALE = 0.75f;
    private const float CLOSE_BUTTON_WIDTH = 136f;
    private const float EDIT_BUTTON_WIDTH = 80f;
    private const float ICON_BUTTON_SIZE = FIELD_HEIGHT;
    private const string REFRESH_ICON_CHILD = "Image";

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
    private static readonly Color SpinnerColor = new(0.94f, 0.70f, 0.29f, 1f);
    private static readonly Color ProgressTrackColor = new(0.94f, 0.70f, 0.29f, 0.2f);
    private static readonly Color OverlayColor = new(0f, 0f, 0f, 0.6f);
    private static readonly Color ErrorColor = new(0.93f, 0.45f, 0.35f, 1f);
    private const float ERROR_FONT_SCALE = 0.8f;

    public static WorldCreationDialog Build(FejdStartup startup)
    {
        var root = startup.m_createWorldPanel.transform;
        var vanillaPanel = (RectTransform)startup.m_newWorldName.transform.parent;
        var nameField = startup.m_newWorldName;
        var seedField = startup.m_newWorldSeed;
        var done = startup.m_newWorldDone;
        var cancel = vanillaPanel.Find("Cancel").GetComponent<Button>();

        var panel = (RectTransform)Object.Instantiate(vanillaPanel.gameObject, root).transform;
        panel.name = PANEL_NAME;
        panel.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);
        vanillaPanel.gameObject.SetActive(false);

        foreach (var cloned in new[] { nameField.name, seedField.name, done.name, cancel.name, "GenerateSeed" })
            DestroyChild(panel, cloned);

        var labelTemplate = panel.Find("seed").GetComponent<TMP_Text>();

        var dialog = panel.gameObject.AddComponent<WorldCreationDialog>();
        dialog.startup = startup;
        dialog.nameField = nameField;
        dialog.seedField = seedField;

        var y = CONTENT_TOP;

        PlaceLabel(panel.Find("name"), ref y);
        if (panel.Find("name_tip") is RectTransform nameTip)
        {
            Place(nameTip, COLUMN1_X, y + LABEL_HEIGHT, COLUMN1_WIDTH, LABEL_HEIGHT);
            nameTip.GetComponent<TMP_Text>().horizontalAlignment = HorizontalAlignmentOptions.Right;
        }
        PlaceField(nameField.transform, panel, FIELD_HEIGHT, ref y);

        PlaceLabel(panel.Find("seed"), ref y);
        seedField.transform.SetParent(panel, false);
        var seedWidth = COLUMN1_WIDTH - ICON_BUTTON_SIZE - BUTTON_GAP;
        Place(seedField.transform, COLUMN1_X - (COLUMN1_WIDTH - seedWidth) / 2f, y, seedWidth, FIELD_HEIGHT);
        dialog.newSeedButton = NewButton(GetRefreshButton(startup), panel, "NewSeed", null, 1f);
        Place(dialog.newSeedButton.transform, COLUMN1_X + (COLUMN1_WIDTH - ICON_BUTTON_SIZE) / 2f, y, ICON_BUTTON_SIZE, ICON_BUTTON_SIZE);
        y -= FIELD_HEIGHT + ROW_GAP;

        PlaceLabel(NewLabel(labelTemplate, panel, "generator", "Generator").transform, ref y);
        dialog.generatorDropdown = NewDropdown(startup, panel);
        var dropdownRect = (RectTransform)dialog.generatorDropdown.transform;
        PlaceField(dropdownRect, panel, dropdownRect.sizeDelta.y, ref y);

        dialog.worldSettingsButton = NewEditableLabel(labelTemplate, cancel, panel, "worldSettings", "World settings", ref y);
        dialog.worldSettingsSummary = NewSummary(nameField, panel, "WorldSettingsSummary", ref y);

        dialog.generatorSettingsButton = NewEditableLabel(labelTemplate, cancel, panel, "generatorSettings", "Generator settings", ref y);
        dialog.generatorSettingsSummary = NewSummary(nameField, panel, "GeneratorSettingsSummary", ref y);
        var contentBottom = y + ROW_GAP;

        // Column 2 ends level with column 1, and the preview takes whatever height its buttons leave over
        const float controlsHeight = ROW_GAP + MAIN_BUTTON_HEIGHT;
        var previewSize = Mathf.FloorToInt(CONTENT_TOP - contentBottom - controlsHeight);
        var column2Width = (float)previewSize;
        var column2X = COLUMN2_RIGHT - column2Width / 2f;

        y = CONTENT_TOP;
        BuildPreview(dialog, panel, labelTemplate, column2X, previewSize, ref y);

        var previewBottom = y + ROW_GAP;
        dialog.hideButton = NewIconButton(startup, panel, "HidePreview", Icons.CrossedEye);
        Place(dialog.hideButton.transform, column2X - (column2Width - ICON_BUTTON_SIZE) / 2f, previewBottom + ICON_BUTTON_SIZE,
            ICON_BUTTON_SIZE, ICON_BUTTON_SIZE);
        dialog.detailsButton = NewIconButton(startup, panel, "PreviewDetails", Icons.Magnifier);
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

        BuildDetails(dialog, root, panel, labelTemplate, cancel);
        dialog.settingsDialog = BuildSettingsDialog(startup, root, panel, labelTemplate, cancel);

        dialog.Initialize();
        return dialog;
    }

    private static void BuildPreview(WorldCreationDialog dialog, RectTransform panel, TMP_Text labelTemplate, float x, int size, ref float y)
    {
        dialog.previewSize = size;
        dialog.discTexture = Shapes.Disc(size, DiscColor);

        var preview = NewRawImage(panel, "Preview", dialog.discTexture, true);
        Place(preview.transform, x, y, size, size);
        y -= size + ROW_GAP;

        dialog.previewImage = preview;
        dialog.previewButton = preview.gameObject.AddComponent<Button>();
        dialog.previewButton.targetGraphic = preview;
        dialog.previewButton.transition = Selectable.Transition.None;

        var ring = NewRawImage(preview.transform, "Ring", Shapes.Ring(size, RING_WIDTH, RingColor), false);
        Stretch(ring.rectTransform);

        var spinner = NewRawImage(preview.transform, "Spinner", Shapes.Arc(SPINNER_SIZE, 4f, 270f, SpinnerColor), false);
        spinner.rectTransform.sizeDelta = new Vector2(SPINNER_DISPLAY_SIZE, SPINNER_DISPLAY_SIZE);
        spinner.rectTransform.anchoredPosition = new Vector2(0f, SPINNER_DISPLAY_SIZE);
        dialog.spinner = spinner.rectTransform;

        BuildProgress(dialog, preview.transform, labelTemplate);

        var label = NewLabel(labelTemplate, preview.transform, "Label", "");
        label.horizontalAlignment = HorizontalAlignmentOptions.Center;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.raycastTarget = false;
        Stretch(label.rectTransform);
        dialog.previewLabel = label;
    }

    private static void BuildProgress(WorldCreationDialog dialog, Transform parent, TMP_Text labelTemplate)
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

        var percent = NewLabel(labelTemplate, rect, "Percent", "");
        percent.enableAutoSizing = false;
        percent.fontSize *= PROGRESS_FONT_SCALE;
        percent.horizontalAlignment = HorizontalAlignmentOptions.Center;
        percent.verticalAlignment = VerticalAlignmentOptions.Middle;
        percent.raycastTarget = false;
        Stretch(percent.rectTransform);
        dialog.progressPercent = percent;
    }

    private static void BuildDetails(WorldCreationDialog dialog, Transform root, RectTransform panel, TMP_Text labelTemplate, Button cancel)
    {
        var overlay = new GameObject(DETAILS_NAME, typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(root, false);
        Stretch((RectTransform)overlay.transform);
        overlay.GetComponent<Image>().color = OverlayColor;

        var frame = new GameObject("Frame", typeof(RectTransform));
        var frameRect = (RectTransform)frame.transform;
        frameRect.SetParent(overlay.transform, false);
        frameRect.sizeDelta = new Vector2(DETAILS_WIDTH, DETAILS_HEIGHT);

        if (panel.Find("bkg") is { } background)
            Object.Instantiate(background.gameObject, frameRect).name = "bkg";

        var y = DETAILS_HEIGHT / 2f - DETAILS_PADDING;

        var title = NewLabel(labelTemplate, frameRect, "topic", "World preview");
        title.horizontalAlignment = HorizontalAlignmentOptions.Center;
        Place(title.transform, 0f, y, DETAILS_MAP_SIZE, DETAILS_TITLE_HEIGHT);
        y -= DETAILS_TITLE_HEIGHT + BUTTON_GAP;

        var map = NewRawImage(frameRect, "Map", Shapes.Disc(WorldPreview.DETAILS_SIZE, DiscColor), false);
        Place(map.transform, 0f, y, DETAILS_MAP_SIZE, DETAILS_MAP_SIZE);
        var ring = NewRawImage(map.transform, "Ring", Shapes.Ring(WorldPreview.DETAILS_SIZE, RING_WIDTH * 2f, RingColor), false);
        Stretch(ring.rectTransform);
        y -= DETAILS_MAP_SIZE + BUTTON_GAP;

        var close = NewButton(cancel, frameRect, "Close", "Close", 1f);
        Place(close.transform, 0f, y, CLOSE_BUTTON_WIDTH, MAIN_BUTTON_HEIGHT);

        dialog.detailsOverlay = overlay;
        dialog.detailsFrame = frameRect;
        dialog.detailsImage = map;
        dialog.detailsCloseButton = close;

        overlay.SetActive(false);
    }

    private static SettingsDialog BuildSettingsDialog(FejdStartup startup, Transform root, RectTransform panel, TMP_Text labelTemplate, Button cancel)
    {
        var overlay = new GameObject(SETTINGS_NAME, typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(root, false);
        Stretch((RectTransform)overlay.transform);
        overlay.GetComponent<Image>().color = OverlayColor;

        var frame = (RectTransform)new GameObject("Frame", typeof(RectTransform)).transform;
        frame.SetParent(overlay.transform, false);

        if (panel.Find("bkg") is { } background)
            Object.Instantiate(background.gameObject, frame).name = "bkg";

        var rows = (RectTransform)new GameObject("Rows", typeof(RectTransform)).transform;
        rows.SetParent(frame, false);
        Stretch(rows);

        var title = NewLabel(labelTemplate, frame, "topic", "");
        title.horizontalAlignment = HorizontalAlignmentOptions.Center;

        var error = NewLabel(labelTemplate, frame, "error", "");
        error.horizontalAlignment = HorizontalAlignmentOptions.Center;
        error.verticalAlignment = VerticalAlignmentOptions.Middle;
        error.textWrappingMode = TextWrappingModes.Normal;
        error.color = ErrorColor;
        error.fontSize *= ERROR_FONT_SCALE;

        var rowLabel = NewLabel(labelTemplate, frame, "LabelTemplate", "");
        rowLabel.horizontalAlignment = HorizontalAlignmentOptions.Left;
        rowLabel.gameObject.SetActive(false);

        var dropdown = NewDropdown(startup, frame);
        dropdown.gameObject.SetActive(false);

        var settings = overlay.AddComponent<SettingsDialog>();
        settings.frame = frame;
        settings.title = title;
        settings.rows = rows;
        settings.error = error;
        settings.okButton = NewButton(cancel, frame, "Ok", "OK", 1f);
        settings.cancelButton = NewButton(cancel, frame, "Cancel", "Cancel", 1f);
        settings.labelTemplate = rowLabel;
        settings.dropdownTemplate = dropdown;
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
    private static Button NewEditableLabel(TMP_Text labelTemplate, Button buttonTemplate, RectTransform panel, string name, string text, ref float y)
    {
        var top = y;
        var label = NewLabel(labelTemplate, panel, name, text);
        PlaceLabel(label.transform, ref y);

        var button = NewButton(buttonTemplate, panel, name + "Edit", "Edit", 1f);
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

    private static void DestroyChild(Transform parent, string name)
    {
        if (parent.Find(name) is { } child)
            Object.DestroyImmediate(child.gameObject);
    }

    private static void StripLocalization(GameObject gameObject)
    {
        foreach (var localize in gameObject.GetComponentsInChildren<Localize>(true))
            Object.DestroyImmediate(localize);
    }

    private static TMP_Text NewLabel(TMP_Text template, Transform parent, string name, string text)
    {
        var label = Object.Instantiate(template.gameObject, parent);
        label.name = name;
        StripLocalization(label);

        var component = label.GetComponent<TMP_Text>();
        component.text = text;
        return component;
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

    private static Button GetRefreshButton(FejdStartup startup)
    {
        return startup.m_serverListPanel.GetComponentInChildren<ServerListGui>(true).m_serverRefreshButton;
    }

    private static Button NewIconButton(FejdStartup startup, Transform parent, string name, Sprite icon)
    {
        var button = NewButton(GetRefreshButton(startup), parent, name, null, 1f);
        button.transform.Find(REFRESH_ICON_CHILD).GetComponent<Image>().sprite = icon;
        return button;
    }

    private static Button NewButton(Button template, Transform parent, string name, string? text, float fontScale)
    {
        var gameObject = Object.Instantiate(template.gameObject, parent);
        gameObject.name = name;
        StripLocalization(gameObject);

        foreach (var gamePad in gameObject.GetComponentsInChildren<UIGamePad>(true))
        {
            if (gamePad.m_hint != null)
                Object.DestroyImmediate(gamePad.m_hint);
            Object.DestroyImmediate(gamePad);
        }

        foreach (var tooltip in gameObject.GetComponentsInChildren<UITooltip>(true))
            Object.DestroyImmediate(tooltip);

        var button = gameObject.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();

        if (text == null)
            return button;

        var label = gameObject.GetComponentInChildren<TMP_Text>(true);
        label.text = text;
        label.fontSize *= fontScale;
        label.fontSizeMax *= fontScale;
        label.fontSizeMin *= fontScale;
        return button;
    }

    private static TMP_Text NewSummary(TMP_InputField fieldTemplate, RectTransform panel, string name, ref float y)
    {
        var gameObject = Object.Instantiate(fieldTemplate.gameObject, panel);
        gameObject.name = name;
        StripLocalization(gameObject);

        var inputField = gameObject.GetComponent<TMP_InputField>();
        var text = inputField.textComponent;
        if (inputField.placeholder != null)
            Object.DestroyImmediate(inputField.placeholder.gameObject);
        Object.DestroyImmediate(inputField);

        text.text = "";
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.horizontalAlignment = HorizontalAlignmentOptions.Left;
        text.verticalAlignment = VerticalAlignmentOptions.Top;
        text.enableAutoSizing = false;
        text.fontSize *= SUMMARY_FONT_SCALE;

        Place(gameObject.transform, COLUMN1_X, y, COLUMN1_WIDTH, SUMMARY_HEIGHT);
        y -= SUMMARY_HEIGHT + ROW_GAP;
        return text;
    }

    private static TMP_Dropdown NewDropdown(FejdStartup startup, Transform panel)
    {
        var template = startup.m_settingsPrefab.GetComponentInChildren<TMP_Dropdown>(true);
        var gameObject = Object.Instantiate(template.gameObject, panel);
        gameObject.name = "GeneratorDropdown";
        StripLocalization(gameObject);

        var dropdown = gameObject.GetComponent<TMP_Dropdown>();
        RemoveExternalCaption(dropdown);
        dropdown.captionText.overflowMode = TextOverflowModes.Ellipsis;
        dropdown.itemText.overflowMode = TextOverflowModes.Ellipsis;
        dropdown.captionText.richText = true;
        dropdown.itemText.richText = true;
        dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        return dropdown;
    }

    // Settings menu dropdowns may carry a side caption next to the box, besides the one showing the selected value
    private static void RemoveExternalCaption(TMP_Dropdown dropdown)
    {
        foreach (var child in dropdown.transform.Cast<Transform>().ToList())
        {
            if (child.GetComponent<TMP_Text>() != null && child != dropdown.captionText.transform)
                Object.DestroyImmediate(child.gameObject);
        }
    }
}
