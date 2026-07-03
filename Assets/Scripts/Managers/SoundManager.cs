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
    public const string PunchSfxPath = "SoundEffects/punch";
    public const string EnemyHitSfxPath = "SoundEffects/punch";
    public const string FootstepTwoLegsSfxPath = "Sounds/footstep_two_legs";
    public const string FootstepOneLegSfxPath = "Sounds/footstep_one_leg";
    public const string FootstepNoLegsSfxPath = "Sounds/footstep_no_legs";
    const string FootstepFallbackSfxPath = "Sounds/scticky";
    public const string MinotaurSlamSfxPath = "Sounds/heavy_punch1";
    public const string MinotaurFastBreathSfxPath = "Sounds/mino_heat";
    public const string MinotaurPinStickSfxPath = "Sounds/short_punch1";
    public const string BookBossRageSfxPath = "Sounds/book_boss_rage";
    public const string BookBossRainLoopSfxPath = "Sounds/book_boss_rain_loop";
    public const string BookBossFloorSlamSfxPath = "Sounds/book_boss_floor_slam";
    public const string BookBossPaperFlySfxPath = "Sounds/book_boss_paper_fly";
    public const string WaveClearSfxPath = "Sounds/wave_clear";
    const string WaveClearFallbackSfxPath = "Sounds/paper333";
    public const string AfterVictoryBgmPath = "BGM/after_victory";
    public const string CoinPickupSfxPath = "SoundEffects/동전 먹을떄 효과음";
    public const string GemUseSfxPath = "SoundEffects/보석 효과음";
    public const float DefaultRepeatGuard = 0.08f;
    public const int DefaultVolumeLevel = 7;
    // BGM 전체 볼륨 배율 (0~1). 낮출수록 배경음악만 조용해짐. 효과음엔 영향 없음.
    const float BgmVolumeScale = 0.4f;
    const string BgmVolumeLevelKey = "StartOptionBgmVolumeLevel";
    const string SfxVolumeLevelKey = "StartOptionSfxVolumeLevel";

    static SoundManager instance;
    static readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();

    [Header("Global")]
    [SerializeField, Range(0f, 2f)] float masterSfxVolume = 1f;
    [SerializeField] AudioSource sfxSource;
    AudioSource footstepSource;

    [Header("Clips")]
    [SerializeField] AudioClip panelClip;
    [SerializeField, Range(0f, 3f)] float panelVolume = 1f;
    [SerializeField] AudioClip slimeClip;
    [SerializeField, Range(0f, 3f)] float slimeVolume = 1f;
    [SerializeField] AudioClip clickClip;
    [SerializeField, Range(0f, 3f)] float clickVolume = 1.35f;
    [SerializeField] AudioClip punchClip;
    [SerializeField, Range(0f, 3f)] float punchVolume = 1f;
    [SerializeField] AudioClip coinPickupClip;
    [SerializeField, Range(0f, 3f)] float coinPickupVolume = 1f;
    [SerializeField] AudioClip gemUseClip;
    [SerializeField, Range(0f, 3f)] float gemUseVolume = 1f;
    [SerializeField, Min(0f)] float gemUseDuration = 1.4f;
    [SerializeField] AudioClip waveClearClip;
    [SerializeField, Range(0f, 3f)] float waveClearVolume = 1f;
    [SerializeField] AudioClip afterVictoryBgmClip;

    [Header("Player Footsteps")]
    [SerializeField] AudioClip twoLegsFootstepClip;
    [SerializeField, Range(0f, 3f)] float twoLegsFootstepVolume = 0.65f;
    [SerializeField] AudioClip oneLegFootstepClip;
    [SerializeField, Range(0f, 3f)] float oneLegFootstepVolume = 0.75f;
    [SerializeField] AudioClip noLegsFootstepClip;
    [SerializeField, Range(0f, 3f)] float noLegsFootstepVolume = 0.85f;

    [Header("Minotaur Boss Clips")]
    [SerializeField] AudioClip minotaurSlamClip;
    [SerializeField, Range(0f, 3f)] float minotaurSlamVolume = 1.15f;
    [SerializeField] AudioClip minotaurFastBreathClip;
    [SerializeField, Range(0f, 3f)] float minotaurFastBreathVolume = 1f;
    [SerializeField] AudioClip minotaurPinStickClip;
    [SerializeField, Range(0f, 3f)] float minotaurPinStickVolume = 0.95f;

    [Header("Book Boss Clips")]
    [SerializeField] AudioClip bookBossRageClip;
    [SerializeField, Range(0f, 3f)] float bookBossRageVolume = 1f;
    [SerializeField] AudioClip bookBossRainLoopClip;
    [SerializeField, Range(0f, 3f)] float bookBossRainLoopVolume = 0.55f;
    [SerializeField] AudioClip bookBossFloorSlamClip;
    [SerializeField, Range(0f, 3f)] float bookBossFloorSlamVolume = 1.05f;
    [SerializeField] AudioClip bookBossPaperFlyClip;
    [SerializeField, Range(0f, 3f)] float bookBossPaperFlyVolume = 0.85f;

    AudioSource gemAudioSource;
    AudioSource bookBossRageSource;
    AudioSource bookBossRainSource;
    Coroutine gemStopRoutine;
    Coroutine bgmFadeRoutine;

    [Header("Combat Clips")]
    [SerializeField] AudioClip enemyHitClip;
    [SerializeField, Range(0f, 3f)] float enemyHitVolume = 1f;
    [SerializeField] AudioClip playerHitClip;
    [SerializeField, Range(0f, 3f)] float playerHitVolume = 1f;

    AudioClip lastClip;
    float lastPlayTime = -999f;
    AudioClip lastFootstepClip;
    float lastFootstepPlayTime = -999f;

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

    public static void PlayTutorialPaperOpen(float repeatGuard = DefaultRepeatGuard)
    {
        PlayPanel(repeatGuard);
    }

    public static void PlayTutorialPaperClose(float repeatGuard = DefaultRepeatGuard)
    {
        PlayPanel(repeatGuard);
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

    public static void PlayGemUse()
    {
        SoundManager manager = EnsureInstance();
        manager.PlayGemUseInternal();
    }

    public static void PlayCoinPickup(float repeatGuard = DefaultRepeatGuard)
    {
        SoundManager manager = EnsureInstance();
        manager.PlayManaged(manager.GetCoinPickupClip(), manager.coinPickupVolume, repeatGuard);
    }

    public static void PlayPunch(float repeatGuard = DefaultRepeatGuard)
    {
        SoundManager manager = EnsureInstance();
        manager.PlayManaged(manager.GetPunchClip(), manager.punchVolume, repeatGuard);
    }

    public static void PlayEnemyHit(float repeatGuard = DefaultRepeatGuard)
    {
        SoundManager manager = EnsureInstance();
        AudioClip clip = manager.GetEnemyHitClip();
        if (clip != null)
            manager.PlayManaged(clip, manager.enemyHitVolume, repeatGuard);
    }

    public static void PlayPlayerHit(float repeatGuard = DefaultRepeatGuard)
    {
        SoundManager manager = EnsureInstance();
        if (manager.playerHitClip != null)
            manager.PlayManaged(manager.playerHitClip, manager.playerHitVolume, repeatGuard);
    }

    public static void PlayPlayerFootstep(int legCount, float repeatGuard = DefaultRepeatGuard)
    {
        SoundManager manager = EnsureInstance();
        AudioClip clip = manager.GetFootstepClip(legCount);
        if (clip == null)
            return;

        manager.PlayFootstepManaged(clip, manager.GetFootstepVolume(legCount), repeatGuard);
    }

    public static void StopPlayerFootstep()
    {
        SoundManager manager = instance;
        if (manager != null && manager.footstepSource != null)
            manager.footstepSource.Stop();
    }

    public static void PlayWaveClear(float repeatGuard = 0.2f)
    {
        SoundManager manager = EnsureInstance();
        manager.PlayManaged(manager.GetWaveClearClip(), manager.waveClearVolume, repeatGuard);
    }

    public static void PlayAfterVictoryBgmWithFade(float fadeOutDuration = 0.9f, float fadeInDuration = 1.0f)
    {
        SoundManager manager = EnsureInstance();
        if (manager.bgmFadeRoutine != null)
            manager.StopCoroutine(manager.bgmFadeRoutine);

        manager.bgmFadeRoutine = manager.StartCoroutine(manager.AfterVictoryBgmFadeRoutine(fadeOutDuration, fadeInDuration));
    }

    public static void PlayMinotaurSlam(float repeatGuard = 0.04f)
    {
        SoundManager manager = EnsureInstance();
        manager.PlayManaged(manager.GetMinotaurSlamClip(), manager.minotaurSlamVolume, repeatGuard);
    }

    public static void PlayMinotaurFastBreath(float repeatGuard = 0.25f)
    {
        SoundManager manager = EnsureInstance();
        manager.PlayManaged(manager.GetMinotaurFastBreathClip(), manager.minotaurFastBreathVolume, repeatGuard);
    }

    public static void PlayMinotaurPinStick(float repeatGuard = 0.04f)
    {
        SoundManager manager = EnsureInstance();
        manager.PlayManaged(manager.GetMinotaurPinStickClip(), manager.minotaurPinStickVolume, repeatGuard);
    }

    public static void PlayBookBossRage(float repeatGuard = 0.2f)
    {
        SoundManager manager = EnsureInstance();
        manager.PlayBookBossRageInternal(repeatGuard);
    }

    public static void StopBookBossRage()
    {
        SoundManager manager = instance;
        if (manager != null && manager.bookBossRageSource != null)
            manager.bookBossRageSource.Stop();
    }

    public static void StartBookBossRainLoop()
    {
        SoundManager manager = EnsureInstance();
        manager.StartBookBossRainLoopInternal();
    }

    public static void StopBookBossRainLoop()
    {
        SoundManager manager = instance;
        if (manager != null && manager.bookBossRainSource != null)
            manager.bookBossRainSource.Stop();
    }

    public static void PlayBookBossFloorSlam(float repeatGuard = 0.04f)
    {
        SoundManager manager = EnsureInstance();
        manager.PlayManaged(manager.GetBookBossFloorSlamClip(), manager.bookBossFloorSlamVolume, repeatGuard);
    }

    public static void PlayBookBossPaperFly(float repeatGuard = 0.02f)
    {
        SoundManager manager = EnsureInstance();
        manager.PlayManaged(manager.GetBookBossPaperFlyClip(), manager.bookBossPaperFlyVolume, repeatGuard);
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
        float volume = GetBgmVolume01() * BgmVolumeScale;
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

    void PlayGemUseInternal()
    {
        if (gemUseClip == null)
            gemUseClip = LoadClipResource(GemUseSfxPath);

        if (gemUseClip == null)
            return;

        EnsureGemSource();
        if (gemStopRoutine != null)
            StopCoroutine(gemStopRoutine);

        gemAudioSource.clip = gemUseClip;
        gemAudioSource.volume = Mathf.Max(0f, gemUseVolume) * Mathf.Max(0f, masterSfxVolume);
        gemAudioSource.Play();
        gemStopRoutine = StartCoroutine(StopGemAfterDuration());
    }

    System.Collections.IEnumerator StopGemAfterDuration()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, gemUseDuration));
        if (gemAudioSource != null)
            gemAudioSource.Stop();
        gemStopRoutine = null;
    }

    void EnsureGemSource()
    {
        if (gemAudioSource != null)
            return;

        gemAudioSource = gameObject.AddComponent<AudioSource>();
        gemAudioSource.playOnAwake = false;
        gemAudioSource.loop = false;
    }

    void EnsureBookBossRainSource()
    {
        if (bookBossRainSource != null)
            return;

        GameObject sourceObject = new GameObject("BookBossRainAudioSource");
        sourceObject.transform.SetParent(transform, false);
        bookBossRainSource = sourceObject.AddComponent<AudioSource>();
        bookBossRainSource.playOnAwake = false;
        bookBossRainSource.loop = true;
    }

    void EnsureBookBossRageSource()
    {
        if (bookBossRageSource != null)
            return;

        GameObject sourceObject = new GameObject("BookBossRageAudioSource");
        sourceObject.transform.SetParent(transform, false);
        bookBossRageSource = sourceObject.AddComponent<AudioSource>();
        bookBossRageSource.playOnAwake = false;
        bookBossRageSource.loop = false;
    }

    void PlayBookBossRageInternal(float repeatGuard)
    {
        AudioClip clip = GetBookBossRageClip();
        if (clip == null)
            return;

        EnsureBookBossRageSource();
        if (clip == lastClip && Time.unscaledTime - lastPlayTime < repeatGuard)
            return;

        lastClip = clip;
        lastPlayTime = Time.unscaledTime;
        bookBossRageSource.Stop();
        bookBossRageSource.clip = clip;
        bookBossRageSource.volume = Mathf.Max(0f, bookBossRageVolume) * Mathf.Max(0f, masterSfxVolume);
        bookBossRageSource.Play();
    }

    void StartBookBossRainLoopInternal()
    {
        AudioClip clip = GetBookBossRainLoopClip();
        if (clip == null)
            return;

        EnsureBookBossRainSource();
        bookBossRainSource.clip = clip;
        bookBossRainSource.loop = true;
        bookBossRainSource.volume = Mathf.Max(0f, bookBossRainLoopVolume) * Mathf.Max(0f, masterSfxVolume);
        if (!bookBossRainSource.isPlaying)
            bookBossRainSource.Play();
    }

    System.Collections.IEnumerator AfterVictoryBgmFadeRoutine(float fadeOutDuration, float fadeInDuration)
    {
        AudioSource bgmSource = FindPreferredBgmSource();
        if (bgmSource == null)
            yield break;

        float startVolume = bgmSource.volume;
        float elapsed = 0f;
        float safeFadeOut = Mathf.Max(0.01f, fadeOutDuration);
        while (elapsed < safeFadeOut && bgmSource != null)
        {
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / safeFadeOut);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (bgmSource == null)
            yield break;

        bgmSource.Stop();
        bgmSource.clip = GetAfterVictoryBgmClip();
        bgmSource.loop = true;
        bgmSource.volume = 0f;
        if (bgmSource.clip != null)
            bgmSource.Play();

        float targetVolume = GetBgmVolume01() * BgmVolumeScale;
        elapsed = 0f;
        float safeFadeIn = Mathf.Max(0.01f, fadeInDuration);
        while (elapsed < safeFadeIn && bgmSource != null)
        {
            bgmSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / safeFadeIn);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (bgmSource != null)
            bgmSource.volume = targetVolume;

        bgmFadeRoutine = null;
    }

    AudioSource FindPreferredBgmSource()
    {
        GameObject namedBgm = GameObject.Find("BGM");
        AudioSource source = namedBgm != null ? namedBgm.GetComponent<AudioSource>() : null;
        if (source != null)
            return source;

        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sources.Length; i++)
            if (IsBgmSource(sources[i]))
                return sources[i];

        GameObject bgmObject = new GameObject("BGM");
        source = bgmObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        return source;
    }

    AudioClip GetCoinPickupClip()
    {
        if (coinPickupClip == null)
            coinPickupClip = LoadClipResource(CoinPickupSfxPath);

        return coinPickupClip;
    }

    AudioClip GetPunchClip()
    {
        if (punchClip == null)
            punchClip = LoadClipResource(PunchSfxPath);

        return punchClip;
    }

    AudioClip GetEnemyHitClip()
    {
        if (enemyHitClip == null)
            enemyHitClip = LoadClipResource(EnemyHitSfxPath);

        return enemyHitClip;
    }

    AudioClip GetWaveClearClip()
    {
        if (waveClearClip == null)
            waveClearClip = LoadClipResource(WaveClearSfxPath, WaveClearFallbackSfxPath);

        return waveClearClip;
    }

    AudioClip GetAfterVictoryBgmClip()
    {
        if (afterVictoryBgmClip == null)
            afterVictoryBgmClip = LoadClipResource(AfterVictoryBgmPath);

        return afterVictoryBgmClip;
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

    AudioClip GetFootstepClip(int legCount)
    {
        if (legCount >= 2)
        {
            if (twoLegsFootstepClip == null)
                twoLegsFootstepClip = LoadClipResource(FootstepTwoLegsSfxPath, FootstepFallbackSfxPath);
            return twoLegsFootstepClip;
        }

        if (legCount == 1)
        {
            if (oneLegFootstepClip == null)
                oneLegFootstepClip = LoadClipResource(FootstepOneLegSfxPath, FootstepFallbackSfxPath);
            return oneLegFootstepClip;
        }

        if (noLegsFootstepClip == null)
            noLegsFootstepClip = LoadClipResource(FootstepNoLegsSfxPath, FootstepFallbackSfxPath);
        return noLegsFootstepClip;
    }

    float GetFootstepVolume(int legCount)
    {
        if (legCount >= 2)
            return twoLegsFootstepVolume;
        if (legCount == 1)
            return oneLegFootstepVolume;
        return noLegsFootstepVolume;
    }

    AudioClip GetMinotaurSlamClip()
    {
        if (minotaurSlamClip == null)
            minotaurSlamClip = LoadClipResource(MinotaurSlamSfxPath);
        return minotaurSlamClip;
    }

    AudioClip GetMinotaurFastBreathClip()
    {
        if (minotaurFastBreathClip == null)
            minotaurFastBreathClip = LoadClipResource(MinotaurFastBreathSfxPath);
        return minotaurFastBreathClip;
    }

    AudioClip GetMinotaurPinStickClip()
    {
        if (minotaurPinStickClip == null)
            minotaurPinStickClip = LoadClipResource(MinotaurPinStickSfxPath);
        return minotaurPinStickClip;
    }

    AudioClip GetBookBossRageClip()
    {
        if (bookBossRageClip == null)
            bookBossRageClip = LoadClipResource(BookBossRageSfxPath);
        return bookBossRageClip;
    }

    AudioClip GetBookBossRainLoopClip()
    {
        if (bookBossRainLoopClip == null)
            bookBossRainLoopClip = LoadClipResource(BookBossRainLoopSfxPath);
        return bookBossRainLoopClip;
    }

    AudioClip GetBookBossFloorSlamClip()
    {
        if (bookBossFloorSlamClip == null)
            bookBossFloorSlamClip = LoadClipResource(BookBossFloorSlamSfxPath);
        return bookBossFloorSlamClip;
    }

    AudioClip GetBookBossPaperFlyClip()
    {
        if (bookBossPaperFlyClip == null)
            bookBossPaperFlyClip = LoadClipResource(BookBossPaperFlySfxPath);
        return bookBossPaperFlyClip;
    }

    void EnsureSource()
    {
        if (sfxSource == null)
            sfxSource = GetComponent<AudioSource>();

        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
    }

    void EnsureFootstepSource()
    {
        if (footstepSource == null)
        {
            GameObject sourceObject = new GameObject("FootstepAudioSource");
            sourceObject.transform.SetParent(transform, false);
            footstepSource = sourceObject.AddComponent<AudioSource>();
        }

        footstepSource.playOnAwake = false;
        footstepSource.loop = false;
    }

    void PlayManaged(AudioClip clip, float clipVolume, float repeatGuard)
    {
        PlayInternal(clip, repeatGuard, clipVolume);
    }

    void PlayFootstepManaged(AudioClip clip, float clipVolume, float repeatGuard)
    {
        if (clip == null)
            return;

        EnsureFootstepSource();

        if (clip == lastFootstepClip && Time.unscaledTime - lastFootstepPlayTime < repeatGuard)
            return;

        lastFootstepClip = clip;
        lastFootstepPlayTime = Time.unscaledTime;
        footstepSource.Stop();
        footstepSource.PlayOneShot(clip, Mathf.Max(0f, clipVolume) * Mathf.Max(0f, masterSfxVolume));
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
