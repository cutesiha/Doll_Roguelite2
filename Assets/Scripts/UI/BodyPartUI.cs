using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class BodyPartUI : MonoBehaviour
{
    [SerializeField] TMP_FontAsset font;
    [SerializeField] float fontSize      = 18f;
    [SerializeField] Color textColor     = Color.white;
    [SerializeField] Color shadowColor   = new Color(0f, 0f, 0f, 0.8f);
    [SerializeField] Vector2 anchoredPos = new Vector2(10f, -10f);
    [SerializeField] Vector2 rectSize    = new Vector2(320f, 300f);

    TextMeshProUGUI label;
    Shadow          labelShadow;

    void OnEnable()  => BuildUI();
    void OnDisable() => DestroyUI();

    void OnValidate() => ApplyProperties();

    void BuildUI()
    {
        if (label != null) return;

        var canvasGO = new GameObject("BodyStatusCanvas");
        canvasGO.hideFlags = HideFlags.HideAndDontSave;
        canvasGO.transform.SetParent(transform);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var textGO = new GameObject("BodyStatusText");
        textGO.hideFlags = HideFlags.HideAndDontSave;
        textGO.transform.SetParent(canvasGO.transform, false);

        label = textGO.AddComponent<TextMeshProUGUI>();
        label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        label.raycastTarget    = false;

        var rt = label.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);

        labelShadow = textGO.AddComponent<Shadow>();

        ApplyProperties();
    }

    void DestroyUI()
    {
        if (label == null) return;
        var canvasGO = label.transform.parent.gameObject;
        label       = null;
        labelShadow = null;
        if (Application.isPlaying) Destroy(canvasGO);
        else DestroyImmediate(canvasGO);
    }

    void ApplyProperties()
    {
        if (label == null) return;

        label.fontSize = fontSize;
        label.color    = textColor;
        TMP_FontAsset fontAsset = UIThinDungFont.Get(font);
        if (fontAsset != null) label.font = fontAsset;

        var rt = label.GetComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = rectSize;

        if (labelShadow != null)
        {
            labelShadow.effectColor    = shadowColor;
            labelShadow.effectDistance = new Vector2(1f, -1f);
        }
    }

    void Update()
    {
        if (label == null) return;

        BodyState s = BodyManager.Instance != null
            ? BodyManager.Instance.State
            : new BodyState();

        string Y = "O";
        string N = "<color=#FF4444>X</color>";

        label.text =
            "[ 현재 몸 상태 ]\n" +
            $"머리  : {(s.head     ? "있음" : "<color=#FF4444>없음</color>")}\n" +
            $"눈알  : 왼 {(s.eyeLeft  ? Y : N)} / 오 {(s.eyeRight ? Y : N)}\n" +
            $"몸    : {(s.body     ? "있음" : "<color=#FF4444>없음</color>")}\n" +
            $"팔    : 왼 {(s.armLeft  ? Y : N)} / 오 {(s.armRight ? Y : N)}\n" +
            $"다리  : 왼 {(s.legLeft  ? Y : N)} / 오 {(s.legRight ? Y : N)}";
    }
}
