using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Worldhalla.Plugin.UI.SettingsForm;
using Worldhalla.Plugin.UI.Util;

namespace Worldhalla.Plugin.UI.CreateWorld;

public static class WorldCreationDialogBuilder
{
    private const string PANEL_NAME = "WorldhallaPanel";

    private const float DESCRIPTION_HEIGHT = 56f;
    private const float DESCRIPTION_ROW = DESCRIPTION_HEIGHT + Theme.ROW_GAP;

    // The preview is square and as tall as column 1, so the panel grows as wide as column 1 grows tall
    private const float PANEL_WIDTH = 860f + DESCRIPTION_ROW;
    private const float PANEL_HEIGHT = 540f + DESCRIPTION_ROW;
    private const float CONTENT_TOP = 215f + DESCRIPTION_ROW / 2f;
    private const float COLUMN1_X = -220f - DESCRIPTION_ROW / 2f;
    private const float COLUMN1_WIDTH = 360f;
    private const float COLUMN2_RIGHT = 395f + DESCRIPTION_ROW / 2f;
    private const float SUMMARY_HEIGHT = 85f;
    private const float EDIT_BUTTON_WIDTH = 80f;
    private const float ICON_BUTTON_SIZE = Theme.FIELD_HEIGHT;

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
        dialog.nameField = nameField;
        dialog.seedField = seedField;
        dialog.doneButton = done;
        dialog.cancelButton = cancel;

        var y = CONTENT_TOP;

        PlaceLabel(palette.Label(panel, "name", vanilla.NameLabelKey).transform, ref y);
        if (vanilla.NameTip(panel) is { } nameTip)
        {
            Rects.Place(nameTip.transform, COLUMN1_X, y + Theme.LABEL_HEIGHT, COLUMN1_WIDTH, Theme.LABEL_HEIGHT);
            nameTip.horizontalAlignment = HorizontalAlignmentOptions.Right;
        }
        PlaceField(nameField.transform, panel, Theme.FIELD_HEIGHT, ref y);

        PlaceLabel(palette.Label(panel, "seed", vanilla.SeedLabelKey).transform, ref y);
        seedField.transform.SetParent(panel, false);
        var seedWidth = COLUMN1_WIDTH - ICON_BUTTON_SIZE - Theme.BUTTON_GAP;
        Rects.Place(seedField.transform, COLUMN1_X - (COLUMN1_WIDTH - seedWidth) / 2f, y, seedWidth, Theme.FIELD_HEIGHT);
        dialog.newSeedButton = palette.IconButton(panel, "NewSeed");
        Rects.Place(dialog.newSeedButton.transform, COLUMN1_X + (COLUMN1_WIDTH - ICON_BUTTON_SIZE) / 2f, y, ICON_BUTTON_SIZE, ICON_BUTTON_SIZE);
        y -= Theme.FIELD_HEIGHT + Theme.ROW_GAP;

        PlaceLabel(palette.Label(panel, "generator", "$wh_generator").transform, ref y);
        dialog.generatorDropdown = palette.Dropdown(panel, "GeneratorDropdown");
        var dropdownRect = (RectTransform)dialog.generatorDropdown.transform;
        PlaceField(dropdownRect, panel, dropdownRect.sizeDelta.y, ref y);
        dialog.generatorDescription = NewDescription(palette, panel, ref y);

        dialog.worldSettingsButton = NewEditableLabel(palette, panel, "worldSettings", "$wh_world_settings", ref y);
        dialog.worldSettingsSummary = NewSummary(palette, panel, "WorldSettingsSummary", ref y);

        dialog.generatorSettingsButton = NewEditableLabel(palette, panel, "generatorSettings", "$wh_generator_settings", ref y);
        dialog.generatorSettingsSummary = NewSummary(palette, panel, "GeneratorSettingsSummary", ref y);
        var contentBottom = y + Theme.ROW_GAP;

        // Column 2 ends level with column 1, and the preview takes whatever height its buttons leave over
        const float controlsHeight = Theme.ROW_GAP + Theme.BUTTON_HEIGHT;
        var previewSize = Mathf.FloorToInt(CONTENT_TOP - contentBottom - controlsHeight);
        var column2Width = (float)previewSize;
        var column2X = COLUMN2_RIGHT - column2Width / 2f;

        y = CONTENT_TOP;
        dialog.tile = PreviewTileBuilder.Build(palette, panel, column2X, previewSize, ref y);

        var halfWidth = (column2Width - Theme.BUTTON_GAP) / 2f;
        var halfOffset = (halfWidth + Theme.BUTTON_GAP) / 2f;

        done.transform.SetParent(panel, false);
        cancel.transform.SetParent(panel, false);
        Rects.Place(done.transform, column2X - halfOffset, y, halfWidth, Theme.BUTTON_HEIGHT);
        Rects.Place(cancel.transform, column2X + halfOffset, y, halfWidth, Theme.BUTTON_HEIGHT);

        if (panel.GetComponent<UIGroupHandler>() is { } group)
            group.m_defaultElement = nameField.gameObject;

        dialog.details = DetailsOverlayBuilder.Build(palette, root, panel.gameObject);
        dialog.locations = dialog.details.locations;
        dialog.settingsDialog = SettingsDialogBuilder.Build(palette, root);

        dialog.Initialize();
        return dialog;
    }

    private static void PlaceLabel(Transform label, ref float y)
    {
        Rects.Place(label, COLUMN1_X, y, COLUMN1_WIDTH, Theme.LABEL_HEIGHT);
        label.GetComponent<TMP_Text>().horizontalAlignment = HorizontalAlignmentOptions.Left;
        y -= Theme.LABEL_HEIGHT;
    }

    // Created after its label so it draws above it and gets the clicks the full-width label would otherwise take
    private static Button NewEditableLabel(GuiElementPalette palette, RectTransform panel, string name, string text, ref float y)
    {
        var top = y;
        var label = palette.Label(panel, name, text);
        PlaceLabel(label.transform, ref y);

        var button = palette.Button(panel, name + "Edit", "$wh_edit");
        Rects.Place(button.transform, COLUMN1_X + (COLUMN1_WIDTH - EDIT_BUTTON_WIDTH) / 2f, top, EDIT_BUTTON_WIDTH, Theme.LABEL_HEIGHT);

        var buttonText = button.GetComponentInChildren<TMP_Text>(true);
        Rects.Stretch(buttonText.rectTransform);
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
        Rects.Place(field, COLUMN1_X, y, COLUMN1_WIDTH, height);
        y -= height + Theme.ROW_GAP;
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

        Rects.Place(box, COLUMN1_X, y, COLUMN1_WIDTH, height);
        y -= height + Theme.ROW_GAP;
    }
}
