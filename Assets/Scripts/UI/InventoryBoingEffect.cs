using System.Collections;
using UnityEngine;

// task5: 인벤토리 슬롯 클릭 / 아이템 배치 시 "또잉" 스케일 애니메이션
[RequireComponent(typeof(RectTransform))]
public class InventoryBoingEffect : MonoBehaviour
{
    RectTransform rect;
    Coroutine boingRoutine;
    Vector3 originalScale;
    bool hasOriginalScale;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    public void PlayBoing()
    {
        if (rect == null) rect = GetComponent<RectTransform>();
        if (!hasOriginalScale)
        {
            originalScale = rect.localScale;
            hasOriginalScale = true;
        }
        if (boingRoutine != null)
            StopCoroutine(boingRoutine);
        boingRoutine = StartCoroutine(BoingRoutine());
    }

    IEnumerator BoingRoutine()
    {
        float duration = 0.28f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            // t: 0→1, 스케일 곡선: 1 → 1.18 → 0.88 → 1
            float scale = ScaleCurve(t);
            rect.localScale = originalScale * scale;
            yield return null;
        }
        rect.localScale = originalScale;
        boingRoutine = null;
    }

    static float ScaleCurve(float t)
    {
        // 0: 1.0, 0.25: 1.18, 0.6: 0.88, 1.0: 1.0
        if (t < 0.25f) return Mathf.Lerp(1f, 1.18f, t / 0.25f);
        if (t < 0.6f)  return Mathf.Lerp(1.18f, 0.88f, (t - 0.25f) / 0.35f);
        return Mathf.Lerp(0.88f, 1f, (t - 0.6f) / 0.4f);
    }
}
