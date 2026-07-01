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
    static readonly Color BarColor = new Color(0.86f, 0.26f, 0.22f, 0.5f); // 연한 붉은색
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
            go.transform.localScale = new Vector3(1.25f, 0.55f, 1f);
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

        const int w = 64;
        const int h = 24;
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.name = "OffscreenEnemyArrow";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color solid = Color.white;
        int shaftEnd = 40;          // 막대 부분 끝
        int shaftHalf = 5;          // 막대 반두께(픽셀)
        float centerY = (h - 1) * 0.5f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool inShaft = x <= shaftEnd && Mathf.Abs(y - centerY) <= shaftHalf;

                // 화살촉: shaftEnd~w 구간에서 삼각형으로 좁아짐
                float headT = Mathf.InverseLerp(shaftEnd, w - 1, x);
                float headHalf = Mathf.Lerp(h * 0.5f, 0f, headT);
                bool inHead = x > shaftEnd && Mathf.Abs(y - centerY) <= headHalf;

                texture.SetPixel(x, y, inShaft || inHead ? solid : clear);
            }
        }

        texture.Apply();
        arrowSprite = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), w);
        arrowSprite.name = texture.name;
        return arrowSprite;
    }
}
