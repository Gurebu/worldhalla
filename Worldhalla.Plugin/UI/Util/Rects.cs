using UnityEngine;

namespace Worldhalla.Plugin.UI.Util;

public static class Rects
{
    private static readonly Vector2 Centre = new(0.5f, 0.5f);
    private static readonly Vector2 TopCentre = new(0.5f, 1f);

    // x and top are measured from the parent's centre
    public static RectTransform Place(Transform transform, float x, float top, float width, float height)
    {
        return Place(transform, Centre, x, top, width, height);
    }

    // x is measured from the parent's centre, top from its top edge
    public static RectTransform PlaceFromTop(Transform transform, float x, float top, float width, float height)
    {
        return Place(transform, TopCentre, x, top, width, height);
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public static void ScaleToFit(RectTransform frame, RectTransform screen)
    {
        var available = screen.rect.size * Theme.OVERLAY_SCREEN_FRACTION;
        var size = frame.sizeDelta;
        var scale = Mathf.Min(1f, available.x / size.x, available.y / size.y);
        frame.localScale = new Vector3(scale, scale, 1f);
    }

    private static RectTransform Place(Transform transform, Vector2 anchor, float x, float top, float width, float height)
    {
        var rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = TopCentre;
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, top);
        return rect;
    }
}
