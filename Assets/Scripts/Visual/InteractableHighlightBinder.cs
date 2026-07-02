using UnityEngine;
using UnityEngine.SceneManagement;

// 모든 씬에서 문/아이템에 InteractableHighlight(살구색 근접 글로우 + 스파클)를 자동 부착한다.
// 문/아이템은 런타임에 생성되므로 씬 로드 직후 + 짧은 주기로 스캔해 아직 안 붙은 대상에만 부착한다.
// 부착은 멱등(GetComponent 체크)이라 이미 붙은 오브젝트는 건너뛴다.
[DisallowMultipleComponent]
public sealed class InteractableHighlightBinder : MonoBehaviour
{
    const string RootName = "_InteractableHighlightBinder";
    const float ScanInterval = 0.6f;
    const float DoorDistance = 2.4f;
    const float ItemDistance = 1.8f;

    static InteractableHighlightBinder instance;

    Transform player;
    float nextScanTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Application.isPlaying)
            return;

        EnsureInstance();
        instance.player = null;      // re-resolve player in the new scene
        instance.nextScanTime = 0f;  // scan on the next frame
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject(RootName);
        DontDestroyOnLoad(go);
        instance = go.AddComponent<InteractableHighlightBinder>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (Time.unscaledTime < nextScanTime)
            return;
        nextScanTime = Time.unscaledTime + ScanInterval;

        ResolvePlayer();
        Scan();
    }

    void ResolvePlayer()
    {
        if (player != null)
            return;

        GameObject found = GameObject.FindWithTag("Player");
        if (found != null)
            player = found.transform;
    }

    void Scan()
    {
        // 문
        foreach (DoorTrigger door in FindObjectsByType<DoorTrigger>(FindObjectsSortMode.None))
            Attach(door.gameObject, DoorDistance);

        // 아이템류 픽업
        foreach (ItemWorldPickup p in FindObjectsByType<ItemWorldPickup>(FindObjectsSortMode.None))
            Attach(p.gameObject, ItemDistance);
        foreach (JewelWorldPickup p in FindObjectsByType<JewelWorldPickup>(FindObjectsSortMode.None))
            Attach(p.gameObject, ItemDistance);
        foreach (CoinWorldPickup p in FindObjectsByType<CoinWorldPickup>(FindObjectsSortMode.None))
            Attach(p.gameObject, ItemDistance);
        foreach (DropPickup p in FindObjectsByType<DropPickup>(FindObjectsSortMode.None))
            Attach(p.gameObject, ItemDistance);
    }

    void Attach(GameObject target, float distance)
    {
        if (target == null)
            return;
        if (target.GetComponent<InteractableHighlight>() != null)
            return;

        InteractableHighlight highlight = target.AddComponent<InteractableHighlight>();
        highlight.Configure(player, distance);
    }
}
