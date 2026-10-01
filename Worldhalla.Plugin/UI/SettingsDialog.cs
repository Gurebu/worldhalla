using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Worldhalla.API.Settings;
using Worldhalla.API.Settings.Controls;
using Worldhalla.Plugin.Internal;
using Logger = Worldhalla.Plugin.Internal.Logger;

namespace Worldhalla.Plugin.UI;

public class SettingsDialog : MonoBehaviour
{
    private const float WIDTH = 560f;
    private const float PADDING = 16f;
    private const float TITLE_HEIGHT = 30f;
    private const float LABEL_HEIGHT = 26f;
    private const float FIELD_HEIGHT = 35f;
    private const float ROW_GAP = 10f;
    private const float ERROR_HEIGHT = 40f;
    private const float BUTTON_WIDTH = 136f;
    private const float BUTTON_HEIGHT = 45f;
    private const float BUTTON_GAP = 8f;
    private const float SCREEN_FRACTION = 0.95f;
    private const string BUILD_FAILED = "$wh_settings_build_failed";

    internal RectTransform frame = null!;
    internal TMP_Text title = null!;
    internal RectTransform rows = null!;
    internal TMP_Text error = null!;
    internal Button okButton = null!;
    internal Button cancelButton = null!;
    internal TMP_Text labelTemplate = null!;
    internal TMP_Dropdown dropdownTemplate = null!;
    internal UIGroupHandler group = null!;

    private readonly List<Action> unbind = [];
    private ISettingsForm? form;
    private Action<ISettings>? accepted;
    private GameObject? firstField;

    public bool IsOpen => form != null;

    internal void Initialize()
    {
        okButton.onClick.AddListener(Accept);
        cancelButton.onClick.AddListener(Close);
        gameObject.SetActive(false);
    }

    public void Open(string heading, ISettingsForm settingsForm, Action<ISettings> onAccepted)
    {
        Close();

        try
        {
            var y = -(PADDING + TITLE_HEIGHT + ROW_GAP);
            foreach (var control in settingsForm.Controls)
                y = AddRow(control, y);

            form = settingsForm;
            accepted = onAccepted;
        }
        catch (Exception e)
        {
            Logger.LogError($"Can't show settings form: {e}");
            Clear();
            return;
        }

        title.text = heading;
        Layout(settingsForm.Controls.Count);
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

        form = null;
        accepted = null;
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

    private void Validate()
    {
        var failure = TryBuild(out _);
        error.text = failure ?? "";
        okButton.interactable = failure == null;
    }

    private void Accept()
    {
        if (TryBuild(out var settings) != null || accepted == null)
            return;

        var onAccepted = accepted;
        Close();
        onAccepted(settings!);
    }

    private string? TryBuild(out ISettings? settings)
    {
        settings = null;
        if (form == null)
            return Translations.Get(BUILD_FAILED);

        try
        {
            var built = form.Build();
            SettingsBlob.Check(built);

            settings = built;
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

        var available = ((RectTransform)transform).rect.size * SCREEN_FRACTION;
        var scale = Mathf.Min(1f, available.x / WIDTH, available.y / height);
        frame.localScale = new Vector3(scale, scale, 1f);

        Place(title.rectTransform, -PADDING, TITLE_HEIGHT);
        Place(error.rectTransform, -(PADDING + TITLE_HEIGHT + ROW_GAP + rowsHeight), ERROR_HEIGHT);

        var buttonsTop = -(height - PADDING - BUTTON_HEIGHT);
        var offset = (BUTTON_WIDTH + BUTTON_GAP) / 2f;
        PlaceButton(okButton, -offset, buttonsTop);
        PlaceButton(cancelButton, offset, buttonsTop);
    }

    private static void Place(RectTransform rect, float top, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(WIDTH - 2f * PADDING, height);
        rect.anchoredPosition = new Vector2(0f, top);
    }

    private static void PlaceButton(Button button, float x, float top)
    {
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(BUTTON_WIDTH, BUTTON_HEIGHT);
        rect.anchoredPosition = new Vector2(x, top);
    }

    private void OnDestroy()
    {
        foreach (var action in unbind)
            action();
    }
}
