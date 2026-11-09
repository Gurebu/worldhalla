using GUIFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Worldhalla.API.Settings;
using Worldhalla.API.Settings.Controls;
using Worldhalla.Plugin.Localization;
using Worldhalla.Plugin.UI.Util;

namespace Worldhalla.Plugin.UI.SettingsForm;

public class SettingsDialog : MonoBehaviour
{
    private const float WIDTH = 560f;
    private const float PADDING = 16f;
    private const float TITLE_HEIGHT = 30f;
    private const float LABEL_HEIGHT = Theme.LABEL_HEIGHT;
    private const float FIELD_HEIGHT = Theme.FIELD_HEIGHT;
    private const float ROW_GAP = Theme.ROW_GAP;
    private const float ERROR_HEIGHT = 40f;
    private const float BUTTON_WIDTH = Theme.BUTTON_WIDTH;
    private const float BUTTON_HEIGHT = Theme.BUTTON_HEIGHT;
    private const float BUTTON_GAP = Theme.BUTTON_GAP;
    private const string BUILD_FAILED = "$wh_settings_build_failed";

    public RectTransform frame = null!;
    public TMP_Text title = null!;
    public RectTransform rows = null!;
    public TMP_Text error = null!;
    public Button okButton = null!;
    public Button cancelButton = null!;
    public TMP_Text labelTemplate = null!;
    public TMP_Dropdown dropdownTemplate = null!;
    public GuiInputField textFieldTemplate = null!;
    public UIGroupHandler group = null!;

    private readonly List<Action> unbind = [];

    private Func<Action>? build;
    private GameObject? firstField;

    public bool IsOpen => build != null;

    public void Initialize()
    {
        okButton.onClick.AddListener(Accept);
        cancelButton.onClick.AddListener(Close);
        gameObject.SetActive(false);
    }

    public void Open<T>(string heading, IReadOnlyList<Control> controls, Func<T> buildSettings, Action<T> onAccepted)
    {
        Close();

        try
        {
            var y = -(PADDING + TITLE_HEIGHT + ROW_GAP);
            foreach (var control in controls)
                y = AddRow(control, y);

            build = () =>
            {
                var settings = buildSettings();
                return () => onAccepted(settings);
            };
        }
        catch (Exception e)
        {
            Logger.LogError($"Can't show settings form: {e}");
            Clear();
            return;
        }

        title.text = heading;
        Layout(controls.Count);
        group.m_defaultElement = firstField != null ? firstField : cancelButton.gameObject;
        gameObject.SetActive(true);
        Validate();
    }

    public void Close()
    {
        Clear();
        gameObject.SetActive(false);
    }

    private void Clear()
    {
        foreach (var action in unbind)
            action();
        unbind.Clear();

        foreach (Transform row in rows)
            Destroy(row.gameObject);

        build = null;
        firstField = null;
    }

    private float AddRow(Control control, float y)
    {
        var label = Instantiate(labelTemplate.gameObject, rows).GetComponent<TMP_Text>();
        label.gameObject.SetActive(true);
        label.text = control.Label;
        Place(label.rectTransform, y, LABEL_HEIGHT);
        y -= LABEL_HEIGHT;

        var field = control switch
        {
            Select select => BindSelect(select),
            TextField textField => BindTextField(textField),
            _ => throw new NotSupportedException($"Control {control.GetType().Name} isn't supported by this Worldhalla version"),
        };
        Place((RectTransform)field.transform, y, FIELD_HEIGHT);
        if (firstField == null)
            firstField = field;

        return y - FIELD_HEIGHT - ROW_GAP;
    }

    private GameObject BindSelect(Select select)
    {
        var dropdown = Instantiate(dropdownTemplate.gameObject, rows).GetComponent<TMP_Dropdown>();
        dropdown.gameObject.SetActive(true);
        dropdown.ClearOptions();
        dropdown.AddOptions(select.Options.ToList());
        dropdown.SetValueWithoutNotify(select.Selected);
        dropdown.RefreshShownValue();
        dropdown.onValueChanged.AddListener(index => select.Selected = index);

        void Show()
        {
            dropdown.SetValueWithoutNotify(select.Selected);
            dropdown.RefreshShownValue();
            Validate();
        }

        select.Changed += Show;
        unbind.Add(() => select.Changed -= Show);
        return dropdown.gameObject;
    }

    private GameObject BindTextField(TextField textField)
    {
        var input = Instantiate(textFieldTemplate.gameObject, rows).GetComponent<GuiInputField>();
        input.gameObject.SetActive(true);
        input.VirtualKeyboardTitle = textField.Label;
        input.SetTextWithoutNotify(textField.Text);
        input.onValueChanged.AddListener(text => textField.Text = text);

        // Typing raises Changed too, and resetting the same text would move the caret
        void Show()
        {
            if (input.text != textField.Text)
                input.SetTextWithoutNotify(textField.Text);
            Validate();
        }

        textField.Changed += Show;
        unbind.Add(() => textField.Changed -= Show);
        return input.gameObject;
    }

    private void Validate()
    {
        var failure = TryBuild(out _);
        error.text = failure ?? "";
        okButton.interactable = failure == null;
    }

    private void Accept()
    {
        if (TryBuild(out var handOver) != null)
            return;

        Close();
        handOver!();
    }

    private string? TryBuild(out Action? handOver)
    {
        handOver = null;
        if (build == null)
            return Translations.Get(BUILD_FAILED);

        try
        {
            handOver = build();
            return null;
        }
        catch (SettingsException e)
        {
            return e.Message;
        }
        catch (Exception e)
        {
            Logger.LogError($"Settings form failed to build settings: {e}");
            return Translations.Get(BUILD_FAILED);
        }
    }

    private void Layout(int rowCount)
    {
        var rowsHeight = rowCount * (LABEL_HEIGHT + FIELD_HEIGHT + ROW_GAP);
        var height = PADDING + TITLE_HEIGHT + ROW_GAP + rowsHeight + ERROR_HEIGHT + BUTTON_HEIGHT + PADDING;
        frame.sizeDelta = new Vector2(WIDTH, height);
        Rects.ScaleToFit(frame, (RectTransform)transform);

        Place(title.rectTransform, -PADDING, TITLE_HEIGHT);
        Place(error.rectTransform, -(PADDING + TITLE_HEIGHT + ROW_GAP + rowsHeight), ERROR_HEIGHT);

        var buttonsTop = -(height - PADDING - BUTTON_HEIGHT);
        var offset = (BUTTON_WIDTH + BUTTON_GAP) / 2f;
        Rects.PlaceFromTop(okButton.transform, -offset, buttonsTop, BUTTON_WIDTH, BUTTON_HEIGHT);
        Rects.PlaceFromTop(cancelButton.transform, offset, buttonsTop, BUTTON_WIDTH, BUTTON_HEIGHT);
    }

    private static void Place(RectTransform rect, float top, float height)
    {
        Rects.PlaceFromTop(rect, 0f, top, WIDTH - 2f * PADDING, height);
    }

    private void OnDestroy()
    {
        foreach (var action in unbind)
            action();
    }
}
