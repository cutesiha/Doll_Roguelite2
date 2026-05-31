using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ExitRoomUI : MonoBehaviour
{
    [SerializeField] string mapSceneName = "MapScene";

    void Start()
    {
        BuildButton();
    }

    void BuildButton()
    {
        // Canvas
        var canvasGO = new GameObject("ExitRoomCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // 버튼
        var btnGO = new GameObject("ExitButton");
        btnGO.transform.SetParent(canvasGO.transform, false);

        var img = btnGO.AddComponent<Image>();
        img.color = new Color(0.75f, 0.15f, 0.15f, 0.95f);

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;
        var cols = btn.colors;
        cols.highlightedColor = new Color(1f, 0.35f, 0.35f, 1f);
        cols.pressedColor     = new Color(0.5f, 0.05f, 0.05f, 1f);
        btn.colors = cols;
        btn.onClick.AddListener(ExitRoom);

        // 오른쪽 위 고정
        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(1f, 1f);
        rt.sizeDelta        = new Vector2(60f, 60f);
        rt.anchoredPosition = new Vector2(-20f, -20f);

        // X 텍스트
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = "X";
        tmp.fontSize  = 30;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        var labelRt = labelGO.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;
    }

    void ExitRoom()
    {
        MapRunState.CompletePendingRoom();
        SceneManager.LoadScene(mapSceneName);
    }
}
