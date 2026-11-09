using TMPro;
using UnityEngine;
using Worldhalla.Plugin.UI.Util;

namespace Worldhalla.Plugin.UI.SettingsForm;

public static class SettingsDialogBuilder
{
    private const string NAME = "WorldhallaSettings";

    public static SettingsDialog Build(GuiElementPalette palette, Transform root)
    {
        var overlay = palette.Overlay(root, NAME);
        var frame = palette.Frame(overlay);

        var rows = (RectTransform)new GameObject("Rows", typeof(RectTransform)).transform;
        rows.SetParent(frame, false);
        Rects.Stretch(rows);

        var title = palette.Label(frame, "topic", "");
        title.horizontalAlignment = HorizontalAlignmentOptions.Center;

        var error = palette.Label(frame, "error", "");
        error.horizontalAlignment = HorizontalAlignmentOptions.Center;
        error.verticalAlignment = VerticalAlignmentOptions.Middle;
        error.textWrappingMode = TextWrappingModes.Normal;
        error.color = Theme.Error;
        error.fontSize *= Theme.ERROR_FONT_SCALE;

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
        settings.okButton = palette.Button(frame, "Ok", "$wh_settings_ok", GuiElementPalette.GAMEPAD_CONFIRM);
        settings.cancelButton = palette.Button(frame, "Cancel", "$wh_settings_cancel", GuiElementPalette.GAMEPAD_BACK);
        settings.group = GuiElementPalette.OverlayGroup(overlay);
        settings.labelTemplate = rowLabel;
        settings.dropdownTemplate = dropdown;
        settings.textFieldTemplate = textField;
        settings.Initialize();
        return settings;
    }
}
