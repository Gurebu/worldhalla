using UnityEngine;
using UnityEngine.EventSystems;

namespace Worldhalla.Plugin.UI.Preview;

// Positions are local to the rect and relative to its centre; a drag reports its offset from where it started
internal sealed class DragAndScroll : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
{
    private Vector2 dragStart;

    public event Action? DragStarted;
    public event Action<Vector2>? Dragged;
    public event Action<Vector2, float>? Scrolled;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!ToLocal(eventData.pressPosition, eventData.pressEventCamera, out dragStart))
            return;

        DragStarted?.Invoke();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ToLocal(eventData.position, eventData.pressEventCamera, out var now))
            Dragged?.Invoke(now - dragStart);
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (ToLocal(eventData.position, eventData.enterEventCamera, out var at))
            Scrolled?.Invoke(at, eventData.scrollDelta.y);
    }

    private bool ToLocal(Vector2 screen, Camera camera, out Vector2 local)
    {
        var rect = (RectTransform)transform;
        var hit = RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, camera, out local);
        local -= rect.rect.center;
        return hit;
    }
}
