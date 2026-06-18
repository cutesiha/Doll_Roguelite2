using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SoundManager : MonoBehaviour
{
    public const string PanelSfxPath = "Sounds/paper555_[cut_0sec]";
    public const string SlimeSfxPath = "Sounds/slime";
    public const string ClickSfxPath = "Sounds/click";
    public const float DefaultRepeatGuard = 0.08f;
    public const int DefaultVolumeLevel = 7;
    const string BgmVolumeLevelKey = "StartOptionBgmVolumeLevel";
    const string SfxVolumeLevelKey = "StartOptionSfxVolumeLevel";

    static SoundManager instance;
    static readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();

    [Header("Global")]
    [SerializeField, Range(0f, 2f)] float masterSfxVolume = 1f;
    [SerializeField] AudioSource sfxSource;

    [Header("Clips")]
    [SerializeField] AudioClip panelClip;
    [SerializeField, Range(0f, 3f)] float panelVolume = 1f;
    [SerializeField] AudioClip slimeClip;
    [SerializeField, Range(0f, 3f)] float slimeVolume = 1f;
    [SerializeField] AudioClip clickClip;
    [SerializeField, Range(0f, 3f)] float clickVolume = 1.35f;

    [Header("Combat Clips")]
    [SerializeField] AudioClip enemyHitClip;
    [SerializeField, Range(0f, 3f)] float enemyHitVolume = 1f;
    [SerializeField] AudioClip playerHitClip;
    [SerializeField, Range(0f, 3f)] float playerHitVolume = 1f;

    AudioClip lastClip;
    float lastPlayTime = -999f;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            if (IsSceneInstance(this) && !IsSceneInstance(instance))
            {
                Destroy(instance.gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        instance = this;
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }

        EnsureSource();
        ApplySavedVolumes();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnEnable()
    {
        if (instance == null || (!IsSceneInstance(instance) && IsSceneInstance(this)))
        {
            instance = this;
            EnsureSource();
            ApplySavedVolumes();
        }
    }

    static bool IsSceneInstance(SoundManager manager)
    {
        return manager != null
            && manager.gameObject.scene.IsValid()
            && manager.gameObject.scene.name != "DontDestroyOnLoad";
    }

    static SoundManager FindPreferredInstance()
    {
        SoundManager[] managers = FindObjectsByType<SoundManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        SoundManager fallback = null;

        for (int i = 0; i < managers.Length; i++)
        {
            if (IsSceneInstance(managers[i]))
                return managers[i];

            if (fallback == null)
                fallback = managers[i];
        }

        return fallback;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            instance = null;
        }
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySavedVolumes();
    }

    void OnValidate()
    {
        if (instance == this)
        {
            EnsureSource();
            return;
        }

        EnsureSource();
    }

    public static void PlayPanel(float repeatGuard = DefaultRepeatGuard)
    {
        SoundManager manager = EnsureInstance();
        manager.PlayManaged(manager.GetPanelClip(), manager.panelVolume, repeatGuard);
    }

    public static void PlaySlime(float repeatGuard = DefaultRepeatGuard)
    {
        SoundManager manager = EnsureInstance();
        manager.PlayManaged(manager.GetSlimeClip(), manager.slimeVolume, repeatGuard);
    }

    public static void PlayClick(float repeatGuard = DefaultRepeatGuard)
    {
        SoundManager manager = EnsureInstance();
        manager.PlayManaged(manager.GetClickClip(), manager.clickVolume, repeatGuard);
    }

    public static void PlayEnemyHit(float repeatGuard = DefaultRepeatGuard)
    {
        SoundManager manager = EnsureInstance();
        if (manager.enemyHitClip != null)
            manager.PlayManaged(manager.enemyHitClip, manager.enemyHitVolume, repeatGuard);
    }

    public static void PlayPlayerHit(float repeatGuard = DefaultRepeatGuard)
    {
        SoundManager manager = EnsureInstance();
        if (manager.playerHitClip != null)
            manager.PlayManaged(manager.playerHitClip, manager.playerHitVolume, repeatGuard);
    }

    public static void PlaySfxResource(string resourcePath, string fallbackResourcePath = null, float repeatGuard = DefaultRepeatGuard, float volumeScale = 1f)
    {
        PlaySfx(LoadClipResource(resourcePath, fallbackResourcePath), repeatGuard, volumeScale);
    }

    public static void PlaySfx(AudioClip clip, float repeatGuard = DefaultRepeatGuard, float volumeScale = 1f)
    {
        if (clip == null)
            return;

        EnsureInstance().PlayInternal(clip, repeatGuard, volumeScale);
    }

    public static int GetBgmVolumeLevel()
    {
        return Mathf.Clamp(PlayerPrefs.GetInt(BgmVolumeLevelKey, DefaultVolumeLevel), 0, 10);
    }

    public static int GetSfxVolumeLevel()
    {
        return Mathf.Clamp(PlayerPrefs.GetInt(SfxVolumeLevelKey, DefaultVolumeLevel), 0, 10);
    }

    public static float GetBgmVolume01()
    {
        return GetBgmVolumeLevel() / 10f;
    }

    public static float GetSfxVolume01()
    {
        return GetSfxVolumeLevel() / 10f;
    }

    public static void SetBgmVolumeLevel(int level)
    {
        PlayerPrefs.SetInt(BgmVolumeLevelKey, Mathf.Clamp(level, 0, 10));
        PlayerPrefs.Save();
        ApplyBgmVolumeToSceneSources();
    }

    public static void SetSfxVolumeLevel(int level)
    {
        PlayerPrefs.SetInt(SfxVolumeLevelKey, Mathf.Clamp(level, 0, 10));
        PlayerPrefs.Save();
        ApplySfxVolumeToSceneSources();
    }

    public static void ApplySavedVolumes()
    {
        if (instance != null)
            instance.masterSfxVolume = GetSfxVolume01();

        ApplyBgmVolumeToSceneSources();
        ApplySfxVolumeToSceneSources();
    }

    public static AudioClip LoadClipResource(string resourcePath, string fallbackResourcePath = null)
    {
        AudioClip clip = LoadClip(resourcePath);
        if (clip == null)
            clip = LoadClip(fallbackResourcePath);

        return clip;
    }

    public static void DisableAudioSourcesInChildren(GameObject root)
    {
        if (root == null)
            return;

        AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            sources[i].Stop();
            sources[i].playOnAwake = false;
            sources[i].enabled = true;
        }
    }

    static void ApplyBgmVolumeToSceneSources()
    {
        float volume = GetBgmVolume01();
        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sources.Length; i++)
            if (IsBgmSource(sources[i]))
                sources[i].volume = volume;
    }

    static void ApplySfxVolumeToSceneSources()
    {
        float volume = GetSfxVolume01();
        if (instance != null)
            instance.masterSfxVolume = volume;

        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sources.Length; i++)
        {
            if (instance != null && sources[i] == instance.sfxSource)
                continue;

            if (IsSfxSource(sources[i]))
                sources[i].volume = volume;
        }
    }

    static bool IsBgmSource(AudioSource source)
    {
        if (source == null)
            return false;

        if (source.name.ToLowerInvariant().Contains("bgm"))
            return true;

        return IsClipInResourceFolder(source.clip, "/Resources/BGM/");
    }

    static bool IsSfxSource(AudioSource source)
    {
        if (source == null)
            return false;

        return IsClipInResourceFolder(source.clip, "/Resources/Sounds/");
    }

    static bool IsClipInResourceFolder(AudioClip clip, string folderMarker)
    {
        if (clip == null)
            return false;

#if UNITY_EDITOR
        string path = AssetDatabase.GetAssetPath(clip).Replace('\\', '/');
        if (!string.IsNullOrEmpty(path) && path.Contains(folderMarker))
            return true;
#endif

        return false;
    }

    static AudioClip LoadClip(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return null;

        if (clipCache.TryGetValue(resourcePath, out AudioClip cachedClip))
            return cachedClip;

        AudioClip clip = Resources.Load<AudioClip>(resourcePath);
        clipCache[resourcePath] = clip;
        return clip;
    }

    static SoundManager EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindPreferredInstance();
        if (instance == null)
        {
            GameObject managerObject = new GameObject("SoundManager");
            instance = managerObject.AddComponent<SoundManager>();
        }

        instance.EnsureSource();
        return instance;
    }

    AudioClip GetPanelClip()
    {
        if (panelClip == null)
            panelClip = LoadClipResource(PanelSfxPath);

        return panelClip;
    }

    AudioClip GetSlimeClip()
    {
        if (slimeClip == null)
            slimeClip = LoadClipResource(SlimeSfxPath);

        return slimeClip;
    }

    AudioClip GetClickClip()
    {
        if (clickClip == null)
            clickClip = LoadClipResource(ClickSfxPath);

        return clickClip;
    }

    void EnsureSource()
    {
        if (sfxSource == null)
            sfxSource = GetComponent<AudioSource>();

        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
    }

    void PlayManaged(AudioClip clip, float clipVolume, float repeatGuard)
    {
        PlayInternal(clip, repeatGuard, clipVolume);
    }

    void PlayInternal(AudioClip clip, float repeatGuard, float volumeScale)
    {
        EnsureSource();

        if (clip == lastClip && Time.unscaledTime - lastPlayTime < repeatGuard)
            return;

        lastClip = clip;
        lastPlayTime = Time.unscaledTime;
        sfxSource.PlayOneShot(clip, Mathf.Max(0f, volumeScale) * Mathf.Max(0f, masterSfxVolume));
    }
}
