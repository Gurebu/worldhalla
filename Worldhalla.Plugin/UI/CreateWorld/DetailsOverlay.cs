using UnityEngine;
using UnityEngine.UI;
using Worldhalla.Plugin.UI.Preview;
using Worldhalla.Plugin.UI.Util;

namespace Worldhalla.Plugin.UI.CreateWorld;

public class DetailsOverlay : MonoBehaviour
{
    private const float GAMEPAD_PAN_PIXELS_PER_SECOND = 600f;
    private const float GAMEPAD_DEAD_ZONE = 0.2f;

    public RectTransform frame = null!;
    public RawImage image = null!;
    public RawImage wholeImage = null!;
    public DragAndScroll input = null!;
    public UIGroupHandler group = null!;
    public GameObject ring = null!;
    public Button closeButton = null!;
    public LocationsLayer locations = null!;

    private PreviewTexture texture = null!;
    private PreviewTexture wholeTexture = null!;
    private Texture placeholder = null!;
    private PreviewView view = PreviewView.Whole;
    private PreviewView dragStart;
    private Vector2 gamepadPan;

    public void Initialize()
    {
        texture = new PreviewTexture(PreviewView.SIZE);
        wholeTexture = new PreviewTexture(PreviewView.SIZE);
        placeholder = image.texture;

        closeButton.onClick.AddListener(Close);
        input.DragStarted += () => dragStart = view;
        input.Dragged += Pan;
        input.Scrolled += Zoom;

        gameObject.SetActive(false);
    }

    public void Open()
    {
        locations.ShowAvailability();
        Rects.ScaleToFit(frame, (RectTransform)transform);

        view = PreviewView.Whole;
        gamepadPan = Vector2.zero;
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void Refresh(WorldPreview preview)
    {
        if (!gameObject.activeSelf)
            return;

        Navigate();

        ring.SetActive(view.IsWhole);
        locations.Place(view);

        var render = preview.EnsureDetails(view);
        var whole = preview.WholeDetails;

        wholeImage.enabled = whole != null;
        if (whole != null)
        {
            wholeTexture.Show(whole);
            wholeImage.texture = wholeTexture.texture;
            Place(wholeImage, whole.view.PlacementIn(view));
        }

        image.enabled = render == null || render != whole;
        if (render != null)
        {
            texture.Show(render);
            image.texture = texture.texture;
            Place(image, render.view.PlacementIn(view));
        }
        else
        {
            image.texture = placeholder;
            Place(image, new Rect(0f, 0f, 1f, 1f));
        }
    }

    private void Pan(Vector2 offset)
    {
        view = dragStart.Panned(ToPixels(offset));
    }

    private void Zoom(Vector2 at, float scroll)
    {
        if (scroll != 0f)
            view = view.Zoomed(ToPixels(at) + 0.5f * PreviewView.SIZE * Vector2.one, scroll > 0f);
    }

    // Pans at a constant screen speed whatever the zoom; the remainder carries over since a view moves by whole pixels
    private void Navigate()
    {
        if (!group.IsActive)
            return;

        var stick = ZInput.GetJoyLeftStick();
        if (stick.magnitude > GAMEPAD_DEAD_ZONE)
        {
            gamepadPan -= stick * (GAMEPAD_PAN_PIXELS_PER_SECOND * Time.unscaledDeltaTime);
            var pixels = new Vector2(Mathf.Round(gamepadPan.x), Mathf.Round(gamepadPan.y));
            gamepadPan -= pixels;
            view = view.Panned(pixels);
        }

        var zoomIn = ZInput.GetButtonDown("JoyMapZoomIn");
        if (zoomIn || ZInput.GetButtonDown("JoyMapZoomOut"))
            view = view.Zoomed(0.5f * PreviewView.SIZE * Vector2.one, zoomIn);
    }

    private Vector2 ToPixels(Vector2 local)
    {
        return local * (PreviewView.SIZE / ((RectTransform)input.transform).rect.width);
    }

    private static void Place(RawImage image, Rect placement)
    {
        image.rectTransform.anchorMin = placement.min;
        image.rectTransform.anchorMax = placement.max;
    }
}
