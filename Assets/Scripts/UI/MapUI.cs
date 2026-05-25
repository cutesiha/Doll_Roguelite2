using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[ExecuteAlways]
public class MapUI : MonoBehaviour
{
    [SerializeField] float layerSpacing = 4f;
    [SerializeField] float mapWidth = 12f;
    [SerializeField] float nodeRadius = 0.5f;
    [SerializeField] float camLerpSpeed = 5f;

    static readonly Color ColCurrent   = new Color(1.00f, 0.65f, 0.10f, 1f);
    static readonly Color ColCleared   = new Color(0.20f, 0.20f, 0.20f, 1f);
    static readonly Color ColNone      = new Color(0.30f, 0.30f, 0.30f, 1f);
    static readonly Color ColNormal    = new Color(0.25f, 0.50f, 1.00f, 1f);
    static readonly Color ColHard      = new Color(0.65f, 0.10f, 0.15f, 1f);
    static readonly Color ColBoss      = new Color(0.90f, 0.75f, 0.10f, 1f);
    static readonly Color ColRouteOnly = new Color(0.45f, 0.45f, 0.45f, 1f);
    static readonly Color ColLine      = new Color(0.40f, 0.40f, 0.40f, 1f);

    Sprite circleSprite;
    readonly Dictionary<MapNode, SpriteRenderer> nodeRenderers = new Dictionary<MapNode, SpriteRenderer>();
    readonly Dictionary<MapNode, CircleCollider2D> nodeColliders = new Dictionary<MapNode, CircleCollider2D>();
    readonly List<(LineRenderer lr, MapNode from, MapNode to)> lines = new List<(LineRenderer lr, MapNode from, MapNode to)>();

    float targetCamY;

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

    void Cleanup()
    {
        nodeRenderers.Clear();
        nodeColliders.Clear();
        lines.Clear();

        // 딕셔너리는 도메인 리로드 후 초기화되므로, transform 자식 전체를 직접 삭제
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

        // ── orthoSize 계산 ──────────────────────────────────────────────
        // 세로: 3계층이 화면을 꽉 채워야 하므로 layerSpacing + padding
        // 보스(마지막 레이어) 노출 방지 상한: (totalLayers-2)*layerSpacing
        // 가로: 실제 화면 비율 반영해 노드가 잘리지 않도록 필요 시 확대
        float vertOrtho = layerSpacing + 0.8f;
        float maxOrtho  = (totalLayers - 2) * layerSpacing - 0.2f;
        float orthoSize = vertOrtho;
        float effectiveMapWidth = mapWidth;

        if (Camera.main != null)
        {
            float horzOrtho = mapWidth / Camera.main.aspect / 2f + 0.5f;
            orthoSize = Mathf.Min(Mathf.Max(vertOrtho, horzOrtho), maxOrtho);
            // 실제 보이는 가로 범위 안에 노드가 수용되도록 X 스프레드 조정
            effectiveMapWidth = Mathf.Min(mapWidth, (orthoSize - 0.5f) * Camera.main.aspect * 2f);
            Camera.main.orthographicSize = orthoSize;
        }
        // ────────────────────────────────────────────────────────────────

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

        // Initial camera: center of layers 0–2
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
    }

    void CreateLine(MapNode from, MapNode to)
    {
        var go = new GameObject($"Line_{from.id}_{to.id}");
        go.transform.SetParent(transform);

        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, new Vector3(from.position.x, from.position.y, 0.1f));
        lr.SetPosition(1, new Vector3(to.position.x, to.position.y, 0.1f));
        lr.startWidth = lr.endWidth = 0.05f;
        lr.useWorldSpace = true;
        lr.sortingOrder = 1;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", ColLine);
        lr.material = mat;

        lines.Add((lr, from, to));
    }

    void Update()
    {
        // Smooth camera scroll (play mode only to avoid editor camera fighting)
        if (Application.isPlaying && Camera.main != null)
        {
            var pos = Camera.main.transform.position;
            pos.y = Mathf.Lerp(pos.y, targetCamY, Time.deltaTime * camLerpSpeed);
            Camera.main.transform.position = pos;
        }

        if (!Application.isPlaying) return;

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        var worldPos = (Vector2)Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        var hit = Physics2D.OverlapPoint(worldPos);
        if (hit == null) return;

        foreach (var kvp in nodeColliders)
        {
            if (kvp.Value == hit && MapManager.Instance.TryMoveToNode(kvp.Key))
                break;
        }
    }

    void Refresh()
    {
        foreach (var kvp in nodeRenderers)
        {
            var node = kvp.Key;
            var sr = kvp.Value;
            bool hidden = node.state == NodeState.Hidden;
            sr.gameObject.SetActive(!hidden);
            if (!hidden) sr.color = GetColor(node);
        }

        foreach (var entry in lines)
            // from과 to 둘 다 Hidden이 아닐 때만 선 표시
            entry.lr.gameObject.SetActive(
                entry.from.state != NodeState.Hidden &&
                entry.to.state  != NodeState.Hidden);

        // Update camera target based on current layer
        if (MapManager.Instance != null)
        {
            int curLayer = MapManager.Instance.CurrentNode.layer;
            targetCamY = -(curLayer + 1) * layerSpacing;

            // In edit mode snap camera immediately (no lerp)
            if (!Application.isPlaying && Camera.main != null)
                Camera.main.transform.position = new Vector3(0f, targetCamY, -10f);
        }
    }

    Color GetColor(MapNode n)
    {
        if (n.state == NodeState.Current)   return ColCurrent;
        if (n.state == NodeState.Cleared)   return ColCleared;
        if (n.state == NodeState.RouteOnly) return ColRouteOnly;
        if (n.state == NodeState.Visible)
        {
            switch (n.conditionType)
            {
                case NodeConditionType.Normal: return ColNormal;
                case NodeConditionType.Hard:   return ColHard;
                case NodeConditionType.Boss:   return ColBoss;
                default:                       return ColNone;
            }
        }
        return Color.white;
    }

    List<List<MapNode>> CollectLayers(MapNode root)
    {
        var result = new List<List<MapNode>>();
        var visited = new HashSet<MapNode>();
        var queue = new Queue<MapNode>();
        queue.Enqueue(root);
        visited.Add(root);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            while (result.Count <= node.layer) result.Add(new List<MapNode>());
            result[node.layer].Add(node);
            foreach (var child in node.children)
            {
                if (!visited.Contains(child))
                {
                    visited.Add(child);
                    queue.Enqueue(child);
                }
            }
        }
        return result;
    }

    static Sprite MakeCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var center = new Vector2(size / 2f, size / 2f);
        float r = size / 2f - 1f;
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
