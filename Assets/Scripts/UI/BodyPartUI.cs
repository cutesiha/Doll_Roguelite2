using UnityEngine;

[ExecuteAlways]
public class BodyPartUI : MonoBehaviour
{
    [SerializeField] int fontSize = 22;
    [SerializeField] Color textColor = Color.white;
    [SerializeField] Color shadowColor = new Color(0f, 0f, 0f, 0.8f);
    [SerializeField] Rect displayRect = new Rect(10, 10, 320, 300);

    GUIStyle bodyStyle;
    GUIStyle shadowStyle;
    int cachedFontSize;

    void InitStyles()
    {
        // 폰트 크기 변경 시 재생성
        if (bodyStyle != null && cachedFontSize == fontSize) return;

        cachedFontSize = fontSize;
        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            richText = true
        };
        bodyStyle.normal.textColor = textColor;

        shadowStyle = new GUIStyle(bodyStyle);
        shadowStyle.normal.textColor = shadowColor;
    }

    void OnGUI()
    {
        InitStyles();

        // 에디터 프리뷰: BodyManager 없으면 더미 상태 표시
        BodyState s = BodyManager.Instance != null
            ? BodyManager.Instance.State
            : new BodyState();

        string Y = "O";
        string N = "<color=#FF4444>X</color>";

        string text =
            "[ 현재 몸 상태 ]\n" +
            $"머리  : {(s.head  ? "있음" : "<color=#FF4444>없음</color>")}\n" +
            $"눈알  : 왼 {(s.eyeLeft  ? Y : N)} / 오 {(s.eyeRight ? Y : N)}\n" +
            $"몸    : {(s.body  ? "있음" : "<color=#FF4444>없음</color>")}\n" +
            $"팔    : 왼 {(s.armLeft  ? Y : N)} / 오 {(s.armRight ? Y : N)}\n" +
            $"다리  : 왼 {(s.legLeft  ? Y : N)} / 오 {(s.legRight ? Y : N)}";

        GUI.Label(new Rect(displayRect.x + 1, displayRect.y + 1, displayRect.width, displayRect.height), text, shadowStyle);
        GUI.Label(displayRect, text, bodyStyle);
    }
}
