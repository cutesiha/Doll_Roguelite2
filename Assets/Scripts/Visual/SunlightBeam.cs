using System.Collections.Generic;
using UnityEngine;

// 낡은 마법 인형공방 창문에서 비스듬히 들어오는 오후 햇살.
// 월드 공간 반투명 스프라이트로 구현: 햇살 빛줄기(사다리꼴) + 바닥 빛번짐(타원) + 빛 속 먼지.
// [ExecuteAlways] 라서 플레이하지 않아도 에디터 Game/Scene 뷰에서 보인다.
// 이 GameObject 의 위치=햇살이 시작되는 창문 지점, 회전=햇살 방향(로컬 -Y 로 뻗어나감).
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class SunlightBeam : MonoBehaviour
{
    [Header("Beam (햇살 빛줄기)")]
    [Tooltip("연한 노랑/베이지.")]
    [SerializeField] Color beamColor = new Color(1f, 0.94f, 0.72f, 1f);
    [Tooltip("햇살 세기. 기본 0.25, 안 보이면 0.4까지.")]
    [SerializeField, Range(0f, 0.6f)] float beamAlpha = 0.25f;
    [SerializeField, Min(1f)] float beamLength = 15f;
    [SerializeField, Min(0.1f)] float topWidth = 2.2f;
    [SerializeField, Min(0.1f)] float bottomWidth = 7f;
    [SerializeField] int beamSortingOrder = 60;

    [Header("Floor Light Pool (바닥 빛번짐)")]
    [SerializeField] Color poolColor = new Color(1f, 0.9f, 0.6f, 1f);
    [SerializeField, Range(0f, 0.6f)] float poolAlpha = 0.22f;
    [SerializeField] Vector2 poolSize = new Vector2(8f, 3.2f);
    [SerializeField] int poolSortingOrder = -40;

    [Header("Dust in beam (빛 속 먼지)")]
    [SerializeField, Range(0, 80)] int dustCount = 16;
    [Tooltip("작고 반짝이는 먼지 색(따뜻한 노랑).")]
    [SerializeField] Color dustColor = new Color(1f, 0.85f, 0.5f, 1f);
    [SerializeField, Range(0f, 1f)] float dustAlpha = 0.8f;
    [SerializeField, Range(0.02f, 0.6f)] float dustSizeMin = 0.06f;
    [SerializeField, Range(0.02f, 0.8f)] float dustSizeMax = 0.16f;
    [Tooltip("먼지 이동 속도(작고 느리게).")]
    [SerializeField, Range(0f, 1.5f)] float dustSpeed = 0.25f;
    [SerializeField] int dustSortingOrder = 62;

    const string BeamName = "_SunBeamSprite";
    const string PoolName = "_SunFloorPool";
    const string DustRootName = "_SunDust";

    sealed class Mote
    {
        public Transform tr;
        public SpriteRenderer sr;
        public Vector2 local;      // beam-local position
        public float vy;           // drift along -Y (down the beam)
        public float phase;
        public float twinkle;
        public float baseA;
    }

    SpriteRenderer beam;
    SpriteRenderer pool;
    Transform dustRoot;
    readonly List<Mote> motes = new List<Mote>();

    Sprite beamSprite;
    Sprite softSprite;
    int beamTexKey = int.MinValue;
    float lastTime;

    void OnEnable()
    {
        lastTime = Time.realtimeSinceStartup;
        Build();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || !isActiveAndEnabled)
                return;
            Build();
        };
    }
#endif

    void Build()
    {
        EnsureSprites();
        EnsureBeam();
        EnsurePool();
        EnsureDust();
        ApplyStatic();
    }

    void EnsureBeam()
    {
        if (beam == null)
        {
            Transform t = transform.Find(BeamName);
            beam = t != null ? t.GetComponent<SpriteRenderer>() : null;
        }
        if (beam == null)
        {
            GameObject go = new GameObject(BeamName);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);
            beam = go.AddComponent<SpriteRenderer>();
        }
        beam.sprite = beamSprite;
        beam.sortingOrder = beamSortingOrder;
        // 스프라이트는 pivot(0.5,1)=상단 중앙 → 원점(창문)에서 아래로 뻗음. 폭/길이로 스케일.
        beam.transform.localPosition = Vector3.zero;
        beam.transform.localRotation = Quaternion.identity;
        Vector2 nb = beamSprite.bounds.size;
        beam.transform.localScale = new Vector3(bottomWidth / Mathf.Max(0.001f, nb.x), beamLength / Mathf.Max(0.001f, nb.y), 1f);
    }

    void EnsurePool()
    {
        if (pool == null)
        {
            Transform t = transform.Find(PoolName);
            pool = t != null ? t.GetComponent<SpriteRenderer>() : null;
        }
        if (pool == null)
        {
            GameObject go = new GameObject(PoolName);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);
            pool = go.AddComponent<SpriteRenderer>();
        }
        pool.sprite = softSprite;
        pool.sortingOrder = poolSortingOrder;
        // 빛줄기 끝(바닥) 위치. 부모 회전을 상쇄해 타원이 바닥에 눕도록 world 회전 0 으로.
        pool.transform.localPosition = new Vector3(0f, -beamLength, 0f);
        pool.transform.rotation = Quaternion.identity;
        pool.transform.localScale = Vector3.one; // world 스케일은 아래서 sizeDelta 로
        SetWorldSize(pool.transform, poolSize.x, poolSize.y);
    }

    void SetWorldSize(Transform t, float w, float h)
    {
        Vector3 ls = t.lossyScale;
        Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
        // softSprite 는 1 유닛 기준(PPU=size) 이라 스케일이 곧 월드 크기.
        t.localScale = new Vector3(
            w / Mathf.Max(0.0001f, parent.x),
            h / Mathf.Max(0.0001f, parent.y),
            1f);
    }

    void EnsureDust()
    {
        if (dustRoot == null)
        {
            Transform t = transform.Find(DustRootName);
            dustRoot = t != null ? t : null;
        }
        if (dustRoot == null)
        {
            GameObject go = new GameObject(DustRootName);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            dustRoot = go.transform;
        }

        while (motes.Count < dustCount)
            motes.Add(CreateMote());

        for (int i = 0; i < motes.Count; i++)
            motes[i].tr.gameObject.SetActive(i < dustCount);
    }

    Mote CreateMote()
    {
        GameObject go = new GameObject("SunMote");
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(dustRoot, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = softSprite;
        sr.sortingOrder = dustSortingOrder;

        Mote m = new Mote
        {
            tr = go.transform,
            sr = sr,
            phase = Random.Range(0f, 20f),
            twinkle = Random.Range(0.6f, 1.8f),
            baseA = Random.Range(0.6f, 1f)
        };
        ResetMote(m, true);
        return m;
    }

    void ResetMote(Mote m, bool anywhere)
    {
        float y = anywhere ? Random.Range(-beamLength, 0f) : Random.Range(-1f, 0f);
        float frac = Mathf.InverseLerp(0f, -beamLength, y); // 0 top .. 1 bottom
        float halfW = Mathf.Lerp(topWidth, bottomWidth, frac) * 0.5f;
        float x = Random.Range(-halfW, halfW);
        m.local = new Vector2(x, y);
        m.vy = Random.Range(0.5f, 1.2f) * dustSpeed;
        float s = Random.Range(dustSizeMin, Mathf.Max(dustSizeMin, dustSizeMax));
        SetWorldSize(m.tr, s, s);
        m.tr.localPosition = new Vector3(m.local.x, m.local.y, -0.01f);
    }

    void ApplyStatic()
    {
        if (beam != null)
        {
            Color c = beamColor; c.a = beamAlpha;
            beam.color = c;
        }
        if (pool != null)
        {
            Color c = poolColor; c.a = poolAlpha;
            pool.color = c;
        }
    }

    void Update()
    {
        if (beam == null || pool == null || dustRoot == null)
            Build();

        // 편집 중 회전/크기 변경 즉시 반영
        if (beam != null && beam.sprite != null)
        {
            Vector2 nb = beam.sprite.bounds.size;
            beam.transform.localScale = new Vector3(bottomWidth / Mathf.Max(0.001f, nb.x), beamLength / Mathf.Max(0.001f, nb.y), 1f);
            beam.sortingOrder = beamSortingOrder;
        }
        if (pool != null)
        {
            pool.transform.localPosition = new Vector3(0f, -beamLength, 0f);
            pool.transform.rotation = Quaternion.identity;
            SetWorldSize(pool.transform, poolSize.x, poolSize.y);
            pool.sortingOrder = poolSortingOrder;
        }
        ApplyStatic();

        float now = Time.realtimeSinceStartup;
        float dt = Mathf.Clamp(now - lastTime, 0f, 0.1f);
        lastTime = now;

        for (int i = 0; i < motes.Count; i++)
        {
            Mote m = motes[i];
            if (!m.tr.gameObject.activeSelf)
                continue;

            m.local.y -= m.vy * dt;                                   // 천천히 아래로
            m.local.x += Mathf.Sin(now * 0.6f + m.phase) * dt * dustSpeed * 0.8f; // 좌우 흔들림
            if (m.local.y < -beamLength)
                ResetMote(m, false);                                  // 위에서 재등장

            m.tr.localPosition = new Vector3(m.local.x, m.local.y, -0.01f);

            float tw = Mathf.Clamp01(0.5f + 0.5f * Mathf.Sin(now * m.twinkle + m.phase));
            Color c = dustColor;
            c.a = Mathf.Clamp01(dustAlpha * m.baseA * (0.25f + 0.75f * tw * tw));
            m.sr.color = c;
        }
    }

    void EnsureSprites()
    {
        if (softSprite == null)
            softSprite = MakeSoftCircle();

        int key = new Vector4(topWidth, bottomWidth, beamLength, 0f).GetHashCode();
        if (beamSprite == null || key != beamTexKey)
        {
            beamTexKey = key;
            beamSprite = MakeBeamSprite();
        }
    }

    Sprite MakeSoftCircle()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "SunSoftCircle", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxR = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / maxR;
                float a = Mathf.Clamp01(1f - d);
                a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        // PPU=size -> 스프라이트 1유닛 크기
        Sprite s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        s.name = "SunSoftCircle";
        return s;
    }

    Sprite MakeBeamSprite()
    {
        const int W = 96, H = 384;
        Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            name = "SunBeamTex", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        float topFrac = Mathf.Clamp01(topWidth / Mathf.Max(0.001f, bottomWidth));
        float cx = (W - 1) * 0.5f;
        for (int y = 0; y < H; y++)
        {
            float ny = y / (float)(H - 1);            // 0 bottom .. 1 top
            float halfFrac = Mathf.Lerp(1f, topFrac, ny); // 바닥은 넓게, 위는 좁게
            // 길이 페이드: 위(창문)와 아래 끝을 부드럽게
            float lenFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.18f, ny))     // 아래 끝 페이드인
                          * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.72f, ny));    // 위 끝 살짝 페이드
            lenFade = Mathf.Lerp(0.55f, 1f, lenFade);
            for (int x = 0; x < W; x++)
            {
                float dxN = Mathf.Abs(x - cx) / (W * 0.5f); // 0 center .. 1 edge
                float a;
                if (dxN > halfFrac)
                    a = 0f;
                else
                {
                    float t = dxN / Mathf.Max(0.001f, halfFrac); // 0 center .. 1 trapezoid edge
                    float soft = 1f - t * t;                     // 중앙 밝고 가장자리 투명
                    a = Mathf.Clamp01(soft) * lenFade;
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        // pivot 상단 중앙(0.5,1). native 크기는 EnsureBeam 에서 bounds 로 보정해 스케일함.
        Sprite s = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 1f), 100f);
        s.name = "SunBeamTex";
        return s;
    }
}
