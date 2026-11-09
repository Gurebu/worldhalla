using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Worldhalla.Plugin.Localization;
using Worldhalla.Plugin.UI.Preview;

namespace Worldhalla.Plugin.UI.CreateWorld;

public class PreviewTile : MonoBehaviour
{
    private const float SPINNER_DEGREES_PER_SECOND = -270f;

    private const string SPOILER_HEADER = "$wh_spoiler_header";
    private const string SPOILER_TEXT = "$wh_spoiler_text";
    private const string CLICK_TO_REVEAL = "$wh_preview_click_to_reveal";
    private const string GENERATING = "$wh_preview_generating";
    private const string FAILED = "$wh_preview_failed";

    public RawImage image = null!;
    public Button button = null!;
    public TMP_Text label = null!;
    public RectTransform spinner = null!;
    public GameObject progress = null!;
    public Image progressFill = null!;
    public TMP_Text progressPercent = null!;
    public Texture2D discTexture = null!;
    public int size;
    public Button hideButton = null!;
    public Button detailsButton = null!;

    private PreviewTexture texture = null!;
    private bool spoilerAccepted;

    public bool IsRevealed { get; private set; }

    public event Action? Revealed;
    public event Action? Hidden;
    public event Action? DetailsRequested;

    public void Initialize()
    {
        texture = new PreviewTexture(size);

        button.onClick.AddListener(OnClicked);
        hideButton.onClick.AddListener(Hide);
        detailsButton.onClick.AddListener(() => DetailsRequested?.Invoke());
    }

    public void ResetSession()
    {
        IsRevealed = false;
        spoilerAccepted = false;
    }

    // pending: a request is about to be made, so the tile already shows as generating
    public void Refresh(WorldPreview preview, bool pending)
    {
        var phase = preview.CurrentPhase;
        var waiting = IsRevealed && (phase == WorldPreview.Phase.Generating || pending);

        if (preview.Main is { } render && phase is WorldPreview.Phase.Sampling or WorldPreview.Phase.Done)
        {
            texture.Show(render);
            image.texture = texture.texture;
        }
        else
        {
            image.texture = discTexture;
        }

        label.text = Translations.Get(!IsRevealed ? CLICK_TO_REVEAL
            : waiting ? GENERATING
            : phase == WorldPreview.Phase.Failed ? FAILED
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

        hideButton.interactable = IsRevealed;
        detailsButton.interactable = IsRevealed && preview.CanShowDetails;
    }

    private void OnClicked()
    {
        if (IsRevealed)
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
        IsRevealed = true;
        Revealed?.Invoke();
    }

    private void Hide()
    {
        IsRevealed = false;
        Hidden?.Invoke();
    }
}
