using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 화면 밖(카메라 시야 밖)에 있는 적을 카메라 가장자리에서 연한 붉은 막대(화살표)로 가리킨다.
// 튜토리얼의 작업대 화살표처럼, 해당 적의 방향을 따라 회전한다.
public class OffscreenEnemyIndicators : MonoBehaviour
{
    static OffscreenEnemyIndicators instance;

    Camera cam;
    readonly List<SpriteRenderer> pool = new();
    readonly List<EnemyBase> enemies = new();
    float nextRefreshTime;
    float pulseTime;

    static Sprite arrowSprite;

    const float RefreshInterval = 0.3f;
    static readonly Color BarColor = new Color(0.92f, 0.08f, 0.055f, 0.38f); // soft red
    const int SortingOrder = 500;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject("OffscreenEnemyIndicators");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<OffscreenEnemyIndicators>();
    }

    void LateUpdate()
    {
        if (!IsCombatScene())
        {
            HideAll();
            return;
        }

        if (cam == null)
            cam = Camera.main;
        if (cam == null || !cam.orthographic)
        {
            HideAll();
            return;
        }

        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + RefreshInterval;
            RefreshEnemies();
        }

        pulseTime += Time.unscaledDeltaTime;
        float pulse = 0.75f + Mathf.Sin(pulseTime * 5f) * 0.25f;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 camPos = cam.transform.position;
        float insetW = Mathf.Min(0.7f, halfW * 0.12f);
        float insetH = Mathf.Min(0.7f, halfH * 0.12f);

        int used = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyBase enemy = enemies[i];
            if (enemy == null || !enemy.isActiveAndEnabled)
                continue;

            Vector3 enemyPos = enemy.transform.position;
            Vector3 vp = cam.WorldToViewportPoint(enemyPos);
            bool onScreen = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
            if (onScreen)
                continue;

            Vector2 dir = new Vector2(enemyPos.x - camPos.x, enemyPos.y - camPos.y);
            if (dir.sqrMagnitude < 0.0001f)
                continue;
            dir.Normalize();

            float tx = Mathf.Abs(dir.x) > 1e-4f ? (halfW - insetW) / Mathf.Abs(dir.x) : float.MaxValue;
            float ty = Mathf.Abs(dir.y) > 1e-4f ? (halfH - insetH) / Mathf.Abs(dir.y) : float.MaxValue;
            float t = Mathf.Min(tx, ty);
            Vector3 edgePos = camPos + (Vector3)(dir * t);
            edgePos.z = 0f;

            SpriteRenderer sr = GetIndicator(used);
            sr.transform.position = edgePos;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            sr.color = new Color(BarColor.r, BarColor.g, BarColor.b, BarColor.a * pulse);
            sr.enabled = true;
            used++;
        }

        for (int i = used; i < pool.Count; i++)
            if (pool[i] != null)
                pool[i].enabled = false;
    }

    void RefreshEnemies()
    {
        enemies.Clear();
        EnemyBase[] found = FindObjectsByType<EnemyBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
            if (found[i] != null)
                enemies.Add(found[i]);
    }

    SpriteRenderer GetIndicator(int index)
    {
        while (pool.Count <= index)
        {
            GameObject go = new GameObject("OffscreenEnemyArrow_" + pool.Count);
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(0.82f, 0.62f, 1f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ArrowSprite();
            sr.color = BarColor;
            sr.sortingOrder = SortingOrder;
            sr.enabled = false;
            pool.Add(sr);
        }

        return pool[index];
    }

    void HideAll()
    {
        for (int i = 0; i < pool.Count; i++)
            if (pool[i] != null)
                pool[i].enabled = false;
    }

    static bool IsCombatScene()
    {
        string name = SceneManager.GetActiveScene().name;
        return name != "StartScene" && name != "TutorialScene" && name != "MapScene";
    }

    // +X 방향을 가리키는 막대+화살촉 스프라이트 (피벗 중앙)
    static Sprite ArrowSprite()
    {
        if (arrowSprite != null)
            return arrowSprite;

        const int w = 42;
        const int h = 24;
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.name = "OffscreenEnemyArrow";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color solid = Color.white;
        int bodyEnd = 18;
        int bodyHalf = 3;
        float centerY = (h - 1) * 0.5f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool inBody = x <= bodyEnd && x >= 6 && Mathf.Abs(y - centerY) <= bodyHalf;
                float headT = Mathf.InverseLerp(bodyEnd, w - 1, x);
                float headHalf = Mathf.Lerp(h * 0.5f, 0f, headT);
                bool inHead = x > bodyEnd && Mathf.Abs(y - centerY) <= headHalf;
                bool inTailA = x < 12 && Mathf.Abs((y - centerY) - (12 - x) * 0.45f) <= 2.1f;
                bool inTailB = x < 12 && Mathf.Abs((y - centerY) + (12 - x) * 0.45f) <= 2.1f;

                texture.SetPixel(x, y, inBody || inHead || inTailA || inTailB ? solid : clear);
            }
        }

        texture.Apply();
        arrowSprite = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), w);
        arrowSprite.name = texture.name;
        return arrowSprite;
    }
}

public class FarEnemyArrowIndicator : MonoBehaviour
{
    [SerializeField, Min(0.1f)] float showDistance = 9.0f;
    [SerializeField, Min(0.1f)] float playerOffset = 1.18f;
    [SerializeField, Min(0f)] float verticalOffset = 0.58f;
    [SerializeField, Range(0f, 1f)] float arrowAlpha = 0.52f;
    [SerializeField] Color arrowColor = new Color(0.9f, 0.08f, 0.055f, 0.52f);

    Transform player;
    Transform arrowRoot;
    SpriteRenderer[] renderers;
    static Sprite squareSprite;

    public static FarEnemyArrowIndicator Ensure()
    {
        FarEnemyArrowIndicator existing = FindFirstObjectByType<FarEnemyArrowIndicator>();
        if (existing != null)
            return existing;

        GameObject go = new GameObject("FarEnemyArrowIndicator");
        return go.AddComponent<FarEnemyArrowIndicator>();
    }

    void Awake()
    {
        BuildArrow();
        SetVisible(false);
    }

    void Update()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;
        }

        if (player == null)
        {
            SetVisible(false);
            return;
        }

        EnemyBase target = FindFarthestEnemy(out float distance);
        if (target == null || distance < showDistance)
        {
            SetVisible(false);
            return;
        }

        Vector2 fromPlayer = target.transform.position - player.position;
        if (fromPlayer.sqrMagnitude <= 0.0001f)
        {
            SetVisible(false);
            return;
        }

        Vector2 direction = fromPlayer.normalized;
        float bob = Mathf.Sin(Time.time * 6.4f) * 0.055f;
        arrowRoot.position = (Vector2)player.position + direction * (playerOffset + bob) + Vector2.up * verticalOffset;
        arrowRoot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        arrowRoot.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.04f, 0.5f + 0.5f * Mathf.Sin(Time.time * 5.2f));
        SetVisible(true);
    }

    EnemyBase FindFarthestEnemy(out float farthestDistance)
    {
        farthestDistance = 0f;
        EnemyBase best = null;
        EnemyBase[] enemies = FindObjectsByType<EnemyBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyBase enemy = enemies[i];
            if (enemy == null || enemy.transform == player)
                continue;

            float distance = Vector2.Distance(player.position, enemy.transform.position);
            if (distance > farthestDistance)
            {
                farthestDistance = distance;
                best = enemy;
            }
        }

        return best;
    }

    void BuildArrow()
    {
        GameObject root = new GameObject("FarEnemyArrow");
        root.transform.SetParent(transform, false);
        arrowRoot = root.transform;

        AddArrowPart("Core", new Vector2(-0.12f, 0f), new Vector2(0.34f, 0.14f), 0f);
        AddArrowPart("HeadA", new Vector2(0.16f, 0.12f), new Vector2(0.42f, 0.15f), -38f);
        AddArrowPart("HeadB", new Vector2(0.16f, -0.12f), new Vector2(0.42f, 0.15f), 38f);
        renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
    }

    void AddArrowPart(string objectName, Vector2 localPosition, Vector2 scale, float angle)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(arrowRoot, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSprite();
        renderer.color = arrowColor;
        renderer.sortingOrder = 219;
    }

    void SetVisible(bool visible)
    {
        if (arrowRoot == null)
            return;

        if (arrowRoot.gameObject.activeSelf != visible)
            arrowRoot.gameObject.SetActive(visible);

        if (!visible || renderers == null)
            return;

        float pulse = Mathf.Lerp(0.36f, arrowAlpha, 0.5f + 0.5f * Mathf.Sin(Time.time * 7.5f));
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Color c = arrowColor;
            c.a = pulse;
            renderers[i].color = c;
        }
    }

    static Sprite SquareSprite()
    {
        if (squareSprite != null)
            return squareSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        squareSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        squareSprite.name = "FarEnemyArrowSquare";
        return squareSprite;
    }
}
