using GUIFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Worldhalla.API.Settings;
using Worldhalla.Plugin.Core;
using Worldhalla.Plugin.Localization;
using Worldhalla.Plugin.UI.Preview;
using Worldhalla.Plugin.UI.SettingsForm;
using Worldhalla.Plugin.UI.Util;

namespace Worldhalla.Plugin.UI.CreateWorld;

public class WorldCreationDialog : MonoBehaviour
{
    private const string VANILLA_OPTION = "Valheim";
    private const string GUID_COLOR = "#8a8a8a";
    private const float SEED_DEBOUNCE_SECONDS = 0.5f;
    private const float GAMEPAD_DEAD_ZONE = 0.2f;
    private const float GAMEPAD_SCROLL_PIXELS_PER_SECOND = 150f;
    private const string VANILLA_DESCRIPTION = "$wh_vanilla_description";

    public GuiInputField nameField = null!;
    public GuiInputField seedField = null!;
    public TMP_Dropdown generatorDropdown = null!;
    public ScrollRect generatorDescription = null!;
    public TMP_Text worldSettingsSummary = null!;
    public TMP_Text generatorSettingsSummary = null!;
    public Button worldSettingsButton = null!;
    public Button generatorSettingsButton = null!;
    public Button newSeedButton = null!;
    public Button doneButton = null!;
    public Button cancelButton = null!;
    public PreviewTile tile = null!;
    public DetailsOverlay details = null!;
    public LocationsLayer locations = null!;
    public SettingsDialog settingsDialog = null!;

    private NavigationGrid navigation = null!;
    private UIGroupHandler? group;
    private WorldPreview preview = null!;

    private List<RegisteredGenerator> generators = [];
    private ISettings? settings;
    private WorldhallaSettings worldhallaSettings = WorldhallaSettings.Default;
    private float? seedChangedAt;
    private Exception? reportedFailure;

    public RegisteredGenerator? SelectedWorldgen
    {
        get
        {
            var index = generatorDropdown.value - 1;
            return index >= 0 && index < generators.Count ? generators[index] : null;
        }
    }

    public ISettings? Settings => settings;

    public WorldhallaSettings WorldhallaSettings => worldhallaSettings;

    public void Initialize()
    {
        navigation = new NavigationGrid(
            [[nameField], [seedField, newSeedButton], [generatorDropdown], [worldSettingsButton], [generatorSettingsButton]],
            [[tile.button], [tile.hideButton, tile.detailsButton], [doneButton, cancelButton]]);
        group = GetComponent<UIGroupHandler>();

        preview = new WorldPreview(tile.size);
        locations.Initialize(preview);

        seedField.onValueChanged.AddListener(_ => OnSeedChanged());
        generatorDropdown.onValueChanged.AddListener(_ => OnGeneratorChanged());
        newSeedButton.interactable = true;
        newSeedButton.onClick.AddListener(() => seedField.text = World.GenerateSeed());
        tile.Revealed += RequestPreview;
        tile.Hidden += OnPreviewHidden;
        tile.DetailsRequested += details.Open;
        worldSettingsButton.onClick.AddListener(() => Logger.LogInfo("World settings screen is not implemented yet"));
        generatorSettingsButton.onClick.AddListener(OpenGeneratorSettings);
    }

    public void Open()
    {
        generators = Registry.Generators.Values
            .OrderBy(generator => generator.Name)
            .ThenBy(generator => generator.Guid)
            .ToList();

        generatorDropdown.ClearOptions();
        generatorDropdown.AddOptions(new[] { VANILLA_OPTION }
            .Concat(generators.Select(generator => $"<noparse>{generator.Name}</noparse> <color={GUID_COLOR}>(<noparse>{generator.Guid}</noparse>)</color>"))
            .ToList());
        generatorDropdown.SetValueWithoutNotify(generators.FindIndex(generator => generator.Guid == NewWorldDefaults.WorldgenGuid) + 1);
        generatorDropdown.RefreshShownValue();

        ResetSession();
        worldhallaSettings = WorldhallaSettings.Default;
        OnGeneratorChanged();
    }

    private void OnDisable()
    {
        ResetSession();
    }

    private void ResetSession()
    {
        tile.ResetSession();
        seedChangedAt = null;
        preview.Clear();
        details.Close();
        settingsDialog.Close();
        locations.ResetSession();
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
        tile.Refresh(preview, seedChangedAt != null);
        details.Refresh(preview);
        ScrollDescription();
        RefreshButtons();
        navigation.Apply();
    }

    private void OnSeedChanged()
    {
        if (!tile.IsRevealed)
            return;

        preview.Clear();
        seedChangedAt = Time.unscaledTime;
    }

    private void OnGeneratorChanged()
    {
        var worldgen = SelectedWorldgen;
        settings = worldgen != null ? InitialSettings(worldgen) : null;

        generatorDescription.content.GetComponent<TMP_Text>().text = worldgen != null ? worldgen.Description : Translations.Get(VANILLA_DESCRIPTION);
        generatorDescription.content.anchoredPosition = Vector2.zero;
        worldSettingsSummary.text = "";
        generatorSettingsSummary.text = settings?.Summary() ?? "";

        RequestPreviewIfRevealed();
    }

    private static ISettings? InitialSettings(RegisteredGenerator worldgen)
    {
        if (worldgen.Guid != NewWorldDefaults.WorldgenGuid || NewWorldDefaults.Settings is not { } text || worldgen.SettingsHandler is not { } handler)
            return worldgen.DefaultSettings;

        try
        {
            var configured = handler.BuildSettingsFromCommandLine(text);
            WorldPayload.CheckSettings(configured);

            return configured;
        }
        catch (Exception e)
        {
            Logger.LogWarning($"Configured settings '{text}' for worldgen '{worldgen.Guid}' can't be used, starting from its defaults: {e.Message}");
            return worldgen.DefaultSettings;
        }
    }

    private void OpenGeneratorSettings()
    {
        if (SelectedWorldgen is not { SettingsHandler: { } handler } || settings == null)
            return;

        ISettingsForm form;
        try
        {
            form = handler.CreateForm(settings);
        }
        catch (Exception e)
        {
            Logger.LogError($"{SelectedWorldgen.Guid} failed to create its settings form: {e}");
            return;
        }

        settingsDialog.Open(Translations.Get("$wh_settings_title", SelectedWorldgen.Name), form.Controls, () =>
        {
            var built = form.Build();
            WorldPayload.CheckSettings(built);
            return built;
        }, OnGeneratorSettingsAccepted);
    }

    private void OnGeneratorSettingsAccepted(ISettings accepted)
    {
        settings = accepted;
        generatorSettingsSummary.text = accepted.Summary();

        RequestPreviewIfRevealed();
    }

    private void OnPreviewHidden()
    {
        seedChangedAt = null;
        preview.Clear();
        details.Close();
        settingsDialog.Close();
    }

    private void RequestPreviewIfRevealed()
    {
        if (!tile.IsRevealed)
            return;

        seedChangedAt = null;
        RequestPreview();
    }

    private void RequestPreview()
    {
        var seed = seedField.text;
        var worldgen = SelectedWorldgen;
        var worldSettings = settings;
        preview.Request(() => PreviewGeneration.Start(worldgen, seed, worldSettings));
    }

    // The right stick, as vanilla scrolls its changelog
    private void ScrollDescription()
    {
        if (group is { IsActive: false })
            return;

        var stick = ZInput.GetJoyRightStickY();
        var overflow = generatorDescription.content.rect.height - generatorDescription.viewport.rect.height;
        if (Mathf.Abs(stick) <= GAMEPAD_DEAD_ZONE || overflow <= 0f)
            return;

        var step = stick * GAMEPAD_SCROLL_PIXELS_PER_SECOND * Time.unscaledDeltaTime / overflow;
        generatorDescription.verticalNormalizedPosition = Mathf.Clamp01(generatorDescription.verticalNormalizedPosition - step);
    }

    private void ReportFailure()
    {
        var failure = preview.GetFailure();
        if (failure == null || failure == reportedFailure)
            return;

        reportedFailure = failure;
        Logger.LogError($"World preview failed: {failure}");
    }

    private void RefreshButtons()
    {
        var worldgen = SelectedWorldgen;

        locations.RefreshButton();
        worldSettingsButton.interactable = worldgen != null;
        generatorSettingsButton.interactable = worldgen?.SettingsHandler != null;
    }
}
