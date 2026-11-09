using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Worldhalla.Plugin.Compat;
using Worldhalla.Plugin.Localization;
using Worldhalla.Plugin.UI.Preview;
using Worldhalla.Plugin.UI.Preview.Locations;
using Worldhalla.Plugin.UI.Util;

namespace Worldhalla.Plugin.UI.CreateWorld;

public class LocationsLayer : MonoBehaviour
{
    private const float ICON_SIZE = 32f;
    private const float CAPTION_FONT_SIZE = 16f;
    private const float CAPTION_WIDTH = 240f;
    private const float CAPTION_OUTLINE_WIDTH = 0.35f;

    private const string LOCATIONS = "$wh_preview_locations";
    private const string LOCATIONS_PROGRESS = "$wh_preview_locations_progress";

    public RectTransform iconParent = null!;
    public Button button = null!;
    public GameObject note = null!;
    public GuiElementPalette palette = null!;

    private readonly List<(RectTransform icon, Vector3 position)> icons = [];

    private WorldPreview preview = null!;
    private TMP_Text buttonLabel = null!;
    private WorldGenerator? simulatedGenerator;
    private CancellationTokenSource? simulation;
    private LocationSimulation.Progress? simulationProgress;
    private Material? captionMaterial;

    private static bool Simulable => LocationPlacementAccelerator.PlacesLikeVanilla;

    public void Initialize(WorldPreview worldPreview)
    {
        preview = worldPreview;
        buttonLabel = button.GetComponentInChildren<TMP_Text>(true);
        button.onClick.AddListener(Simulate);
    }

    public void ResetSession()
    {
        simulation?.Cancel();
        simulatedGenerator = null;
        simulationProgress = null;
        ClearIcons();
    }

    public void ShowAvailability()
    {
        note.SetActive(!Simulable);
    }

    public void RefreshButton()
    {
        button.interactable = Simulable && preview.Generator != null && preview.Generator != simulatedGenerator;
        buttonLabel.text = simulationProgress is { } progress
            ? Translations.Get(LOCATIONS_PROGRESS, Mathf.FloorToInt(progress.Fraction * 100f).ToString())
            : Translations.Get(LOCATIONS);
    }

    public void Place(PreviewView view)
    {
        if (preview.Generator != simulatedGenerator || preview.WorldRadius is not { } radius)
        {
            ClearIcons();
            return;
        }

        foreach (var (icon, position) in icons)
        {
            var at = view.Normalized(new Vector2(position.x, position.z), radius);
            icon.anchorMin = icon.anchorMax = at;
        }
    }

    private void OnDestroy()
    {
        if (captionMaterial != null)
            Destroy(captionMaterial);
    }

    // Experimental: logs where the priority locations of the previewed world would be placed
    private void Simulate()
    {
        if (!Simulable || preview.Generator is not { } generator || generator == simulatedGenerator)
            return;

        simulatedGenerator = generator;
        simulation?.Cancel();
        simulation = new CancellationTokenSource();
        ClearIcons();
        StartCoroutine(RunSimulation(generator, simulation.Token));
    }

    // A simulation that ends without showing anything leaves the button available to try again
    private IEnumerator RunSimulation(WorldGenerator generator, CancellationToken token)
    {
        var progress = new LocationSimulation.Progress();
        simulationProgress = progress;

        yield return LocationSimulation.Run(generator, progress, Show, token);

        if (simulationProgress != progress)
            yield break;

        simulationProgress = null;
        if (icons.Count == 0 && simulatedGenerator == generator)
            simulatedGenerator = null;
    }

    private void Show(List<LocationSimulation.Placed> placed)
    {
        if (preview.Generator != simulatedGenerator)
            return;

        foreach (var location in placed)
        {
            var icon = new GameObject(location.name, typeof(RectTransform), typeof(RawImage));
            icon.transform.SetParent(iconParent, false);
            var image = icon.GetComponent<RawImage>();
            image.texture = location.icon;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.sizeDelta = ICON_SIZE * (1f + 2f * IconOutline.MARGIN) * Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            icons.Add((rect, location.position));

            if (location.caption != null)
                AddCaption(rect, location.caption);
        }
    }

    private void AddCaption(RectTransform icon, string text)
    {
        var caption = palette.Label(icon, "Caption", "");
        caption.fontSharedMaterial = CaptionMaterial(caption);
        caption.text = text;
        caption.fontSize = CAPTION_FONT_SIZE;
        caption.horizontalAlignment = HorizontalAlignmentOptions.Center;
        caption.verticalAlignment = VerticalAlignmentOptions.Top;
        caption.textWrappingMode = TextWrappingModes.NoWrap;
        caption.raycastTarget = false;

        var rect = caption.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(CAPTION_WIDTH, CAPTION_FONT_SIZE * 1.5f);
        rect.anchoredPosition = Vector2.zero;
    }

    private Material CaptionMaterial(TMP_Text caption)
    {
        if (captionMaterial != null)
            return captionMaterial;

        captionMaterial = new Material(caption.fontSharedMaterial);
        // Dilating the face by the outline's width keeps the outline outside the glyph, as the game's own outlined fonts do
        captionMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, CAPTION_OUTLINE_WIDTH);
        captionMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, CAPTION_OUTLINE_WIDTH);
        captionMaterial.SetColor(ShaderUtilities.ID_OutlineColor, IconOutline.Ink);
        return captionMaterial;
    }

    private void ClearIcons()
    {
        foreach (var (icon, _) in icons)
            Destroy(icon.gameObject);
        icons.Clear();
    }
}
