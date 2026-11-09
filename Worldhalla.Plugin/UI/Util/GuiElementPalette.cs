using GUIFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Worldhalla.Plugin.UI.Util;

// Copies of the start menu's own controls, cleared of the behaviour they carry in vanilla's menus. The templates are
// copied once, at construction, onto an inactive shelf, so what is handed out doesn't depend on what vanilla, other
// mods or our own dialogs do to the originals afterwards.
public sealed class GuiElementPalette
{
    public const string GAMEPAD_BACK = "ButtonB";
    public const string GAMEPAD_CONFIRM = "ButtonX";
    public const string GAMEPAD_ACTION = "ButtonY";

    private const string ICON_CHILD = "Image";
    private const string LABEL_SOURCE = "seed";
    private const string BACKGROUND = "bkg";
    private const int OVERLAY_GROUP_PRIORITY = 2;

    private readonly TMP_Text label;
    private readonly Button button;
    private readonly Button iconButton;
    private readonly TMP_Dropdown dropdown;
    private readonly GuiInputField textField;
    private readonly GameObject textBox;
    private readonly Scrollbar scrollbar;
    private readonly GameObject? background;

    public GuiElementPalette(FejdStartup startup)
    {
        var at = NewShelf(startup, "WorldhallaPalette");
        var createWorld = startup.m_newWorldName.transform.parent;

        label = Copy(createWorld.Find(LABEL_SOURCE).gameObject, at, "Label").GetComponent<TMP_Text>();
        label.text = "";
        if (createWorld.Find(BACKGROUND) is { } bkg)
            background = Copy(bkg.gameObject, at, BACKGROUND);

        button = Copy(startup.m_newWorldDone.gameObject, at, "Button").GetComponent<Button>();
        button.interactable = true;
        iconButton = Copy(startup.m_serverListPanel.GetComponentInChildren<ServerListGui>(true).m_serverRefreshButton.gameObject, at, "IconButton")
            .GetComponent<Button>();
        foreach (var template in new[] { button, iconButton })
        {
            template.onClick = new Button.ButtonClickedEvent();
            foreach (var tooltip in template.GetComponentsInChildren<UITooltip>(true))
                Object.DestroyImmediate(tooltip);
        }

        dropdown = Copy(startup.m_settingsPrefab.GetComponentInChildren<TMP_Dropdown>(true).gameObject, at, "Dropdown").GetComponent<TMP_Dropdown>();
        RemoveExternalCaption(dropdown);
        dropdown.captionText.overflowMode = TextOverflowModes.Ellipsis;
        dropdown.itemText.overflowMode = TextOverflowModes.Ellipsis;
        dropdown.captionText.richText = true;
        dropdown.itemText.richText = true;
        dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();

        textField = Copy(startup.m_newWorldSeed.gameObject, at, "TextField").GetComponent<GuiInputField>();
        ClearTextField(textField);

        textBox = Copy(startup.m_newWorldName.gameObject, at, "TextBox");
        ClearTextBox(textBox);

        scrollbar = Copy(startup.m_patchLogScroll.gameObject, at, "Scrollbar").GetComponent<Scrollbar>();
        scrollbar.onValueChanged = new Scrollbar.ScrollEvent();
        scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
    }

    public TMP_Text Label(Transform parent, string name, string text)
    {
        var copy = Instantiate(label, parent, name);
        SetText(copy, text);
        return copy;
    }

    // A gamepad button relabels the template's hotkey hint for that button; without one the hotkey is removed, since
    // a stray copy of Done's X would fire alongside the real one
    public Button Button(Transform parent, string name, string text, string? gamepadButton = null)
    {
        var copy = Instantiate(button, parent, name);
        SetGamepadButton(copy, gamepadButton);
        SetText(copy.GetComponentInChildren<TMP_Text>(true), text);
        return copy;
    }

    public Button IconButton(Transform parent, string name, Sprite? icon = null)
    {
        var copy = Instantiate(iconButton, parent, name);
        SetGamepadButton(copy, null);
        if (icon != null)
            copy.transform.Find(ICON_CHILD).GetComponent<Image>().sprite = icon;
        return copy;
    }

    public TMP_Dropdown Dropdown(Transform parent, string name) => Instantiate(dropdown, parent, name);

    public GuiInputField TextField(Transform parent, string name) => Instantiate(textField, parent, name);

    // An input field's box and text with the input taken out
    public TMP_Text TextBox(Transform parent, string name)
    {
        var box = Object.Instantiate(textBox, parent);
        box.name = name;
        return box.GetComponentInChildren<TMP_Text>(true);
    }

    // A text box whose text grows downwards inside the box and scrolls vertically; its content carries the text
    public ScrollRect ScrollingTextBox(Transform parent, string name)
    {
        var text = TextBox(parent, name);
        text.overflowMode = TextOverflowModes.Overflow;

        var content = text.rectTransform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var viewport = (RectTransform)content.parent;
        if (viewport.GetComponent<RectMask2D>() == null)
            viewport.gameObject.AddComponent<RectMask2D>();

        var box = viewport.parent;
        var bar = Instantiate(scrollbar, box, "Scrollbar");
        var barRect = (RectTransform)bar.transform;
        var barWidth = barRect.rect.width;
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = Vector2.one;
        barRect.pivot = new Vector2(1f, 0.5f);
        barRect.sizeDelta = new Vector2(barWidth, 0f);
        barRect.anchoredPosition = Vector2.zero;
        text.margin += new Vector4(0f, 0f, barWidth, 0f);

        var scroll = box.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = false;
        scroll.scrollSensitivity = text.fontSize;
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }

    // A screen-covering dimmer for a pop-up to sit on
    public GameObject Overlay(Transform root, string name)
    {
        var overlay = new GameObject(name, typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(root, false);
        Rects.Stretch((RectTransform)overlay.transform);
        overlay.GetComponent<Image>().color = Theme.Overlay;
        return overlay;
    }

    public RectTransform Frame(GameObject overlay)
    {
        var frame = (RectTransform)new GameObject("Frame", typeof(RectTransform)).transform;
        frame.SetParent(overlay.transform, false);
        if (background != null)
            Object.Instantiate(background, frame).name = BACKGROUND;
        return frame;
    }

    // Outranks the screen beneath the overlay, which then turns off its hotkeys and stops taking focus
    public static UIGroupHandler OverlayGroup(GameObject overlay)
    {
        overlay.AddComponent<CanvasGroup>();
        var group = overlay.AddComponent<UIGroupHandler>();
        group.m_groupPriority = OVERLAY_GROUP_PRIORITY;
        return group;
    }

    public static RawImage RawImage(Transform parent, string name, Texture texture, bool raycastTarget = false)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        gameObject.transform.SetParent(parent, false);

        var image = gameObject.GetComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = raycastTarget;
        return image;
    }

    public static ColorBlock ShownWhenSelected(ColorBlock colors)
    {
        colors.normalColor = colors.disabledColor = Color.clear;
        colors.highlightedColor = colors.selectedColor = colors.pressedColor = Color.white;
        colors.colorMultiplier = 1f;
        return colors;
    }

    internal static Transform NewShelf(FejdStartup startup, string name)
    {
        var shelf = new GameObject(name, typeof(RectTransform));
        shelf.SetActive(false);
        shelf.transform.SetParent(startup.transform, false);
        return shelf.transform;
    }

    private static T Instantiate<T>(T template, Transform parent, string name) where T : Component
    {
        var copy = Object.Instantiate(template, parent);
        copy.name = name;
        return copy;
    }

    internal static GameObject Copy(GameObject original, Transform shelf, string name)
    {
        var copy = Object.Instantiate(original, shelf);
        copy.name = name;
        foreach (var localize in copy.GetComponentsInChildren<Localize>(true))
            Object.DestroyImmediate(localize);
        return copy;
    }

    private static void ClearTextField(GuiInputField field)
    {
        if (field.placeholder != null)
            Object.DestroyImmediate(field.placeholder.gameObject);

        field.onValueChanged = new TMP_InputField.OnChangeEvent();
        field.onEndEdit = new TMP_InputField.SubmitEvent();
        field.onSubmit = new TMP_InputField.SubmitEvent();
        field.OnInputSubmit = new OnInputSubmitEvent();
        field.contentType = TMP_InputField.ContentType.Standard;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.characterLimit = 0;
        field.text = "";
    }

    private static void ClearTextBox(GameObject copy)
    {
        var inputField = copy.GetComponent<TMP_InputField>();
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
        text.fontSize *= Theme.SMALL_FONT_SCALE;
    }

    private static void SetGamepadButton(Button copy, string? gamepadButton)
    {
        foreach (var gamePad in copy.GetComponentsInChildren<UIGamePad>(true))
        {
            if (gamepadButton != null)
            {
                SetGamepadButton(gamePad, gamepadButton);
                continue;
            }

            if (gamePad.m_hint != null)
                Object.DestroyImmediate(gamePad.m_hint);
            Object.DestroyImmediate(gamePad);
        }
    }

    // Hints read "$KEY_<button>", which Localization resolves to the "Joy<button>" binding while a gamepad is active
    private static void SetGamepadButton(UIGamePad gamePad, string button)
    {
        gamePad.m_zinputKey = "Joy" + button;
        gamePad.m_keyCode = KeyCode.None;
        if (gamePad.m_hint == null)
            return;

        foreach (var hint in gamePad.m_hint.GetComponentsInChildren<TMP_Text>(true))
            hint.text = "$KEY_" + button;
        foreach (var hint in gamePad.m_hint.GetComponentsInChildren<Text>(true))
            hint.text = "$KEY_" + button;
        gamePad.m_hint.AddComponent<Localize>();
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

    private static void SetText(TMP_Text label, string text)
    {
        label.text = text;
        if (text.StartsWith("$"))
            label.gameObject.AddComponent<Localize>();
    }
}
