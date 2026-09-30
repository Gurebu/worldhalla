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
using Logger = Worldhalla.Plugin.Internal.Logger;

namespace Worldhalla.Plugin.UI;

public class WorldCreationDialog : MonoBehaviour
{
    private const string VANILLA_OPTION = "Valheim";
    private const string GUID_COLOR = "#8a8a8a";
    private const float SEED_DEBOUNCE_SECONDS = 0.5f;
    private const float SPINNER_DEGREES_PER_SECOND = -270f;
    private const float DETAILS_SCREEN_FRACTION = 0.95f;

    private const string SPOILER_HEADER = "$wh_spoiler_header";
    private const string SPOILER_TEXT = "$wh_spoiler_text";
    private const string CLICK_TO_REVEAL = "$wh_preview_click_to_reveal";
    private const string ENTER_SEED = "$wh_preview_enter_seed";
    private const string GENERATING = "$wh_preview_generating";
    private const string FAILED = "$wh_preview_failed";

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
    internal Button detailsCloseButton = null!;
    internal SettingsDialog settingsDialog = null!;

    private WorldPreview preview = null!;
    private PreviewTexture? previewTexture;
    private PreviewTexture? detailsTexture;
    private Texture? detailsPlaceholder;

    private List<KeyValuePair<string, IWorldGenerator>> generators = [];
    private ISettings? settings;
    private bool revealed;
    private bool spoilerAccepted;
    private float? seedChangedAt;
    private Exception? reportedFailure;

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
        detailsTexture = new PreviewTexture(WorldPreview.DETAILS_SIZE);
        detailsPlaceholder = detailsImage.texture;

        seedField.onValueChanged.AddListener(_ => OnSeedChanged());
        generatorDropdown.onValueChanged.AddListener(_ => OnGeneratorChanged());
        newSeedButton.interactable = true;
        newSeedButton.onClick.AddListener(() => seedField.text = World.GenerateSeed());
        previewButton.onClick.AddListener(OnPreviewClicked);
        hideButton.onClick.AddListener(Hide);
        detailsButton.onClick.AddListener(OpenDetails);
        detailsCloseButton.onClick.AddListener(CloseDetails);
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

    private void ResetSession()
    {
        revealed = false;
        spoilerAccepted = false;
        seedChangedAt = null;
        preview.Clear();
        CloseDetails();
        settingsDialog.Close();
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

        detailsOverlay.SetActive(true);
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

        if (preview.EnsureDetails() is { } render)
        {
            detailsTexture!.Show(render);
            detailsImage.texture = detailsTexture.texture;
        }
        else
        {
            detailsImage.texture = detailsPlaceholder;
        }
    }

    private void RefreshButtons()
    {
        var worldgen = SelectedWorldgen;

        hideButton.interactable = revealed;
        detailsButton.interactable = revealed && preview.CanShowDetails;
        worldSettingsButton.interactable = worldgen != null;
        generatorSettingsButton.interactable = worldgen is ISettingsHandler;
    }
}
