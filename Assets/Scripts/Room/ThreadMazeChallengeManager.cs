using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ThreadMazeChallengeManager : MonoBehaviour
{
    public enum ChallengeState
    {
        Waiting,
        Running,
        ChallengeSucceeded,
        ChallengeFailed
    }

    [Header("Challenge")]
    [SerializeField, Min(1f)] float timeLimit = 60f;
    [SerializeField] bool requireChallengeNode = true;
    [SerializeField] bool allowDirectRoomSceneTest = true;
    [SerializeField] Vector2 mazeMapSize = new Vector2(38f, 28.5f);
    [SerializeField, Min(1f)] float cameraOrthographicSize = 8.2f;

    [Header("References")]
    [SerializeField] Transform startZone;
    [SerializeField] Transform goalZone;
    [SerializeField] Transform rewardSpawnPoint;
    [SerializeField] TextMeshProUGUI timerLabel;
    [SerializeField] TextMeshProUGUI messageLabel;
    [SerializeField] GameObject temporaryRewardPrefab;

    [Header("Feedback")]
    [SerializeField] Color normalTimerColor = Color.white;
    [SerializeField] Color warningTimerColor = new Color(1f, 0.24f, 0.18f, 1f);
    [SerializeField, Min(0f)] float warningThreshold = 10f;
    [SerializeField, Min(0f)] float warningShakePixels = 8f;
    [SerializeField, Min(0f)] float messageDuration = 2.5f;
    [SerializeField, Min(0.05f)] float failureFeedbackDuration = 0.65f;
    [SerializeField, Min(0.05f)] float mazeFadeDuration = 0.9f;

    [Header("Exit Doors")]
    [SerializeField] GameObject doorTemplate;
    [SerializeField] Vector2 doorLine = new Vector2(8f, 6.1f);

    [Header("Player Shrink")]
    // 이미지 미로(20x15)는 통로가 좁아, 도전방 동안만 플레이어를 축소해 통과 가능하게 한다.
    // 리워드 씬으로 나갈 때 원래 크기로 복구한다.
    [SerializeField, Range(0.2f, 1f)] float challengePlayerScale = 0.6f;

    [Header("Reward Scene")]
    [SerializeField] string rewardSceneName = "ChallengeRewardScene";
    [SerializeField, Min(0f)] float rewardTransitionDelay = 0.9f;

    Transform shrunkPlayer;
    Vector3 shrunkPlayerOriginalScale = Vector3.one;
    bool playerShrunk;

    // 성공/실패 여부를 ChallengeRewardScene으로 전달한다(정적이라 씬 전환에도 유지됨).
    public static bool LastSucceeded;

    ChallengeState state = ChallengeState.Waiting;
    float remainingTime;
    Vector2 timerBasePosition;
    bool timerBasePositionCaptured;
    int lastWarningSecond = int.MaxValue;
    Coroutine messageRoutine;
    Coroutine failureRoutine;
    readonly System.Collections.Generic.List<DoorTrigger> activeDoors = new System.Collections.Generic.List<DoorTrigger>();

    public ChallengeState State => state;
    public bool HasSucceeded => state == ChallengeState.ChallengeSucceeded;
    public bool HasFailed => state == ChallengeState.ChallengeFailed;

    public static bool ShouldHandleCurrentRoom()
    {
        ThreadMazeChallengeManager manager = FindFirstObjectByType<ThreadMazeChallengeManager>(FindObjectsInactive.Include);
        return manager != null && manager.ShouldRunInCurrentScene();
    }

    void Start()
    {
        if (!ShouldRunInCurrentScene())
        {
            Transform root = transform.parent != null ? transform.parent : transform;
            root.gameObject.SetActive(false);
            return;
        }

        BeginChallenge();
    }

    void Update()
    {
        if (state != ChallengeState.Running)
            return;

        remainingTime -= Time.deltaTime;
        UpdateTimerLabel();

        if (remainingTime <= 0f)
            FailChallenge();
    }

    void OnDisable()
    {
        RunHudUI.HideJudgementTimer();
        RestorePlayer(); // 안전망: 예기치 않게 종료돼도 플레이어 크기 복구
    }

    public bool ShouldRunInCurrentScene()
    {
        if (!requireChallengeNode && !Application.isPlaying)
            return true;

        MapNode pending = MapRunState.PendingNode;
        if (pending != null)
            return pending.roomType == RoomType.Challenge;

        string sceneName = SceneManager.GetActiveScene().name;
        return allowDirectRoomSceneTest &&
               (sceneName.StartsWith("RoomScene") || sceneName.StartsWith("ChallengeScene"));
    }

    public void BeginChallenge()
    {
        MapRunState.EnsureRun();
        state = ChallengeState.Running;
        remainingTime = timeLimit;
        lastWarningSecond = int.MaxValue;
        RunHudUI.SetWaveHudVisible(false);
        RunHudUI.ShowJudgementTimer("CHALLENGE", normalTimerColor);
        CaptureTimerBasePosition();
        ClearMessage();
        MovePlayerToStart();
        ShrinkPlayer();
        ConfigureCamera();
        SetExitDoorsVisible(false);
        UpdateTimerLabel();
        Debug.Log("[ThreadMaze] Challenge started.");
    }

    public void NotifyGoalReached()
    {
        if (state != ChallengeState.Running)
            return;

        SucceedChallenge();
    }

    void ShrinkPlayer()
    {
        if (playerShrunk)
            return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            return;

        shrunkPlayer = player.transform;
        shrunkPlayerOriginalScale = shrunkPlayer.localScale;
        shrunkPlayer.localScale = shrunkPlayerOriginalScale * challengePlayerScale;
        playerShrunk = true;
    }

    void RestorePlayer()
    {
        if (!playerShrunk)
            return;

        if (shrunkPlayer != null)
            shrunkPlayer.localScale = shrunkPlayerOriginalScale;
        playerShrunk = false;
    }

    void MovePlayerToStart()
    {
        if (startZone == null)
            return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            return;

        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.position = startZone.position;
        }
        player.transform.position = startZone.position;
    }

    void ConfigureCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        mainCamera.orthographic = true;
        mainCamera.orthographicSize = cameraOrthographicSize;

        PlayerCameraFollow follow = mainCamera.GetComponent<PlayerCameraFollow>();
        if (follow != null)
            follow.ConfigureBounds(mazeMapSize, Vector2.zero, mainCamera.orthographicSize, true);
        else
            mainCamera.transform.position = new Vector3(0f, 0f, -10f);
    }

    void UpdateTimerLabel()
    {
        float shown = Mathf.Max(0f, remainingTime);
        RunHudUI.SetJudgementTimer(shown, timeLimit);

        if (timerLabel == null)
            return;

        timerLabel.text = shown.ToString("0.0") + "s";
        bool warning = shown <= warningThreshold;
        float remainingRatio = timeLimit > 0f ? Mathf.Clamp01(shown / timeLimit) : 0f;
        timerLabel.color = Color.Lerp(warningTimerColor, normalTimerColor, remainingRatio);

        int warningSecond = Mathf.CeilToInt(shown);
        if (warning && warningSecond != lastWarningSecond && warningSecond > 0)
        {
            lastWarningSecond = warningSecond;
            SoundManager.PlayChallengeTimerTick(0.15f);
        }

        CaptureTimerBasePosition();
        if (warning)
        {
            float shakeX = Mathf.Sin(Time.unscaledTime * 42f) * warningShakePixels;
            timerLabel.rectTransform.anchoredPosition = timerBasePosition + new Vector2(shakeX, 0f);
        }
        else
        {
            timerLabel.rectTransform.anchoredPosition = timerBasePosition;
        }
    }

    void CaptureTimerBasePosition()
    {
        if (timerBasePositionCaptured || timerLabel == null)
            return;

        timerBasePosition = timerLabel.rectTransform.anchoredPosition;
        timerBasePositionCaptured = true;
    }

    void SucceedChallenge()
    {
        state = ChallengeState.ChallengeSucceeded;
        remainingTime = Mathf.Max(0f, remainingTime);
        RunHudUI.HideJudgementTimer();
        ShowMessage("도전 성공", new Color(0.3f, 1f, 0.42f, 1f));

        // 보상/다음문은 이 씬에서 처리하지 않는다. 성공 상태만 전달하고
        // ChallengeRewardScene으로 이동한다(거기서 트레저 테이블 + 아이템 + 다음문 처리).
        LastSucceeded = true;
        StartCoroutine(GoToRewardSceneRoutine(rewardTransitionDelay));
        Debug.Log("[ThreadMaze] ChallengeSucceeded -> reward scene");
    }

    IEnumerator GoToRewardSceneRoutine(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);
        RestorePlayer();
        RoomPageTransition.LoadScene(rewardSceneName);
    }

    void FailChallenge()
    {
        if (failureRoutine != null)
            return;

        state = ChallengeState.ChallengeFailed;
        remainingTime = 0f;
        UpdateTimerLabel();
        ShowMessage("도전 실패", new Color(1f, 0.25f, 0.2f, 1f));
        failureRoutine = StartCoroutine(FailChallengeRoutine());
    }

    IEnumerator FailChallengeRoutine()
    {
        RunHudUI.HideJudgementTimer();
        SoundManager.PlayChallengeTimerEnd(0.05f);
        CameraShake.ShakeHorizontal(failureFeedbackDuration, 0.18f, 4.5f);
        ScreenFlash.FlashRed();

        yield return new WaitForSeconds(Mathf.Max(0.05f, failureFeedbackDuration));
        yield return StartCoroutine(FadeMazeOutRoutine());

        // 실패: 보상/트레저 테이블 없이 ChallengeRewardScene으로 이동(거기서 다음문만 표시).
        LastSucceeded = false;
        RestorePlayer();
        RoomPageTransition.LoadScene(rewardSceneName);
        Debug.Log("[ThreadMaze] ChallengeFailed -> reward scene");
        failureRoutine = null;
    }

    IEnumerator FadeMazeOutRoutine()
    {
        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
        TextMeshPro[] texts = GetComponentsInChildren<TextMeshPro>(true);
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        Color[] spriteColors = new Color[sprites.Length];
        Color[] textColors = new Color[texts.Length];

        for (int i = 0; i < sprites.Length; i++)
            spriteColors[i] = sprites[i] != null ? sprites[i].color : Color.white;
        for (int i = 0; i < texts.Length; i++)
            textColors[i] = texts[i] != null ? texts[i].color : Color.white;
        for (int i = 0; i < colliders.Length; i++)
            if (ShouldFadeMazeObject(colliders[i] != null ? colliders[i].gameObject : null))
                colliders[i].enabled = false;

        float duration = Mathf.Max(0.05f, mazeFadeDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float alpha = 1f - Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null || !ShouldFadeMazeObject(sprites[i].gameObject))
                    continue;
                Color color = spriteColors[i];
                color.a *= alpha;
                sprites[i].color = color;
            }
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null || !ShouldFadeMazeObject(texts[i].gameObject))
                    continue;
                Color color = textColors[i];
                color.a *= alpha;
                texts[i].color = color;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < sprites.Length; i++)
            if (sprites[i] != null && ShouldFadeMazeObject(sprites[i].gameObject))
                sprites[i].gameObject.SetActive(false);
        for (int i = 0; i < texts.Length; i++)
            if (texts[i] != null && ShouldFadeMazeObject(texts[i].gameObject))
                texts[i].gameObject.SetActive(false);
    }

    bool ShouldFadeMazeObject(GameObject go)
    {
        if (go == null)
            return false;
        if (doorTemplate != null && go.transform.IsChildOf(doorTemplate.transform))
            return false;
        if (go.GetComponentInParent<DoorTrigger>() != null)
            return false;
        if (go.CompareTag("Player") || go.GetComponentInParent<PlayerController>() != null)
            return false;
        return true;
    }

    void CompleteCurrentRoom()
    {
        if (MapRunState.PendingNode != null)
        {
            BodyConditionUtility.UnlockRequiredMissingSlot(MapRunState.PendingNode);
            MapRunState.CompletePendingRoom();
        }
    }

    Vector3 RewardPosition()
    {
        if (rewardSpawnPoint != null)
            return rewardSpawnPoint.position;
        if (goalZone != null)
            return goalZone.position + Vector3.left * 1.4f;
        return transform.position;
    }

    void SpawnTemporaryReward()
    {
        GameObject reward = temporaryRewardPrefab != null
            ? Instantiate(temporaryRewardPrefab, RewardPosition(), Quaternion.identity, transform)
            : CreateTemporaryRewardPrimitive();

        reward.name = "TemporaryChallengeReward";
        reward.SetActive(true);
    }

    GameObject CreateTemporaryRewardPrimitive()
    {
        GameObject reward = new GameObject("TemporaryChallengeReward");
        reward.transform.SetParent(transform, false);
        reward.transform.position = RewardPosition();
        reward.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

        SpriteRenderer renderer = reward.AddComponent<SpriteRenderer>();
        renderer.sprite = RuntimeSquareSprite();
        renderer.color = new Color(1f, 0.82f, 0.2f, 1f);
        renderer.sortingOrder = 70;
        return reward;
    }

    void ShowMessage(string message, Color color)
    {
        if (messageLabel == null)
            return;

        messageLabel.text = message;
        messageLabel.color = color;
        messageLabel.gameObject.SetActive(true);
        if (messageRoutine != null)
            StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(HideMessageRoutine());
    }

    IEnumerator HideMessageRoutine()
    {
        yield return new WaitForSeconds(messageDuration);
        ClearMessage();
        messageRoutine = null;
    }

    void ClearMessage()
    {
        if (messageLabel != null)
        {
            messageLabel.text = "";
            messageLabel.gameObject.SetActive(false);
        }
    }

    void SetExitDoorsVisible(bool visible)
    {
        for (int i = 0; i < activeDoors.Count; i++)
            if (activeDoors[i] != null)
                activeDoors[i].gameObject.SetActive(visible);
    }

    void BuildNextDoors()
    {
        activeDoors.Clear();
        MapNode current = MapRunState.CurrentNode;
        if (current == null || current.children == null || current.children.Count == 0)
            return;

        GameObject template = doorTemplate != null ? doorTemplate : CreateDoorTemplate();
        template.SetActive(true);

        for (int i = 0; i < current.children.Count; i++)
        {
            GameObject door = i == 0 ? template : Instantiate(template, transform);
            door.name = "ThreadMazeDoor_ToNode_" + current.children[i].id;
            door.transform.SetParent(transform, false);
            door.transform.localPosition = DoorPosition(i, current.children.Count);

            DoorTrigger trigger = door.GetComponent<DoorTrigger>();
            if (trigger == null)
                trigger = door.AddComponent<DoorTrigger>();
            trigger.Configure(current.children[i], true);
            activeDoors.Add(trigger);
        }
    }

    GameObject CreateDoorTemplate()
    {
        GameObject door = new GameObject("ThreadMazeDoor_Template");
        door.transform.SetParent(transform, false);
        door.transform.localScale = new Vector3(1.5f, 0.55f, 1f);

        SpriteRenderer renderer = door.AddComponent<SpriteRenderer>();
        renderer.sprite = RuntimeSquareSprite();
        renderer.color = new Color(0.86f, 0.64f, 0.26f, 1f);
        renderer.sortingOrder = 35;

        BoxCollider2D collider = door.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;

        doorTemplate = door;
        return door;
    }

    Vector3 DoorPosition(int index, int count)
    {
        float x = count <= 1 ? 0f : Mathf.Lerp(-doorLine.x * 0.5f, doorLine.x * 0.5f, index / (float)(count - 1));
        return new Vector3(x, doorLine.y, 0f);
    }

    static Sprite runtimeSquareSprite;

    static Sprite RuntimeSquareSprite()
    {
        if (runtimeSquareSprite != null)
            return runtimeSquareSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        runtimeSquareSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return runtimeSquareSprite;
    }
}
