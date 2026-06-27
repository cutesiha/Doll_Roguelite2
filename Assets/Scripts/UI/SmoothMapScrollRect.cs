using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SmoothMapScrollRect : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] ScrollRect scrollRect;
    [SerializeField, Min(0.01f)] float wheelStep = 0.14f;
    [SerializeField, Min(1f)] float lerpSpeed = 16f;

    float targetNormalizedPosition = 1f;
    bool dragging;

    public void Configure(ScrollRect target, float step, float speed)
    {
        scrollRect = target;
        wheelStep = Mathf.Max(0.01f, step);
        lerpSpeed = Mathf.Max(1f, speed);
        SyncToCurrentPosition();
    }

    void OnEnable()
    {
        SyncToCurrentPosition();
    }

    void Update()
    {
        if (scrollRect == null || dragging)
            return;

        float current = scrollRect.verticalNormalizedPosition;
        float next = Mathf.Lerp(current, targetNormalizedPosition, 1f - Mathf.Exp(-lerpSpeed * Time.unscaledDeltaTime));
        if (Mathf.Abs(next - targetNormalizedPosition) < 0.001f)
            next = targetNormalizedPosition;

        scrollRect.verticalNormalizedPosition = next;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (scrollRect == null || !scrollRect.vertical)
            return;

        targetNormalizedPosition = Mathf.Clamp01(targetNormalizedPosition + eventData.scrollDelta.y * wheelStep);
        eventData.Use();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = true;
        SyncToCurrentPosition();
    }

    public void OnDrag(PointerEventData eventData)
    {
        SyncToCurrentPosition();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragging = false;
        SyncToCurrentPosition();
    }

    void SyncToCurrentPosition()
    {
        if (scrollRect != null)
            targetNormalizedPosition = scrollRect.verticalNormalizedPosition;
    }
}
