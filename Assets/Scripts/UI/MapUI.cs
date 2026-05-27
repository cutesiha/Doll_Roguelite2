using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

[ExecuteAlways]
public class MapUI : MonoBehaviour
{
    [SerializeField] float layerSpacing  = 4f;
    [SerializeField] float mapWidth      = 12f;
    [SerializeField] float nodeRadius    = 0.5f;
    [SerializeField] float camLerpSpeed  = 5f;
    [FormerlySerializedAs("labelFontSize")]
    [SerializeField, Min(0.01f)] float conditionLabelFontSize = 3f;
    [SerializeField, Min(0.01f)] float feedbackLabelFontSize  = 3f;
    [SerializeField] TMP_FontAsset mapTextFont;
    [SerializeField] string roomSceneName = "RoomScene";
    [SerializeField] string bossSceneName = "BossScene";
    [SerializeField, Min(0f)] float conditionFeedbackDelay = 0.6f;

    // 노드 색상
    static readonly Color ColCurrent    = new Color(1.00f, 0.65f, 0.10f, 1f);
    static readonly Color ColCleared    = new Color(0.20f, 0.20f, 0.20f, 1f);
    static readonly Color ColFree       = new Color(0.25f, 0.80f, 0.35f, 1f); // 초록
    static readonly Color ColNoLeftArm  = new Color(0.85f, 0.20f, 0.15f, 1f); // 빨강
    static readonly Color ColNoRightEye = new Color(0.65f, 0.20f, 0.85f, 1f); // 보라
    static readonly Color ColNoLeftLeg  = new Color(1.00f, 0.50f, 0.20f, 1f); // 주황
    static readonly Color ColNoRightLeg = new Color(0.20f, 0.60f, 1.00f, 1f); // 파랑
    static readonly Color ColBoss       = new Color(0.90f, 0.75f, 0.10f, 1f); // 금
    static readonly Color ColRouteOnly  = new Color(0.45f, 0.45f, 0.45f, 1f);
    static readonly Color ColHidden     = new Color(0.22f, 0.22f, 0.22f, 1f); // 짙은 회색
    static readonly Color ColLine       = new Color(0.40f, 0.40f, 0.40f, 1f);

    Sprite circleSprite;
    readonly Dictionary<MapNode, SpriteRenderer>  nodeRenderers = new Dictionary<MapNode, SpriteRenderer>();
    readonly Dictionary<MapNode, CircleCollider2D> nodeColliders = new Dictionary<MapNode, CircleCollider2D>();
    readonly Dictionary<MapNode, TextMeshPro>      nodeLabels    = new Dictionary<MapNode, TextMeshPro>();
    readonly List<(LineRenderer lr, MapNode from, MapNode to)> lines = new List<(LineRenderer lr, MapNode from, MapNode to)>();

    TextMeshPro feedbackLabel;
    float targetCamY;
    bool enteringRoom;

    // ── 조건 판정 ────────────────────────────────────────────────────────
    static bool CanPass(NodeConditionType cond, BodyState s)
    {
        if (s == null) return true;
        switch (cond)
        {
            case NodeConditionType.Free:       return true;
            case NodeConditionType.NoLeftArm:  return !s.armLeft;
            case NodeConditionType.NoRightEye: return !s.eyeRight;
            case NodeConditionType.NoLeftLeg:  return !s.legLeft;
            case NodeConditionType.NoRightLeg: return !s.legRight;
            case NodeConditionType.Boss:       return true;
            default:                           return true;
        }
    }

    static string ConditionText(NodeConditionType cond)
    {
        switch (cond)
        {
            case NodeConditionType.Free:       return "FREE";
            case NodeConditionType.NoLeftArm:  return "NO LEFT ARM";
            case NodeConditionType.NoRightEye: return "NO RIGHT EYE";
            case NodeConditionType.NoLeftLeg:  return "NO LEFT LEG";
            case NodeConditionType.NoRightLeg: return "NO RIGHT LEG";
            case NodeConditionType.Boss:       return "BOSS";
            default:                           return "";
        }
    }
    // ─────────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        Cleanup();
        if (MapManager.Instance == null) return;

        circleSprite = MakeCircleSprite(64);
        MapManager.Instance.OnMapChanged += Refresh;
        Build(MapManager.Instance.Root);
        Refresh();
    }

    void OnDisable()
    {
        if (MapManager.Instance != null)
            MapManager.Instance.OnMapChanged -= Refresh;
        Cleanup();
    }

    void OnValidate()
    {
        ApplyTextSizes();
        if (!Application.isPlaying)
        {
            if (MapManager.Instance != null)
            {
                if (circleSprite == null)
                    circleSprite = MakeCircleSprite(64);
                Build(MapManager.Instance.Root);
                Refresh();
            }
        }
    }

    void Cleanup()
    {
        nodeRenderers.Clear();
        nodeColliders.Clear();
        nodeLabels.Clear();
        lines.Clear();
        feedbackLabel = null;
        enteringRoom = false;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }

    void Build(MapNode root)
    {
        var layers = CollectLayers(root);
        int totalLayers = layers.Count;

        float vertOrtho = layerSpacing + 0.8f;
        float maxOrtho  = (totalLayers - 2) * layerSpacing - 0.2f;
        float orthoSize = vertOrtho;
        float effectiveMapWidth = mapWidth;

        if (Camera.main != null)
        {
            float horzOrtho = mapWidth / Camera.main.aspect / 2f + 0.5f;
            orthoSize = Mathf.Min(Mathf.Max(vertOrtho, horzOrtho), maxOrtho);
            orthoSize = Mathf.Max(orthoSize, 3.5f); // layerSpacing이 작아져도 카메라가 과도하게 줌인되지 않게
            effectiveMapWidth = Mathf.Min(mapWidth, (orthoSize - 0.5f) * Camera.main.aspect * 2f);
            Camera.main.orthographicSize = orthoSize;
        }

        for (int l = 0; l < layers.Count; l++)
        {
            var layer = layers[l];
            for (int i = 0; i < layer.Count; i++)
            {
                float x = layer.Count == 1 ? 0f
                    : (i / (float)(layer.Count - 1) - 0.5f) * effectiveMapWidth;
                float y = -l * layerSpacing;
                layer[i].position = new Vector2(x, y);
                CreateNodeGO(layer[i]);
            }
        }

        foreach (var kvp in nodeRenderers)
            foreach (var child in kvp.Key.children)
                if (nodeRenderers.ContainsKey(child))
                    CreateLine(kvp.Key, child);

        CreateFeedbackLabel();

        targetCamY = -layerSpacing;
        if (Camera.main != null)
            Camera.main.transform.position = new Vector3(0f, targetCamY, -10f);
    }

    void CreateNodeGO(MapNode node)
    {
        var go = new GameObject($"Node_{node.id}");
        go.transform.SetParent(transform);
        go.transform.position = new Vector3(node.position.x, node.position.y, 0f);
        go.transform.localScale = Vector3.one * nodeRadius * 2f;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = circleSprite;
        sr.sortingOrder = 2;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;

        nodeRenderers[node] = sr;
        nodeColliders[node] = col;

        // 조건 텍스트 라벨 (MapUI 직속 자식 — 노드 스케일 상속 방지)
        var labelGO = new GameObject($"Label_{node.id}");
        labelGO.transform.SetParent(transform);
        labelGO.transform.position = new Vector3(
            node.position.x,
            node.position.y + nodeRadius + 0.25f,
            -0.1f);

        var tmp = labelGO.AddComponent<TextMeshPro>();
        tmp.alignment  = TextAlignmentOptions.Center;
        tmp.fontSize   = conditionLabelFontSize;
        tmp.color      = Color.white;
        tmp.sortingOrder = 3;
        ApplyFont(tmp);
        // 라벨이 카메라 밖으로 나가지 않도록 노드 위치 기준 안전 너비 계산
        float safeHalfW = Camera.main != null
            ? Camera.main.orthographicSize * Camera.main.aspect - Mathf.Abs(node.position.x) - 0.1f
            : 2f;
        float labelW = Mathf.Clamp(safeHalfW * 2f, 1f, 4f);
        var rt = tmp.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(labelW, 0.8f);
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = conditionLabelFontSize;
        tmp.fontSizeMin = conditionLabelFontSize * 0.4f;
        labelGO.SetActive(false);

        nodeLabels[node] = tmp;
    }

    void CreateLine(MapNode from, MapNode to)
    {
        var go = new GameObject($"Line_{from.id}_{to.id}");
        go.transform.SetParent(transform);

        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, new Vector3(from.position.x, from.position.y, 0.1f));
        lr.SetPosition(1, new Vector3(to.position.x,   to.position.y,   0.1f));
        lr.startWidth = lr.endWidth = 0.05f;
        lr.useWorldSpace = true;
        lr.sortingOrder  = 1;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", ColLine);
        lr.material = mat;

        lines.Add((lr, from, to));
    }

    void CreateFeedbackLabel()
    {
        var go = new GameObject("FeedbackLabel");
        go.transform.SetParent(transform);

        feedbackLabel = go.AddComponent<TextMeshPro>();
        feedbackLabel.alignment  = TextAlignmentOptions.Center;
        feedbackLabel.fontSize   = feedbackLabelFontSize;
        feedbackLabel.sortingOrder = 4;
        ApplyFont(feedbackLabel);
        feedbackLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(2f, 1f);
        go.SetActive(false);
    }

    void Update()
    {
        if (Application.isPlaying && Camera.main != null)
        {
            var pos = Camera.main.transform.position;
            pos.y = Mathf.Lerp(pos.y, targetCamY, Time.deltaTime * camLerpSpeed);
            Camera.main.transform.position = pos;
        }

        if (!Application.isPlaying) return;
        if (enteringRoom) return;
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

        var worldPos = (Vector2)Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        var hit = Physics2D.OverlapPoint(worldPos);
        if (hit == null) return;

        foreach (var kvp in nodeColliders)
        {
            if (kvp.Value != hit) continue;

            var node = kvp.Key;
            if (node.state != NodeState.Visible) break; // Visible 노드만 판정

            var bodyState = BodyManager.Instance?.State;
            bool pass = CanPass(node.conditionType, bodyState);

            Vector3 feedbackPos = new Vector3(node.position.x, node.position.y + nodeRadius + 0.6f, -0.2f);
            StartCoroutine(ShowConditionResultAndEnterRoom(node, feedbackPos, pass));
            break;
        }
    }

    IEnumerator ShowConditionResultAndEnterRoom(MapNode node, Vector3 worldPos, bool pass)
    {
        if (feedbackLabel == null) yield break;
        enteringRoom = pass;

        feedbackLabel.transform.position = worldPos;
        feedbackLabel.text = pass ? "O" : "X";
        feedbackLabel.color = pass ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.25f, 0.2f);
        feedbackLabel.gameObject.SetActive(true);
        yield return new WaitForSeconds(conditionFeedbackDelay);
        feedbackLabel.gameObject.SetActive(false);

        if (!pass)
        {
            enteringRoom = false;
            yield break;
        }

        if (MapManager.Instance != null && MapManager.Instance.TryBeginRoom(node))
        {
            string scene = node.conditionType == NodeConditionType.Boss ? bossSceneName : roomSceneName;
            SceneManager.LoadScene(scene);
        }
        else
            enteringRoom = false;
    }

    void Refresh()
    {
        foreach (var kvp in nodeRenderers)
        {
            var node = kvp.Key;
            var sr   = kvp.Value;
            sr.gameObject.SetActive(true);
            sr.color = GetColor(node);
        }

        foreach (var entry in lines)
            entry.lr.gameObject.SetActive(true);

        // 조건 라벨 갱신
        foreach (var kvp in nodeLabels)
        {
            var node = kvp.Key;
            var tmp  = kvp.Value;

            if (node.state == NodeState.Visible)
            {
                tmp.gameObject.SetActive(true);
                tmp.text  = ConditionText(node.conditionType);
                tmp.fontSize = conditionLabelFontSize;
                tmp.color = Color.white;
            }
            else if (node.state == NodeState.RouteOnly)
            {
                tmp.gameObject.SetActive(true);
                tmp.text  = "?";
                tmp.fontSize = conditionLabelFontSize;
                tmp.color = new Color(0.6f, 0.6f, 0.6f);
            }
            else
            {
                tmp.gameObject.SetActive(false);
            }
        }

        if (MapManager.Instance != null)
        {
            int curLayer = MapManager.Instance.CurrentNode.layer;
            targetCamY = -(curLayer + 1) * layerSpacing;

            if (!Application.isPlaying && Camera.main != null)
                Camera.main.transform.position = new Vector3(0f, targetCamY, -10f);
        }
    }


    string GetStatusText(BodyState state)
    {
        if (state == null)
            return "[인형 상태]\n데이터 없음";

        bool hasOneArmMissing = state.armLeft != state.armRight;
        bool hasOneLegMissing = state.legLeft != state.legRight;
        bool hasEyeMissing = state.eyeLeft != state.eyeRight;

        int attackPress = hasOneArmMissing ? 3 : 1;
        int movePercent = hasOneLegMissing ? Mathf.RoundToInt(50f) : 100;
        string vision = !state.eyeLeft && !state.eyeRight ? "양쪽 차단"
                        : !state.eyeLeft ? "왼쪽 차단"
                        : !state.eyeRight ? "오른쪽 차단"
                        : "정상";

        return "[인형 수치]\n" +
               $"공격 입력 : {attackPress}회\n" +
               $"이동 속도 : {movePercent}%\n" +
               $"시야 : {vision}\n" +
               $"팔 : 왼 {(state.armLeft ? "O" : "X")} / 오 {(state.armRight ? "O" : "X")}\n" +
               $"다리 : 왼 {(state.legLeft ? "O" : "X")} / 오 {(state.legRight ? "O" : "X")}";
    }

    Color GetColor(MapNode n)
    {
        if (n.state == NodeState.Current)   return ColCurrent;
        if (n.state == NodeState.Cleared)   return ColCleared;
        if (n.state == NodeState.RouteOnly) return ColRouteOnly;
        if (n.state == NodeState.Hidden)    return ColHidden;
        if (n.state == NodeState.Visible)
        {
            switch (n.conditionType)
            {
                case NodeConditionType.Free:       return ColFree;
                case NodeConditionType.NoLeftArm:  return ColNoLeftArm;
                case NodeConditionType.NoRightEye: return ColNoRightEye;
                case NodeConditionType.NoLeftLeg:  return ColNoLeftLeg;
                case NodeConditionType.NoRightLeg: return ColNoRightLeg;
                case NodeConditionType.Boss:       return ColBoss;
                default:                           return ColRouteOnly;
            }
        }
        return Color.white;
    }

    void ApplyTextSizes()
    {
        foreach (var label in nodeLabels.Values)
            if (label != null)
            {
                label.fontSize = conditionLabelFontSize;
                ApplyFont(label);
            }

        if (feedbackLabel != null)
        {
            feedbackLabel.fontSize = feedbackLabelFontSize;
            ApplyFont(feedbackLabel);
        }
    }

    void ApplyFont(TMP_Text tmp)
    {
        if (tmp == null) return;

        TMP_FontAsset fontAsset = mapTextFont != null ? mapTextFont : TMP_Settings.defaultFontAsset;
        if (fontAsset == null) return;

        tmp.font = fontAsset;
        tmp.fontSharedMaterial = fontAsset.material;
    }

    List<List<MapNode>> CollectLayers(MapNode root)
    {
        var result  = new List<List<MapNode>>();
        var visited = new HashSet<MapNode>();
        var queue   = new Queue<MapNode>();
        queue.Enqueue(root);
        visited.Add(root);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            while (result.Count <= node.layer) result.Add(new List<MapNode>());
            result[node.layer].Add(node);
            foreach (var child in node.children)
                if (!visited.Contains(child))
                {
                    visited.Add(child);
                    queue.Enqueue(child);
                }
        }
        return result;
    }

    static Sprite MakeCircleSprite(int size)
    {
        var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var center = new Vector2(size / 2f, size / 2f);
        float r    = size / 2f - 1f;
        var pixels = tex.GetPixels32();
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = Vector2.Distance(new Vector2(x, y), center) <= r
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
