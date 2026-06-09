using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StartBodyPartChoice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public enum ClickAction
    {
        None,
        LoadScene,
        ShowOptionPanel,
        ShowRoadPanel,
        ShowQuitPanel
    }

    StartBodyPartSelector owner;
    RectTransform rectTransform;
    Graphic graphic;
    Vector2 homePosition;
    Vector2 selectedPosition;
    Color normalColor = Color.white;
    Color hoverColor;
    ClickAction action;
    Coroutine moveRoutine;
    float moveDuration;
    bool configured;
    bool selected;
    bool isMoving;
    const float OutwardReactionDistance = 26f;
    const float InwardReactionDistance = 22f;

    public void Configure(
        StartBodyPartSelector owner,
        Vector2 selectedPosition,
        ClickAction action,
        Color hoverColor,
        float moveDuration)
    {
        this.owner = owner;
        this.selectedPosition = selectedPosition;
        this.action = action;
        this.hoverColor = hoverColor;
        this.moveDuration = Mathf.Max(0.01f, moveDuration);

        rectTransform = GetComponent<RectTransform>();
        graphic = GetComponent<Graphic>();
        if (rectTransform != null)
            homePosition = rectTransform.anchoredPosition;

        if (graphic != null)
            normalColor = graphic.color;

        configured = true;
        selected = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!configured || graphic == null || selected)
            return;

        graphic.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!configured || graphic == null || selected)
            return;

        graphic.color = normalColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!configured || rectTransform == null || selected || isMoving)
            return;

        selected = true;
        if (graphic != null)
            graphic.color = normalColor;

        MoveToSelected(() => owner.HandleChoiceComplete(this, action));
    }

    public void ReturnHome()
    {
        ReturnHome(null);
    }

    public void ReturnHome(System.Action onComplete)
    {
        selected = false;
        if (graphic != null)
            graphic.color = normalColor;

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        if (rectTransform == null)
        {
            onComplete?.Invoke();
            return;
        }

        isMoving = true;
        moveRoutine = StartCoroutine(ReturnRoutine(onComplete));
    }

    void MoveToSelected(System.Action onComplete)
    {
        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        isMoving = true;
        moveRoutine = StartCoroutine(AttachRoutine(onComplete));
    }

    IEnumerator AttachRoutine(System.Action onComplete)
    {
        Vector2 outwardDirection = homePosition.sqrMagnitude > 0.01f ? homePosition.normalized : Vector2.up;
        Vector2 reactionPosition = homePosition + outwardDirection * OutwardReactionDistance;
        float reactionDuration = moveDuration * 0.32f;
        float attachDuration = moveDuration * 0.68f;

        yield return MoveSegment(rectTransform.anchoredPosition, reactionPosition, reactionDuration);
        yield return MoveSegment(rectTransform.anchoredPosition, selectedPosition, attachDuration);

        rectTransform.anchoredPosition = selectedPosition;
        moveRoutine = null;
        isMoving = false;
        onComplete?.Invoke();
    }

    IEnumerator MoveRoutine(Vector2 destination, float duration, System.Action onComplete)
    {
        Vector2 start = rectTransform.anchoredPosition;
        yield return MoveSegment(start, destination, duration);

        rectTransform.anchoredPosition = destination;
        moveRoutine = null;
        isMoving = false;
        onComplete?.Invoke();
    }

    IEnumerator ReturnRoutine(System.Action onComplete)
    {
        Vector2 inwardDirection = (selectedPosition - homePosition).sqrMagnitude > 0.01f
            ? (selectedPosition - homePosition).normalized
            : Vector2.zero;
        Vector2 reactionPosition = rectTransform.anchoredPosition + inwardDirection * InwardReactionDistance;
        float reactionDuration = moveDuration * 0.28f;
        float returnDuration = moveDuration * 0.72f;

        yield return MoveSegment(rectTransform.anchoredPosition, reactionPosition, reactionDuration);
        yield return MoveSegment(rectTransform.anchoredPosition, homePosition, returnDuration);

        rectTransform.anchoredPosition = homePosition;
        moveRoutine = null;
        isMoving = false;
        onComplete?.Invoke();
    }

    IEnumerator MoveSegment(Vector2 start, Vector2 destination, float duration)
    {
        isMoving = true;
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            rectTransform.anchoredPosition = Vector2.LerpUnclamped(start, destination, t);
            if (t >= 1f)
                break;

            yield return null;
        }

        rectTransform.anchoredPosition = destination;
    }
}
