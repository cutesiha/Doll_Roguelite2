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
        var canvasGO = new GameObject("ExitRoomCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var btnGO = new GameObject("ExitButton");
        btnGO.transform.SetParent(canvasGO.transform, false);

        var img = btnGO.AddComponent<Image>();
        img.color = new Color(0.75f, 0.15f, 0.15f, 0.95f);

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(1f, 0.35f, 0.35f, 1f);
        colors.pressedColor = new Color(0.5f, 0.05f, 0.05f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(ExitRoom);

        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(60f, 60f);
        rt.anchoredPosition = new Vector2(-20f, -20f);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);

        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text = "X";
        label.fontSize = 30f;
        label.font = UIThinDungFont.Get();
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;

        var labelRt = labelGO.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
    }

    void ExitRoom()
    {
        MapRunState.CompletePendingRoom();
        SceneManager.LoadScene(mapSceneName);
    }
}
