using System.Collections;
using GUIFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Worldhalla.API;
using Worldhalla.API.Settings;
using Worldhalla.API.World;
using Worldhalla.Plugin.Extensions;
using Worldhalla.Plugin.Internal;
using Worldhalla.Plugin.UI.Preview;
using Worldhalla.Plugin.UI.Preview.Locations;
using Logger = Worldhalla.Plugin.Internal.Logger;

namespace Worldhalla.Plugin.UI;

public class WorldCreationDialog : MonoBehaviour
{
    private const string VANILLA_OPTION = "Valheim";
    private const string GUID_COLOR = "#8a8a8a";
    private const float SEED_DEBOUNCE_SECONDS = 0.5f;
    private const float SPINNER_DEGREES_PER_SECOND = -270f;
    private const float DETAILS_SCREEN_FRACTION = 0.95f;
    private const float LOCATION_ICON_SIZE = 32f;
    private const float CAPTION_FONT_SIZE = 16f;
    private const float CAPTION_WIDTH = 240f;
    private const float CAPTION_OUTLINE_WIDTH = 0.35f;
    private const float GAMEPAD_PAN_PIXELS_PER_SECOND = 600f;
    private const float GAMEPAD_DEAD_ZONE = 0.2f;

    private const string SPOILER_HEADER = "$wh_spoiler_header";
    private const string SPOILER_TEXT = "$wh_spoiler_text";
    private const string CLICK_TO_REVEAL = "$wh_preview_click_to_reveal";
    private const string ENTER_SEED = "$wh_preview_enter_seed";
    private const string GENERATING = "$wh_preview_generating";
    private const string FAILED = "$wh_preview_failed";
    private const string LOCATIONS = "$wh_preview_locations";
    private const string LOCATIONS_PROGRESS = "$wh_preview_locations_progress";

    internal FejdStartup startup = null!;
    internal GuiInputField nameField = null!;
    internal GuiInputField seedField = null!;
    internal TMP_Dropdown generatorDropdown = null!;
    internal TMP_Text worldSettingsSummary = null!;
    internal TMP_Text generatorSettingsSummary = null!;
    internal RawImage previewImage = null!;
    internal Button previewButton = null!;
    internal TMP_Text previewLabel = null!;
    internal RectTransform spinner = null!;
    internal GameObject progress = null!;
    internal Image progressFill = null!;
    internal TMP_Text progressPercent = null!;
    internal Texture2D discTexture = null!;
    internal int previewSize;
    internal Button hideButton = null!;
    internal Button detailsButton = null!;
    internal Button worldSettingsButton = null!;
    internal Button generatorSettingsButton = null!;
    internal Button newSeedButton = null!;
    internal GameObject detailsOverlay = null!;
    internal RectTransform detailsFrame = null!;
    internal RawImage detailsImage = null!;
    internal RawImage detailsWholeImage = null!;
    internal DragAndScroll detailsInput = null!;
    internal UIGroupHandler detailsGroup = null!;
    internal GameObject detailsRing = null!;
    internal Button detailsCloseButton = null!;
    internal Button locationsButton = null!;
    internal TMP_Text captionTemplate = null!;
    internal SettingsDialog settingsDialog = null!;

    private WorldPreview preview = null!;
    private PreviewTexture? previewTexture;
    private PreviewTexture? detailsTexture;
    private PreviewTexture? detailsWholeTexture;
    private Texture? detailsPlaceholder;
    private PreviewView detailsView = PreviewView.Whole;
    private PreviewView detailsDragStart;
    private Vector2 gamepadPan;

    private List<KeyValuePair<string, IWorldGenerator>> generators = [];
    private ISettings? settings;
    private bool revealed;
    private bool spoilerAccepted;
    private float? seedChangedAt;
    private Exception? reportedFailure;
    private string? previewSeed;
    private GeneratedWorld? simulatedWorld;
    private CancellationTokenSource? simulation;
    private LocationSimulation.Progress? simulationProgress;
    private TMP_Text locationsLabel = null!;
    private readonly List<(RectTransform icon, Vector3 position)> locationIcons = [];
    private Material? captionMaterial;

    private KeyValuePair<string, IWorldGenerator>? Selected
    {
        get
        {
            var index = generatorDropdown.value - 1;
            return index >= 0 && index < generators.Count ? generators[index] : null;
        }
    }

    public string? SelectedWorldgenGuid => Selected?.Key;

    public IWorldGenerator? SelectedWorldgen => Selected?.Value;

    public ISettings? Settings => settings;

    internal void Initialize()
    {
        preview = new WorldPreview(previewSize);
        previewTexture = new PreviewTexture(previewSize);
        detailsTexture = new PreviewTexture(PreviewView.SIZE);
        detailsWholeTexture = new PreviewTexture(PreviewView.SIZE);
        detailsPlaceholder = detailsImage.texture;

        seedField.onValueChanged.AddListener(_ => OnSeedChanged());
        generatorDropdown.onValueChanged.AddListener(_ => OnGeneratorChanged());
        newSeedButton.interactable = true;
        newSeedButton.onClick.AddListener(() => seedField.text = World.GenerateSeed());
        previewButton.onClick.AddListener(OnPreviewClicked);
        hideButton.onClick.AddListener(Hide);
        detailsButton.onClick.AddListener(OpenDetails);
        detailsCloseButton.onClick.AddListener(CloseDetails);
        locationsButton.onClick.AddListener(SimulateLocations);
        locationsLabel = locationsButton.GetComponentInChildren<TMP_Text>(true);
        detailsInput.DragStarted += () => detailsDragStart = detailsView;
        detailsInput.Dragged += PanDetails;
        detailsInput.Scrolled += ZoomDetails;
        worldSettingsButton.onClick.AddListener(() => Logger.LogInfo("World settings screen is not implemented yet"));
        generatorSettingsButton.onClick.AddListener(OpenGeneratorSettings);
    }

    public void Open()
    {
        generators = Registry.Generators
            .OrderBy(generator => generator.Value.Name)
            .ThenBy(generator => generator.Key)
            .ToList();

        generatorDropdown.ClearOptions();
        generatorDropdown.AddOptions(new[] { VANILLA_OPTION }
            .Concat(generators.Select(generator => $"<noparse>{generator.Value.Name}</noparse> <color={GUID_COLOR}>(<noparse>{generator.Key}</noparse>)</color>"))
            .ToList());
        generatorDropdown.SetValueWithoutNotify(0);
        generatorDropdown.RefreshShownValue();

        ResetSession();
        OnGeneratorChanged();
    }

    private void OnDisable()
    {
        ResetSession();
    }

    private void OnDestroy()
    {
        if (captionMaterial != null)
            Destroy(captionMaterial);
    }

    private void ResetSession()
    {
        revealed = false;
        spoilerAccepted = false;
        seedChangedAt = null;
        preview.Clear();
        CloseDetails();
        settingsDialog.Close();
        simulation?.Cancel();
        simulatedWorld = null;
        simulationProgress = null;
        ClearLocationIcons();
    }

    private void Update()
    {
        if (seedChangedAt is { } changedAt && Time.unscaledTime - changedAt >= SEED_DEBOUNCE_SECONDS)
        {
            seedChangedAt = null;
            RequestPreview();
        }

        preview.Tick();
        ReportFailure();
        NavigateDetails();
        RefreshPreview();
        RefreshDetails();
        RefreshButtons();
    }

    private void OnSeedChanged()
    {
        if (!revealed)
            return;

        preview.Clear();
        seedChangedAt = Time.unscaledTime;
    }

    private void OnGeneratorChanged()
    {
        var worldgen = SelectedWorldgen;
        settings = worldgen?.GetDefaultSettings();

        worldSettingsSummary.text = "";
        generatorSettingsSummary.text = settings?.Summary() ?? "";

        if (revealed)
        {
            seedChangedAt = null;
            RequestPreview();
        }
    }

    private void OpenGeneratorSettings()
    {
        if (SelectedWorldgen is not ISettingsHandler handler || settings == null)
            return;

        ISettingsForm form;
        try
        {
            form = handler.CreateForm(settings);
        }
        catch (Exception e)
        {
            Logger.LogError($"{SelectedWorldgenGuid} failed to create its settings form: {e}");
            return;
        }

        settingsDialog.Open(Translations.Get("$wh_settings_title", SelectedWorldgen.Name), form, OnGeneratorSettingsAccepted);
    }

    private void OnGeneratorSettingsAccepted(ISettings accepted)
    {
        settings = accepted;
        generatorSettingsSummary.text = accepted.Summary();

        if (revealed)
        {
            seedChangedAt = null;
            RequestPreview();
        }
    }

    private void OnPreviewClicked()
    {
        if (revealed)
            return;

        if (spoilerAccepted)
        {
            Reveal();
            return;
        }

        UnifiedPopup.Push(new YesNoPopup(Translations.Get(SPOILER_HEADER), Translations.Get(SPOILER_TEXT), () =>
        {
            UnifiedPopup.Pop();
            spoilerAccepted = true;
            Reveal();
        }, UnifiedPopup.Pop, localizeText: false));
    }

    private void Reveal()
    {
        revealed = true;
        RequestPreview();
    }

    private void Hide()
    {
        revealed = false;
        seedChangedAt = null;
        preview.Clear();
        CloseDetails();
        settingsDialog.Close();
    }

    private void RequestPreview()
    {
        var seed = seedField.text;
        if (string.IsNullOrEmpty(seed))
        {
            preview.Clear();
            return;
        }

        var worldgen = SelectedWorldgen;
        var worldSettings = settings;
        previewSeed = seed;

        if (worldgen == null)
        {
            preview.Request(() => WorldGeneration.Of(VanillaWorldState.Generate(seed)));
            return;
        }

        var seedHash = seed.GetStableHashCode();
        preview.Request(() => WorldGeneration.Start(worldgen, seedHash, worldSettings));
    }

    private void OpenDetails()
    {
        var available = ((RectTransform)detailsOverlay.transform).rect.size * DETAILS_SCREEN_FRACTION;
        var frame = detailsFrame.sizeDelta;
        var scale = Mathf.Min(1f, available.x / frame.x, available.y / frame.y);
        detailsFrame.localScale = new Vector3(scale, scale, 1f);

        detailsView = PreviewView.Whole;
        gamepadPan = Vector2.zero;
        detailsOverlay.SetActive(true);
    }

    // Experimental: logs where the priority locations of the previewed world would be placed
    private void SimulateLocations()
    {
        if (preview.World is not { } world || world == simulatedWorld || previewSeed == null)
            return;

        simulatedWorld = world;
        simulation?.Cancel();
        simulation = new CancellationTokenSource();
        ClearLocationIcons();
        StartCoroutine(RunSimulation(LocationSimulation.CreateGenerator(previewSeed, world), world, simulation.Token));
    }

    // A simulation that ends without showing anything leaves the button available to try again
    private IEnumerator RunSimulation(WorldGenerator generator, GeneratedWorld world, CancellationToken token)
    {
        var progress = new LocationSimulation.Progress();
        simulationProgress = progress;

        yield return LocationSimulation.Run(generator, progress, ShowLocations, token);

        if (simulationProgress != progress)
            yield break;

        simulationProgress = null;
        if (locationIcons.Count == 0 && simulatedWorld == world)
            simulatedWorld = null;
    }

    private void ShowLocations(List<LocationSimulation.Placed> placed)
    {
        if (preview.World != simulatedWorld)
            return;

        foreach (var location in placed)
        {
            var icon = new GameObject(location.name, typeof(RectTransform), typeof(RawImage));
            icon.transform.SetParent(detailsInput.transform, false);
            var image = icon.GetComponent<RawImage>();
            image.texture = location.icon;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.sizeDelta = LOCATION_ICON_SIZE * (1f + 2f * IconOutline.MARGIN) * Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            locationIcons.Add((rect, location.position));

            if (location.caption != null)
                AddCaption(rect, location.caption);
        }
    }

    private void AddCaption(RectTransform icon, string text)
    {
        var caption = Instantiate(captionTemplate, icon, false);
        caption.name = "Caption";
        if (caption.GetComponent<Localize>() is { } localize)
            DestroyImmediate(localize);

        caption.fontSharedMaterial = CaptionMaterial();
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

    private Material CaptionMaterial()
    {
        if (captionMaterial != null)
            return captionMaterial;

        captionMaterial = new Material(captionTemplate.fontSharedMaterial);
        // Dilating the face by the outline's width keeps the outline outside the glyph, as the game's own outlined fonts do
        captionMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, CAPTION_OUTLINE_WIDTH);
        captionMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, CAPTION_OUTLINE_WIDTH);
        captionMaterial.SetColor(ShaderUtilities.ID_OutlineColor, IconOutline.Ink);
        return captionMaterial;
    }

    private void ClearLocationIcons()
    {
        foreach (var (icon, _) in locationIcons)
            Destroy(icon.gameObject);
        locationIcons.Clear();
    }

    private void PlaceLocationIcons()
    {
        if (preview.World != simulatedWorld || preview.WorldRadius is not { } radius)
        {
            ClearLocationIcons();
            return;
        }

        foreach (var (icon, position) in locationIcons)
        {
            var at = detailsView.Normalized(new Vector2(position.x, position.z), radius);
            icon.anchorMin = icon.anchorMax = at;
        }
    }

    private void PanDetails(Vector2 offset)
    {
        detailsView = detailsDragStart.Panned(ToDetailsPixels(offset));
    }

    private void ZoomDetails(Vector2 at, float scroll)
    {
        if (scroll != 0f)
            detailsView = detailsView.Zoomed(ToDetailsPixels(at) + 0.5f * PreviewView.SIZE * Vector2.one, scroll > 0f);
    }

    // Pans at a constant screen speed whatever the zoom; the remainder carries over since a view moves by whole pixels
    private void NavigateDetails()
    {
        if (!detailsOverlay.activeSelf || !detailsGroup.IsActive)
            return;

        var stick = ZInput.GetJoyLeftStick();
        if (stick.magnitude > GAMEPAD_DEAD_ZONE)
        {
            gamepadPan -= stick * (GAMEPAD_PAN_PIXELS_PER_SECOND * Time.unscaledDeltaTime);
            var pixels = new Vector2(Mathf.Round(gamepadPan.x), Mathf.Round(gamepadPan.y));
            gamepadPan -= pixels;
            detailsView = detailsView.Panned(pixels);
        }

        var zoomIn = ZInput.GetButtonDown("JoyMapZoomIn");
        if (zoomIn || ZInput.GetButtonDown("JoyMapZoomOut"))
            detailsView = detailsView.Zoomed(0.5f * PreviewView.SIZE * Vector2.one, zoomIn);
    }

    private Vector2 ToDetailsPixels(Vector2 local)
    {
        return local * (PreviewView.SIZE / ((RectTransform)detailsInput.transform).rect.width);
    }

    private void CloseDetails()
    {
        detailsOverlay.SetActive(false);
    }

    private void ReportFailure()
    {
        var failure = preview.GetFailure();
        if (failure == null || failure == reportedFailure)
            return;

        reportedFailure = failure;
        Logger.LogError($"World preview failed: {failure}");
    }

    private void RefreshPreview()
    {
        var phase = preview.CurrentPhase;
        var waiting = revealed && (phase == WorldPreview.Phase.Generating || seedChangedAt != null);

        if (preview.Main is { } render && phase is WorldPreview.Phase.Sampling or WorldPreview.Phase.Done)
        {
            previewTexture!.Show(render);
            previewImage.texture = previewTexture.texture;
        }
        else
        {
            previewImage.texture = discTexture;
        }

        previewLabel.text = Translations.Get(!revealed ? CLICK_TO_REVEAL
            : waiting ? GENERATING
            : phase == WorldPreview.Phase.Failed ? FAILED
            : phase == WorldPreview.Phase.Idle ? ENTER_SEED
            : "");

        var generationProgress = waiting ? preview.GenerationProgress : null;

        progress.SetActive(generationProgress != null);
        if (generationProgress is { } fraction)
        {
            progressFill.fillAmount = fraction;
            progressPercent.text = $"{Mathf.FloorToInt(fraction * 100f)}%";
        }

        spinner.gameObject.SetActive(waiting && generationProgress == null);
        if (waiting && generationProgress == null)
            spinner.Rotate(0f, 0f, SPINNER_DEGREES_PER_SECOND * Time.unscaledDeltaTime);
    }

    private void RefreshDetails()
    {
        if (!detailsOverlay.activeSelf)
            return;

        detailsRing.SetActive(detailsView.IsWhole);
        PlaceLocationIcons();

        var render = preview.EnsureDetails(detailsView);
        var whole = preview.WholeDetails;

        detailsWholeImage.enabled = whole != null;
        if (whole != null)
        {
            detailsWholeTexture!.Show(whole);
            detailsWholeImage.texture = detailsWholeTexture.texture;
            Place(detailsWholeImage, whole.view.PlacementIn(detailsView));
        }

        detailsImage.enabled = render == null || render != whole;
        if (render != null)
        {
            detailsTexture!.Show(render);
            detailsImage.texture = detailsTexture.texture;
            Place(detailsImage, render.view.PlacementIn(detailsView));
        }
        else
        {
            detailsImage.texture = detailsPlaceholder;
            Place(detailsImage, new Rect(0f, 0f, 1f, 1f));
        }
    }

    private static void Place(RawImage image, Rect placement)
    {
        image.rectTransform.anchorMin = placement.min;
        image.rectTransform.anchorMax = placement.max;
    }

    private void RefreshButtons()
    {
        var worldgen = SelectedWorldgen;

        hideButton.interactable = revealed;
        detailsButton.interactable = revealed && preview.CanShowDetails;
        locationsButton.interactable = preview.World != null && preview.World != simulatedWorld;
        locationsLabel.text = simulationProgress is { } progress
            ? Translations.Get(LOCATIONS_PROGRESS, Mathf.FloorToInt(progress.Fraction * 100f).ToString())
            : Translations.Get(LOCATIONS);
        worldSettingsButton.interactable = worldgen != null;
        generatorSettingsButton.interactable = worldgen is ISettingsHandler;
    }
}
