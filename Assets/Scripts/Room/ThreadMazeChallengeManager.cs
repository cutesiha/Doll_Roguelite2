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
    [SerializeField, Min(1f)] float timeLimit = 40f;
    [SerializeField] bool requireChallengeNode = true;
    [SerializeField] bool allowDirectRoomSceneTest = true;

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

    [Header("Exit Doors")]
    [SerializeField] GameObject doorTemplate;
    [SerializeField] Vector2 doorLine = new Vector2(8f, 6.1f);

    ChallengeState state = ChallengeState.Waiting;
    float remainingTime;
    Vector2 timerBasePosition;
    bool timerBasePositionCaptured;
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

    public bool ShouldRunInCurrentScene()
    {
        if (!requireChallengeNode && !Application.isPlaying)
            return true;

        MapNode pending = MapRunState.PendingNode;
        if (pending != null)
            return pending.roomType == RoomType.Challenge;

        return allowDirectRoomSceneTest && SceneManager.GetActiveScene().name.StartsWith("RoomScene");
    }

    public void BeginChallenge()
    {
        MapRunState.EnsureRun();
        state = ChallengeState.Running;
        remainingTime = timeLimit;
        CaptureTimerBasePosition();
        ClearMessage();
        MovePlayerToStart();
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
        mainCamera.orthographicSize = 5.4f;

        PlayerCameraFollow follow = mainCamera.GetComponent<PlayerCameraFollow>();
        if (follow != null)
            follow.ConfigureBounds(new Vector2(28.47f, 15.87f), Vector2.zero, mainCamera.orthographicSize, true);
        else
            mainCamera.transform.position = new Vector3(0f, 0f, -10f);
    }

    void UpdateTimerLabel()
    {
        if (timerLabel == null)
            return;

        float shown = Mathf.Max(0f, remainingTime);
        timerLabel.text = shown.ToString("0.0") + "s";
        bool warning = shown <= warningThreshold;
        timerLabel.color = warning ? warningTimerColor : normalTimerColor;

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
        ShowMessage("도전 성공", new Color(0.3f, 1f, 0.42f, 1f));

        if (MapRunState.PendingNode != null && MapRunState.PendingNode.roomType == RoomType.Challenge)
            ItemRoomRewardSystem.HandleCombatRoomCleared(MapRunState.PendingNode, RewardPosition());
        else
            SpawnTemporaryReward();

        CompleteCurrentRoom();
        BuildNextDoors();
        Debug.Log("[ThreadMaze] ChallengeSucceeded");
    }

    void FailChallenge()
    {
        state = ChallengeState.ChallengeFailed;
        remainingTime = 0f;
        UpdateTimerLabel();
        ShowMessage("도전 실패", new Color(1f, 0.25f, 0.2f, 1f));
        CompleteCurrentRoom();
        BuildNextDoors();
        Debug.Log("[ThreadMaze] ChallengeFailed");
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
        StopAllCoroutines();
        StartCoroutine(HideMessageRoutine());
    }

    IEnumerator HideMessageRoutine()
    {
        yield return new WaitForSeconds(messageDuration);
        ClearMessage();
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
