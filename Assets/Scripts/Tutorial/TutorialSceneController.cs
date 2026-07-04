using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class TutorialSceneController : MonoBehaviour
{
    enum TutorialStep
    {
        Move,
        Workbench,
        Paper,
        Map,
        Inventory,
        Menu,
        EnemyIntro,
        Attack,
        Door,
        Done
    }

    [Header("Scene")]
    [SerializeField] Camera sceneCamera;
    [SerializeField] PlayerController player;
    [SerializeField] PlayerAttack playerAttack;
    [SerializeField] Transform workbench;
    [SerializeField] string roomSceneName = "RoomScene";
    [SerializeField] Sprite memoSprite;
    [SerializeField] Sprite workbenchSprite;
    [SerializeField] Sprite doorSprite;

    [Header("Tuning")]
    [SerializeField] float workbenchPromptDistance = 3.2f;
    [SerializeField] float doorPromptDistance = 2.6f;
    [SerializeField] Vector2 enemyEntranceStart = new Vector2(-12f, -0.6f);
    [SerializeField] Vector2 enemyEntranceEnd = new Vector2(-3.1f, -0.6f);
    [SerializeField] Vector2 doorPosition = new Vector2(0f, 1.1f);
    [SerializeField] Vector2 cameraBoundsPadding = new Vector2(0.55f, 0.78f);

    [Header("Editable Tutorial UI")]
    [SerializeField] Canvas tutorialCanvas;
    [SerializeField] CanvasGroup movePrompt;
    [SerializeField] CanvasGroup interactPrompt;
    [SerializeField] CanvasGroup mapPrompt;
    [SerializeField] CanvasGroup mapScrollPrompt;
    [SerializeField] CanvasGroup inventoryPrompt;
    [SerializeField] CanvasGroup inventoryDetachPrompt;
    [SerializeField] CanvasGroup inventoryReattachPrompt;
    [SerializeField] CanvasGroup menuPrompt;
    [SerializeField] CanvasGroup attackPrompt;
    [SerializeField] CanvasGroup doorPrompt;
    CanvasGroup paperGroup;
    CanvasGroup pauseOverlay;
    Image fadeImage;
    RectTransform paperRect;
    Vector2 paperShownPosition;
    Transform arrowRoot;
    Transform doorRoot;
    TutorialEnemy activeEnemy;
    InventoryUI inventoryUI;
    RunPauseMenuUI pauseMenuUI;
    GameObject mapButtonObject;
    GameObject inventoryButtonObject;
    GameObject menuButtonObject;
    RunHudUI runHud;
    TutorialStep step;
    GameObject gameplaySkipRoot;
    GameObject gameplaySkipConfirmRoot;
    bool paperReadyForClick;
    bool mapOpened;
    bool mapScrolled;
    float mapScrollStartPosition;
    ScrollRect tutorialMapScrollRect;
    bool inventoryOpened;
    bool inventoryTaskComplete;
    bool inventoryPartDetached;
    BodyPart[] inventoryStartParts;
    BodyPart detachedPart;
    BodySlot detachedSlot;
    bool menuOpened;
    bool attackPromptDismissed;
    bool doorPromptVisible;
    bool exitingToRoom;
    float promptPulseTime;

    static Sprite squareSprite;
    static Sprite circleSprite;

    void Awake()
    {
        if (sceneCamera == null)
            sceneCamera = Camera.main;

        if (player == null)
            player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (playerAttack == null && player != null)
            playerAttack = player.GetComponent<PlayerAttack>();

        if (workbench == null)
        {
            // 씬에 미리 배치된 TutorialWorkbench 재사용 (중복 방지)
            GameObject found = GameObject.Find("TutorialWorkbench");
            workbench = found != null ? found.transform : CreateWorkbench(new Vector2(5.4f, -0.9f)).transform;
        }
        EnsureWorkbenchShadow(workbench);
        AttachInteractableHighlight(workbench, workbenchPromptDistance);

        EnsureCamera();
        EnsureEventSystem();
        EnsureTutorialCanvas();
        EnsureRunHud();
        BuildUi();
        BuildArrow();
        BuildDoor();
        PrepareHudForTutorial();
    }

    IEnumerator Start()
    {
        Time.timeScale = 1f;

        if (player != null)
        {
            player.transform.position = new Vector3(-5.6f, -1.2f, 0f);
            // 튜토리얼: 적에 닿으면 피격 모션은 그대로 재생하되 실제 HP는 깎이지 않게 한다.
            PlayerDamageReceiver pdr = player.GetComponent<PlayerDamageReceiver>();
            if (pdr != null)
                pdr.SetHpLossDisabled(true);
        }

        HideAllPrompts();
        step = TutorialStep.Done;

        TutorialOpeningCutscene openingCutscene = GetComponent<TutorialOpeningCutscene>();
        if (openingCutscene != null && openingCutscene.HasAllPages)
        {
            SetGameplayInputEnabled(false);
            yield return openingCutscene.Play(roomSceneName);
            SetGameplayInputEnabled(true);
        }

        ShowOnly(movePrompt);
        step = TutorialStep.Move;
    }

    void Update()
    {
        promptPulseTime += Time.unscaledDeltaTime;
        PulsePrompt(movePrompt);
        PulsePrompt(interactPrompt);
        PulsePrompt(mapPrompt);
        PulsePrompt(mapScrollPrompt);
        PulsePrompt(inventoryPrompt);
        PulsePrompt(inventoryDetachPrompt);
        PulsePrompt(inventoryReattachPrompt);
        PulsePrompt(menuPrompt);
        PulsePrompt(attackPrompt);
        PulsePrompt(doorPrompt);

        switch (step)
        {
            case TutorialStep.Move:
                UpdateMoveStep();
                break;
            case TutorialStep.Workbench:
                UpdateWorkbenchStep();
                break;
            case TutorialStep.Paper:
                UpdatePaperStep();
                break;
            case TutorialStep.Map:
                UpdateMapStep();
                break;
            case TutorialStep.Inventory:
                UpdateInventoryStep();
                break;
            case TutorialStep.Menu:
                UpdateMenuStep();
                break;
            case TutorialStep.Attack:
                UpdateAttackStep();
                break;
            case TutorialStep.Door:
                UpdateDoorStep();
                break;
        }
    }

    void LateUpdate()
    {
        ClampPlayerToCameraBounds();
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    void UpdateMoveStep()
    {
        if (!WasMovePressed())
            return;

        SetPromptVisible(movePrompt, false);
        SetArrowVisible(true);
        step = TutorialStep.Workbench;
    }

    void UpdateWorkbenchStep()
    {
        UpdateArrow();

        bool nearWorkbench = player != null && Vector2.Distance(player.transform.position, workbench.position) <= workbenchPromptDistance;
        SetPromptVisible(interactPrompt, nearWorkbench);

        if (nearWorkbench && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            SetPromptVisible(interactPrompt, false);
            SetArrowVisible(false);
            StartCoroutine(PaperRoutine());
        }
    }

    void UpdatePaperStep()
    {
        bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool pressedE = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        if (!paperReadyForClick || (!clicked && !pressedE))
            return;

        paperReadyForClick = false;
        StartCoroutine(ClosePaperRoutine());
    }

    void UpdateMapStep()
    {
        if (IsMapOpen())
        {
            if (!mapOpened)
            {
                mapOpened = true;
                mapScrolled = false;
                tutorialMapScrollRect = FindMapScrollRect();
                mapScrollStartPosition = tutorialMapScrollRect != null
                    ? tutorialMapScrollRect.verticalNormalizedPosition
                    : 1f;
                ShowOnly(mapScrollPrompt);
            }

            SetPromptVisible(mapPrompt, false);

            if (!mapScrolled)
            {
                float wheel = Mouse.current != null ? Mathf.Abs(Mouse.current.scroll.ReadValue().y) : 0f;
                float scrollDelta = tutorialMapScrollRect != null
                    ? Mathf.Abs(tutorialMapScrollRect.verticalNormalizedPosition - mapScrollStartPosition)
                    : 0f;
                if (wheel > 0.01f || scrollDelta > 0.002f)
                {
                    mapScrolled = true;
                    SetPromptVisible(mapScrollPrompt, false);
                }
            }

            return;
        }

        SetPromptVisible(mapScrollPrompt, false);
        if (mapOpened && mapScrolled)
            ShowInventoryPrompt();
        else if (mapOpened)
        {
            mapOpened = false;
            ShowOnly(mapPrompt);
        }
    }

    void UpdateInventoryStep()
    {
        if (inventoryUI == null)
            inventoryUI = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);

        if (inventoryUI != null && inventoryUI.IsOpen)
        {
            if (!inventoryOpened)
            {
                inventoryOpened = true;
                CaptureInventoryStartState();
            }

            SetPromptVisible(inventoryPrompt, false);
            UpdateInventoryDragTask();
            return;
        }

        SetPromptVisible(inventoryDetachPrompt, false);
        SetPromptVisible(inventoryReattachPrompt, false);
        if (inventoryTaskComplete)
            ShowMenuPrompt();
        else if (inventoryOpened)
            SetPromptVisible(inventoryPrompt, true);
    }

    void UpdateMenuStep()
    {
        if (pauseMenuUI == null && runHud != null)
            pauseMenuUI = runHud.GetComponent<RunPauseMenuUI>();

        if (pauseMenuUI != null && pauseMenuUI.IsAnyOpen)
        {
            menuOpened = true;
            SetPromptVisible(menuPrompt, false);
            return;
        }

        if (menuOpened)
            StartCoroutine(EnemyIntroRoutine());
    }

    void UpdateAttackStep()
    {
        if (!attackPromptDismissed && WasAttackPressed())
        {
            attackPromptDismissed = true;
            SetPromptVisible(attackPrompt, false);
        }

        if (activeEnemy == null)
        {
            ShowDoor();
            step = TutorialStep.Door;
        }
    }

    void UpdateDoorStep()
    {
        bool nearDoor = player != null && doorRoot != null && Vector2.Distance(player.transform.position, doorRoot.position) <= doorPromptDistance;
        if (nearDoor != doorPromptVisible)
        {
            doorPromptVisible = nearDoor;
            SetPromptVisible(doorPrompt, nearDoor);
        }

        if (nearDoor && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
            StartCoroutine(ExitToRoomRoutine());
    }

    IEnumerator PaperRoutine()
    {
        step = TutorialStep.Paper;
        SoundManager.PlayTutorialPaperOpen(0f);
        SetCanvasGroup(paperGroup, true, 1f);
        Vector2 shown = paperShownPosition;
        Vector2 hidden = shown + new Vector2(0f, -720f);
        Vector2 overshoot = shown + new Vector2(0f, 42f);
        paperRect.anchoredPosition = hidden;

        yield return AnimateRect(paperRect, hidden, overshoot, 0.42f, EaseOutCubic);
        yield return AnimateRect(paperRect, overshoot, shown, 0.16f, EaseOutCubic);
        paperReadyForClick = true;
    }

    IEnumerator ClosePaperRoutine()
    {
        SoundManager.PlayTutorialPaperClose(0f);
        Vector2 shown = paperRect.anchoredPosition;
        Vector2 bump = shown + new Vector2(0f, 32f);
        Vector2 hidden = paperShownPosition + new Vector2(0f, -760f);
        yield return AnimateRect(paperRect, shown, bump, 0.10f, EaseOutCubic);
        yield return AnimateRect(paperRect, bump, hidden, 0.34f, EaseInCubic);
        SetCanvasGroup(paperGroup, false, 0f);
        ShowMapPrompt();
    }

    void ShowMapPrompt()
    {
        if (mapButtonObject != null)
            mapButtonObject.SetActive(true);

        mapOpened = false;
        mapScrolled = false;
        tutorialMapScrollRect = null;
        ShowOnly(mapPrompt);
        step = TutorialStep.Map;
    }

    void ShowInventoryPrompt()
    {
        if (inventoryButtonObject != null)
            inventoryButtonObject.SetActive(true);

        inventoryOpened = false;
        inventoryTaskComplete = false;
        inventoryPartDetached = false;
        inventoryStartParts = null;
        detachedPart = null;
        ShowOnly(inventoryPrompt);
        step = TutorialStep.Inventory;
    }

    void CaptureInventoryStartState()
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null || inventory.equipped == null)
            return;

        inventoryStartParts = new BodyPart[inventory.equipped.Length];
        System.Array.Copy(inventory.equipped, inventoryStartParts, inventory.equipped.Length);
        ShowOnly(inventoryDetachPrompt);
    }

    void UpdateInventoryDragTask()
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null)
            return;

        if (inventoryStartParts == null)
            CaptureInventoryStartState();

        if (!inventoryPartDetached && inventoryStartParts != null)
        {
            int count = Mathf.Min(inventoryStartParts.Length, inventory.equipped.Length);
            for (int i = 0; i < count; i++)
            {
                BodyPart originalPart = inventoryStartParts[i];
                if (originalPart == null || inventory.equipped[i] != null || !StorageContains(inventory, originalPart))
                    continue;

                inventoryPartDetached = true;
                detachedPart = originalPart;
                detachedSlot = (BodySlot)i;
                ShowOnly(inventoryReattachPrompt);
                break;
            }
        }

        if (!inventoryPartDetached)
        {
            SetPromptVisible(inventoryDetachPrompt, true);
            SetPromptVisible(inventoryReattachPrompt, false);
            return;
        }

        int detachedIndex = (int)detachedSlot;
        bool reattached = detachedIndex >= 0
            && detachedIndex < inventory.equipped.Length
            && inventory.equipped[detachedIndex] == detachedPart;
        if (reattached)
        {
            inventoryTaskComplete = true;
            SetPromptVisible(inventoryDetachPrompt, false);
            SetPromptVisible(inventoryReattachPrompt, false);
        }
        else
        {
            SetPromptVisible(inventoryDetachPrompt, false);
            SetPromptVisible(inventoryReattachPrompt, true);
        }
    }

    static bool StorageContains(InventoryManager inventory, BodyPart part)
    {
        if (inventory == null || inventory.storage == null || part == null)
            return false;

        for (int i = 0; i < inventory.storage.Length; i++)
        {
            if (inventory.storage[i] == part)
                return true;
        }

        return false;
    }

    ScrollRect FindMapScrollRect()
    {
        if (runHud == null)
            return null;

        Transform mapScroll = FindChildRecursive(runHud.transform, "MapScroll");
        return mapScroll != null ? mapScroll.GetComponent<ScrollRect>() : null;
    }

    void ShowMenuPrompt()
    {
        if (menuButtonObject != null)
            menuButtonObject.SetActive(true);

        menuOpened = false;
        ShowOnly(menuPrompt);
        step = TutorialStep.Menu;
    }

    IEnumerator EnemyIntroRoutine()
    {
        step = TutorialStep.EnemyIntro;
        SetPromptVisible(mapPrompt, false);
        SetPromptVisible(inventoryPrompt, false);
        SetPromptVisible(menuPrompt, false);
        SetCanvasGroup(pauseOverlay, true, 1f);
        Time.timeScale = 0f;

        activeEnemy = CreateEnemy(enemyEntranceStart);
        Transform enemyTransform = activeEnemy.transform;
        float elapsed = 0f;
        const float duration = 2.1f;
        while (elapsed < duration && activeEnemy != null)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            enemyTransform.position = Vector2.Lerp(enemyEntranceStart, enemyEntranceEnd, EaseOutCubic(t));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (activeEnemy != null)
        {
            enemyTransform.position = enemyEntranceEnd;
            yield return StartCoroutine(EnemySurpriseRoutine(activeEnemy));
        }

        Time.timeScale = 1f;
        if (activeEnemy != null)
            activeEnemy.BeginApproachPlayer();

        SetCanvasGroup(pauseOverlay, false, 0f);
        ShowOnly(attackPrompt);
        attackPromptDismissed = false;
        step = TutorialStep.Attack;
    }

    IEnumerator EnemySurpriseRoutine(TutorialEnemy enemy)
    {
        SpriteRenderer renderer = enemy.GetComponentInChildren<SpriteRenderer>();
        Color baseColor = renderer != null ? renderer.color : Color.white;
        Vector3 basePosition = enemy.transform.position;
        float elapsed = 0f;
        const float duration = 0.62f;
        while (elapsed < duration && enemy != null)
        {
            float t = elapsed / duration;
            float shake = Mathf.Sin(t * Mathf.PI * 16f) * 0.10f * (1f - t);
            enemy.transform.position = basePosition + new Vector3(shake, Random.Range(-0.04f, 0.04f) * (1f - t), 0f);
            if (renderer != null)
                renderer.color = Color.Lerp(new Color(1f, 0.22f, 0.16f, 1f), baseColor, t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (enemy != null)
            enemy.transform.position = basePosition;
        if (renderer != null)
            renderer.color = baseColor;
    }

    IEnumerator ExitToRoomRoutine()
    {
        if (exitingToRoom) yield break;
        exitingToRoom = true;

        step = TutorialStep.Done;
        SetPromptVisible(doorPrompt, false);
        SetArrowVisible(false);
        HideGameplaySkipUI();
        Time.timeScale = 1f;

        GameSaveSystem.MarkTutorialDone();

        if (runHud != null)
            Destroy(runHud.gameObject);

        RunHudUI.ShowControlHintsOnNextRoom = false;

        if (fadeImage != null)
            fadeImage.transform.SetAsLastSibling();
        float elapsed = 0f;
        const float duration = 0.55f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (fadeImage != null)
                SetImageAlpha(fadeImage, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        SceneManager.LoadScene(roomSceneName);
    }

    void SetGameplayInputEnabled(bool enabled)
    {
        if (player != null)
        {
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (!enabled && body != null)
                body.linearVelocity = Vector2.zero;
            player.enabled = enabled;
        }

        if (playerAttack != null)
            playerAttack.enabled = enabled;
    }

    void EnsureCamera()
    {
        if (sceneCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            sceneCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        sceneCamera.orthographic = true;
        sceneCamera.orthographicSize = 5.4f;
        sceneCamera.transform.position = new Vector3(0f, 0f, -10f);
        sceneCamera.clearFlags = CameraClearFlags.SolidColor;
        sceneCamera.backgroundColor = Color.black;
    }

    void ClampPlayerToCameraBounds()
    {
        if (player == null || sceneCamera == null || !sceneCamera.orthographic)
            return;

        const float fullHdAspect = 16f / 9f;
        Vector3 cameraPosition = sceneCamera.transform.position;
        float halfHeight = sceneCamera.orthographicSize;
        float halfWidth = halfHeight * fullHdAspect;

        Vector3 position = player.transform.position;
        float minX = cameraPosition.x - halfWidth + cameraBoundsPadding.x;
        float maxX = cameraPosition.x + halfWidth - cameraBoundsPadding.x;
        float minY = cameraPosition.y - halfHeight + cameraBoundsPadding.y;
        float maxY = cameraPosition.y + halfHeight - cameraBoundsPadding.y;

        Vector3 clamped = new Vector3(
            Mathf.Clamp(position.x, minX, maxX),
            Mathf.Clamp(position.y, minY, maxY),
            position.z);

        if ((clamped - position).sqrMagnitude <= 0.0001f)
            return;

        player.transform.position = clamped;
    }

    void EnsureTutorialCanvas()
    {
        if (tutorialCanvas == null)
        {
            GameObject existing = GameObject.Find("TutorialCanvas");
            if (existing != null)
                tutorialCanvas = existing.GetComponent<Canvas>();
        }

        if (tutorialCanvas == null)
        {
            GameObject newCanvasObject = new GameObject("TutorialCanvas");
            tutorialCanvas = newCanvasObject.AddComponent<Canvas>();
        }

        GameObject canvasObject = tutorialCanvas.gameObject;
        canvasObject.SetActive(true);
        tutorialCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        tutorialCanvas.overrideSorting = true;
        tutorialCanvas.sortingOrder = 900;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
            canvasObject.AddComponent<GraphicRaycaster>();
    }

    void EnsureRunHud()
    {
        runHud = FindFirstObjectByType<RunHudUI>(FindObjectsInactive.Include);
        if (runHud == null)
        {
            GameObject hud = new GameObject("RunHudCanvas");
            hud.AddComponent<RectTransform>();
            runHud = hud.AddComponent<RunHudUI>();
        }

        if (runHud == null)
            return;

        inventoryUI = runHud.GetComponentInChildren<InventoryUI>(true);
        pauseMenuUI = runHud.GetComponent<RunPauseMenuUI>();
    }

    void BuildUi()
    {
        pauseOverlay = CreateFullScreenOverlay("TutorialPauseOverlay", new Color(0f, 0f, 0f, 0.36f), false);
        fadeImage = CreateFullScreenImage("TutorialFade", Color.black);
        SetImageAlpha(fadeImage, 0f);

        movePrompt = EnsurePrompt(movePrompt, "MovePrompt", "[WASD]로 움직이기", new Vector2(-560f, 250f), new Vector2(430f, 142f));
        interactPrompt = EnsurePrompt(interactPrompt, "InteractPrompt", "[E] 키를 눌러 상호작용", new Vector2(530f, -305f), new Vector2(520f, 132f));
        mapPrompt = EnsurePrompt(mapPrompt, "MapPrompt", "[M] 키를 눌러 지도 열기", new Vector2(0f, 338f), new Vector2(650f, 132f));
        inventoryPrompt = EnsurePrompt(inventoryPrompt, "InventoryPrompt", "[Tab] 키를 눌러 인벤토리 열기", new Vector2(0f, 338f), new Vector2(650f, 132f));
        menuPrompt = EnsurePrompt(menuPrompt, "MenuPrompt", "[ESC] 키를 눌러 메뉴 열기", new Vector2(0f, 338f), new Vector2(650f, 132f));
        attackPrompt = EnsurePrompt(attackPrompt, "AttackPrompt", "방향키로 공격", new Vector2(530f, 260f), new Vector2(420f, 132f));
        doorPrompt = EnsurePrompt(doorPrompt, "DoorPrompt", "[Enter]를 눌러 들어가기", new Vector2(0f, -335f), new Vector2(560f, 132f));
        mapScrollPrompt = EnsurePrompt(mapScrollPrompt, "MapScrollPrompt", "스크롤하여 지도 보기", new Vector2(420f, 315f), new Vector2(520f, 116f));
        inventoryDetachPrompt = EnsurePrompt(inventoryDetachPrompt, "InventoryDetachPrompt", "부위를 드래그 하여 슬롯에 장착", new Vector2(0f, 365f), new Vector2(760f, 116f));
        inventoryReattachPrompt = EnsurePrompt(inventoryReattachPrompt, "InventoryReattachPrompt", "부위를 드래그 하여 다시 부착", new Vector2(0f, 365f), new Vector2(760f, 116f));
        BuildPaper();
        HideAllPrompts();
    }

    CanvasGroup CreateFullScreenOverlay(string objectName, Color color, bool visible)
    {
        Image image = CreateFullScreenImage(objectName, color);
        CanvasGroup group = image.gameObject.AddComponent<CanvasGroup>();
        SetCanvasGroup(group, visible, visible ? color.a : 0f);
        return group;
    }

    Image CreateFullScreenImage(string objectName, Color color)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(tutorialCanvas.transform, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    CanvasGroup EnsurePrompt(CanvasGroup prompt, string objectName, string text, Vector2 position, Vector2 size)
    {
        if (prompt != null)
            return prompt;

        if (tutorialCanvas != null)
        {
            Transform existing = tutorialCanvas.transform.Find(objectName);
            if (existing != null)
            {
                CanvasGroup existingGroup = existing.GetComponent<CanvasGroup>();
                if (existingGroup == null)
                    existingGroup = existing.gameObject.AddComponent<CanvasGroup>();
                return existingGroup;
            }
        }

        return CreatePrompt(objectName, text, position, size);
    }

#if UNITY_EDITOR
    [ContextMenu("Tutorial/Create Editable Prompt Objects")]
    void CreateEditablePromptObjects()
    {
        EnsureTutorialCanvas();
        movePrompt = EnsurePrompt(movePrompt, "MovePrompt", "[WASD]로 움직이기", new Vector2(-560f, 250f), new Vector2(430f, 142f));
        interactPrompt = EnsurePrompt(interactPrompt, "InteractPrompt", "[E] 키를 눌러 상호작용", new Vector2(530f, -305f), new Vector2(520f, 132f));
        mapPrompt = EnsurePrompt(mapPrompt, "MapPrompt", "[M] 키를 눌러 지도 열기", new Vector2(0f, 338f), new Vector2(650f, 132f));
        inventoryPrompt = EnsurePrompt(inventoryPrompt, "InventoryPrompt", "[Tab] 키를 눌러 인벤토리 열기", new Vector2(0f, 338f), new Vector2(650f, 132f));
        menuPrompt = EnsurePrompt(menuPrompt, "MenuPrompt", "[ESC] 키를 눌러 메뉴 열기", new Vector2(0f, 338f), new Vector2(650f, 132f));
        attackPrompt = EnsurePrompt(attackPrompt, "AttackPrompt", "방향키로 공격", new Vector2(530f, 260f), new Vector2(420f, 132f));
        doorPrompt = EnsurePrompt(doorPrompt, "DoorPrompt", "[Enter]를 눌러 들어가기", new Vector2(0f, -335f), new Vector2(560f, 132f));
        mapScrollPrompt = EnsurePrompt(mapScrollPrompt, "MapScrollPrompt", "스크롤하여 지도 보기", new Vector2(420f, 315f), new Vector2(520f, 116f));
        inventoryDetachPrompt = EnsurePrompt(inventoryDetachPrompt, "InventoryDetachPrompt", "부위를 드래그 하여 슬롯에 장착", new Vector2(0f, 365f), new Vector2(760f, 116f));
        inventoryReattachPrompt = EnsurePrompt(inventoryReattachPrompt, "InventoryReattachPrompt", "부위를 드래그 하여 다시 부착", new Vector2(0f, 365f), new Vector2(760f, 116f));
        BuildPaper();
        if (paperRect != null)
            paperRect.anchoredPosition = paperShownPosition;
        SetCanvasGroup(paperGroup, true, 1f);
        HideAllPrompts();
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    CanvasGroup CreatePrompt(string objectName, string text, Vector2 position, Vector2 size)
    {
        GameObject root = new GameObject(objectName);
        root.transform.SetParent(tutorialCanvas.transform, false);
        RectTransform rect = root.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        CanvasGroup group = root.AddComponent<CanvasGroup>();

        Image background = root.AddComponent<Image>();
        background.color = new Color(0.98f, 0.94f, 0.82f, 0.94f);
        background.raycastTarget = false;

        CreatePromptBorder(root.transform, new Color(0.04f, 0.035f, 0.03f, 0.96f));

        GameObject textObject = new GameObject("Label");
        textObject.transform.SetParent(root.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 16f);
        textRect.offsetMax = new Vector2(-24f, -16f);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = UIThinDungFont.Get();
        label.fontSize = 42f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.04f, 0.035f, 0.03f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;

        return group;
    }

    void CreatePromptBorder(Transform parent, Color color)
    {
        GameObject borderObject = new GameObject("Border");
        borderObject.transform.SetParent(parent, false);
        RectTransform borderRect = borderObject.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;
        TutorialPromptBorder border = borderObject.AddComponent<TutorialPromptBorder>();
        border.color = color;
        border.raycastTarget = false;
    }

    void AddDashedBorder(RectTransform parent, Vector2 size, Color color)
    {
        const float dash = 34f;
        const float gap = 18f;
        AddDashedEdge(parent, new Vector2(-size.x * 0.5f, size.y * 0.5f), Vector2.right, size.x, dash, gap, color);
        AddDashedEdge(parent, new Vector2(-size.x * 0.5f, -size.y * 0.5f), Vector2.right, size.x, dash, gap, color);
        AddDashedEdge(parent, new Vector2(-size.x * 0.5f, -size.y * 0.5f), Vector2.up, size.y, dash, gap, color);
        AddDashedEdge(parent, new Vector2(size.x * 0.5f, -size.y * 0.5f), Vector2.up, size.y, dash, gap, color);
    }

    void AddDashedEdge(RectTransform parent, Vector2 start, Vector2 direction, float length, float dash, float gap, Color color)
    {
        float offset = 0f;
        int index = 0;
        while (offset < length)
        {
            float segment = Mathf.Min(dash, length - offset);
            GameObject go = new GameObject("Dash_" + index);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = start + direction * (offset + segment * 0.5f);
            rect.sizeDelta = Mathf.Abs(direction.x) > 0f ? new Vector2(segment, 5f) : new Vector2(5f, segment);
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            offset += dash + gap;
            index++;
        }
    }

    void BuildPaper()
    {
        if (tutorialCanvas != null)
        {
            Transform existing = tutorialCanvas.transform.Find("S6Paper");
            if (existing != null)
            {
                paperRect = existing as RectTransform;
                if (paperRect == null)
                    paperRect = existing.GetComponent<RectTransform>();

                paperGroup = existing.GetComponent<CanvasGroup>();
                if (paperGroup == null)
                    paperGroup = existing.gameObject.AddComponent<CanvasGroup>();

                if (paperRect != null)
                {
                    paperShownPosition = paperRect.anchoredPosition;
                    paperRect.anchoredPosition = paperShownPosition + new Vector2(0f, -760f);
                }

                SetCanvasGroup(paperGroup, false, 0f);
                return;
            }
        }

        GameObject paper = new GameObject("S6Paper");
        paper.transform.SetParent(tutorialCanvas.transform, false);
        paperRect = paper.AddComponent<RectTransform>();
        paperRect.anchorMin = paperRect.anchorMax = new Vector2(0.5f, 0.5f);
        paperRect.pivot = new Vector2(0.5f, 0.5f);
        paperGroup = paper.AddComponent<CanvasGroup>();

        Image paperImage = paper.AddComponent<Image>();

        LoadMemoSpriteIfMissing();
        if (memoSprite != null)
        {
            Vector2 size = FitMemoSize(memoSprite, 1000f, 680f);
            paperRect.sizeDelta = size;
            paperImage.sprite = memoSprite;
            paperImage.color = Color.white;
            paperImage.type = Image.Type.Simple;
            paperImage.preserveAspect = true;

            AddPaperText(paper.transform, "화면 클릭 또는 [E]", new Vector2(0f, -size.y * 0.5f - 38f), new Vector2(420f, 54f), 28f, TextAlignmentOptions.Center);
        }
        else
        {
            paperRect.sizeDelta = new Vector2(860f, 520f);
            paperImage.color = new Color(0.98f, 0.96f, 0.90f, 1f);

            AddDashedBorder(paperRect, paperRect.sizeDelta, new Color(0.04f, 0.035f, 0.03f, 0.64f));

            TextMeshProUGUI number = AddPaperText(paper.transform, "#S6", new Vector2(-370f, 210f), new Vector2(180f, 70f), 48f, TextAlignmentOptions.Center);
            number.color = Color.black;

            Image cloud = AddPaperImage(paper.transform, "PaperCloud", new Vector2(0f, 34f), new Vector2(640f, 280f), new Color(0.72f, 0.72f, 0.70f, 0.34f));
            cloud.sprite = CircleSprite();
            cloud.type = Image.Type.Sliced;

            AddPaperText(paper.transform, "언제든 준비가 되면\n떠나자.\n단추가 너의 여정을\n도와줄거야.", new Vector2(40f, 24f), new Vector2(650f, 300f), 43f, TextAlignmentOptions.Center);
            AddPaperText(paper.transform, "화면 클릭 또는 [E]", new Vector2(240f, -202f), new Vector2(360f, 54f), 28f, TextAlignmentOptions.Center);
        }

        paperShownPosition = Vector2.zero;
        paperRect.anchoredPosition = paperShownPosition + new Vector2(0f, -760f);
        SetCanvasGroup(paperGroup, false, 0f);
    }

    void LoadMemoSpriteIfMissing()
    {
        if (memoSprite != null)
            return;

        memoSprite = Resources.Load<Sprite>("Sprites/tutorial/memo");

#if UNITY_EDITOR
        if (memoSprite == null)
            memoSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/tutorial/memo.png");
#endif
    }

    static Vector2 FitMemoSize(Sprite sprite, float maxWidth, float maxHeight)
    {
        float width = sprite.rect.width;
        float height = sprite.rect.height;
        if (width <= 0f || height <= 0f)
            return new Vector2(maxWidth, maxHeight);

        float scale = Mathf.Min(maxWidth / width, maxHeight / height);
        return new Vector2(width * scale, height * scale);
    }

    TextMeshProUGUI AddPaperText(Transform parent, string text, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject("PaperText");
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.font = UIThinDungFont.Get();
        label.text = text;
        label.fontSize = fontSize;
        label.color = new Color(0.04f, 0.035f, 0.03f, 1f);
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }

    Image AddPaperImage(Transform parent, string objectName, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    void BuildArrow()
    {
        if (arrowRoot == null)
        {
            // 씬에 미리 배치된 오브젝트가 있으면 재사용
            GameObject existing = GameObject.Find("WorkbenchArrow");
            arrowRoot = existing != null ? existing.transform : new GameObject("WorkbenchArrow").transform;
        }
        if (arrowRoot.childCount > 0) { SetArrowVisible(false); return; }

        AddArrowPart(arrowRoot, "Shaft", new Vector2(-0.10f, 0f), new Vector2(1.15f, 0.18f), 0f);
        AddArrowPart(arrowRoot, "HeadA", new Vector2(0.54f, 0.17f), new Vector2(0.48f, 0.18f), -38f);
        AddArrowPart(arrowRoot, "HeadB", new Vector2(0.54f, -0.17f), new Vector2(0.48f, 0.18f), 38f);
        SetArrowVisible(false);
    }

    void AddArrowPart(Transform parent, string objectName, Vector2 localPosition, Vector2 scale, float angle)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSprite();
        renderer.color = new Color(0.18f, 0.09f, 0.035f, 0.66f);
        renderer.sortingOrder = 220;
    }

    void BuildGameplaySkipUI()
    {
        if (tutorialCanvas == null)
            return;

        // Top-left skip button
        gameplaySkipRoot = new GameObject("GameplaySkipButton");
        gameplaySkipRoot.transform.SetParent(tutorialCanvas.transform, false);
        RectTransform skipRect = gameplaySkipRoot.AddComponent<RectTransform>();
        skipRect.anchorMin = skipRect.anchorMax = new Vector2(0f, 1f);
        skipRect.pivot = new Vector2(0f, 1f);
        skipRect.sizeDelta = new Vector2(220f, 80f);
        skipRect.anchoredPosition = new Vector2(24f, -18f);

        TextMeshProUGUI label = gameplaySkipRoot.AddComponent<TextMeshProUGUI>();
        label.font = UIThinDungFont.Get();
        label.text = "skip";
        label.fontSize = 44f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.30f, 0.11f, 0.05f, 0.82f);
        label.outlineWidth = 0.16f;
        label.outlineColor = new Color(0.18f, 0.055f, 0.025f, 0.74f);
        label.raycastTarget = true;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        Button btn = gameplaySkipRoot.AddComponent<Button>();
        btn.targetGraphic = label;
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.62f, 0.62f, 0.62f, 1f);
        cb.selectedColor = cb.highlightedColor;
        cb.pressedColor = new Color(0.48f, 0.48f, 0.48f, 1f);
        cb.fadeDuration = 0.10f;
        btn.colors = cb;
        btn.onClick.AddListener(ShowGameplaySkipConfirm);

        // Confirmation dialog
        gameplaySkipConfirmRoot = new GameObject("GameplaySkipConfirm");
        gameplaySkipConfirmRoot.transform.SetParent(tutorialCanvas.transform, false);
        RectTransform confirmRect = gameplaySkipConfirmRoot.AddComponent<RectTransform>();
        confirmRect.anchorMin = Vector2.zero;
        confirmRect.anchorMax = Vector2.one;
        confirmRect.offsetMin = Vector2.zero;
        confirmRect.offsetMax = Vector2.zero;

        // Dark backdrop
        GameObject backdrop = new GameObject("Backdrop");
        backdrop.transform.SetParent(gameplaySkipConfirmRoot.transform, false);
        RectTransform backdropRect = backdrop.AddComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        Image backdropImg = backdrop.AddComponent<Image>();
        backdropImg.color = new Color(0f, 0f, 0f, 0.44f);
        backdropImg.raycastTarget = true;

        // Panel
        GameObject panel = new GameObject("ConfirmPanel");
        panel.transform.SetParent(gameplaySkipConfirmRoot.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(820f, 340f);
        panelRect.anchoredPosition = Vector2.zero;
        Image panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.97f, 0.91f, 0.80f, 1f);
        panelImg.raycastTarget = true;

        // Question text
        GameObject questionObj = new GameObject("QuestionText");
        questionObj.transform.SetParent(panel.transform, false);
        RectTransform questionRect = questionObj.AddComponent<RectTransform>();
        questionRect.anchorMin = questionRect.anchorMax = new Vector2(0.5f, 0.5f);
        questionRect.anchoredPosition = new Vector2(0f, 68f);
        questionRect.sizeDelta = new Vector2(740f, 100f);
        TextMeshProUGUI questionText = questionObj.AddComponent<TextMeshProUGUI>();
        questionText.font = UIThinDungFont.Get();
        questionText.text = "스킵하시겠습니까?";
        questionText.fontSize = 44f;
        questionText.alignment = TextAlignmentOptions.Center;
        questionText.color = Color.black;
        questionText.raycastTarget = false;

        // Yes button
        Button yesBtn = CreateConfirmAnswerButton(panel.transform, "예", new Vector2(-170f, -72f));
        yesBtn.onClick.AddListener(() =>
        {
            SoundManager.PlayClick(0f);
            gameplaySkipConfirmRoot.SetActive(false);
            GameSaveSystem.MarkTutorialDone();
            StartCoroutine(ExitToRoomRoutine());
        });

        // No button
        Button noBtn = CreateConfirmAnswerButton(panel.transform, "아니오", new Vector2(170f, -72f));
        noBtn.onClick.AddListener(() =>
        {
            SoundManager.PlayClick(0f);
            gameplaySkipConfirmRoot.SetActive(false);
        });

        gameplaySkipConfirmRoot.SetActive(false);
    }

    Button CreateConfirmAnswerButton(Transform parent, string text, Vector2 position)
    {
        GameObject go = new GameObject("AnswerButton_" + text);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(240f, 80f);

        Image img = go.AddComponent<Image>();
        img.color = new Color(1f, 0.96f, 0.88f, 1f);
        img.raycastTarget = true;

        TextMeshProUGUI label = new GameObject("Label").AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(go.transform, false);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label.font = UIThinDungFont.Get();
        label.text = text;
        label.fontSize = 48f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.black;
        label.raycastTarget = false;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        cb.pressedColor = new Color(0.64f, 0.64f, 0.64f, 1f);
        cb.fadeDuration = 0.08f;
        btn.colors = cb;
        return btn;
    }

    void ShowGameplaySkipConfirm()
    {
        if (gameplaySkipConfirmRoot == null || step == TutorialStep.Done)
            return;
        SoundManager.PlayClick(0f);
        gameplaySkipConfirmRoot.SetActive(true);
        gameplaySkipConfirmRoot.transform.SetAsLastSibling();
    }

    void HideGameplaySkipUI()
    {
        if (gameplaySkipRoot != null)
            gameplaySkipRoot.SetActive(false);
        if (gameplaySkipConfirmRoot != null)
            gameplaySkipConfirmRoot.SetActive(false);
    }

    void BuildDoor()
    {
        GameObject door = new GameObject("TutorialExitDoor");
        doorRoot = door.transform;
        doorRoot.position = doorPosition;

        Sprite sprite = doorSprite;
#if UNITY_EDITOR
        if (sprite == null)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/door/enemyroom.png");
            foreach (Object a in assets)
                if (a is Sprite s) { sprite = s; break; }
        }
#endif

        // 다른 문들과 동일한 크기/콜라이더를 쓰도록 DoorSpriteCatalog 설정을 적용한다.
        DoorSpriteCatalog catalog = DoorSpriteCatalog.Load();

        if (sprite != null)
        {
            GameObject spriteGO = new GameObject("DoorSprite");
            spriteGO.transform.SetParent(door.transform, false);

            float visualScale = catalog != null ? catalog.doorVisualScale : 2.4928f;
            Vector2 visualOffset = catalog != null ? catalog.doorVisualOffset : Vector2.zero;
            spriteGO.transform.localScale = new Vector3(visualScale, visualScale, 1f);
            spriteGO.transform.localPosition = new Vector3(visualOffset.x, visualOffset.y, 0f);

            SpriteRenderer sr = spriteGO.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 20;
        }
        else
        {
            AddSpriteBlock(doorRoot, "DoorPanel", Vector2.zero, new Vector2(1.15f, 1.75f), new Color(0.18f, 0.12f, 0.09f, 1f), 20);
            AddSpriteBlock(doorRoot, "DoorInner", new Vector2(0f, -0.04f), new Vector2(0.82f, 1.38f), new Color(0.48f, 0.30f, 0.18f, 1f), 21);
            AddSpriteBlock(doorRoot, "DoorKnob", new Vector2(0.28f, -0.05f), new Vector2(0.11f, 0.11f), new Color(0.94f, 0.72f, 0.28f, 1f), 22);
        }

        AddDoorInteractionCollider(door, catalog);
        AttachInteractableHighlight(doorRoot, doorPromptDistance);
        door.SetActive(false);
    }

    // 튜토리얼 문에 다른 문(DoorTrigger)과 동일한 상호작용 콜라이더를 붙인다.
    // 문 루트의 스케일이 1이므로 카탈로그의 월드 오프셋 좌표를 그대로 사용할 수 있다.
    void AddDoorInteractionCollider(GameObject doorObject, DoorSpriteCatalog catalog)
    {
        if (catalog != null && catalog.doorColliderPath != null && catalog.doorColliderPath.Length >= 3)
        {
            PolygonCollider2D poly = doorObject.GetComponent<PolygonCollider2D>();
            if (poly == null)
                poly = doorObject.AddComponent<PolygonCollider2D>();
            poly.isTrigger = true;
            poly.pathCount = 1;
            poly.SetPath(0, catalog.doorColliderPath);
            return;
        }

        BoxCollider2D box = doorObject.GetComponent<BoxCollider2D>();
        if (box == null)
            box = doorObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = catalog != null ? catalog.doorColliderSize : new Vector2(2.34f, 2.7f);
        box.offset = catalog != null ? catalog.doorColliderOffset : Vector2.zero;
    }

    void AttachInteractableHighlight(Transform target, float distance)
    {
        if (target == null)
            return;

        InteractableHighlight highlight = target.GetComponent<InteractableHighlight>();
        if (highlight == null)
            highlight = target.gameObject.AddComponent<InteractableHighlight>();

        highlight.Configure(player != null ? player.transform : null, distance);
    }

    void ShowDoor()
    {
        if (doorRoot == null)
            return;

        if (!doorRoot.gameObject.activeSelf)
            doorRoot.gameObject.SetActive(true);
    }

    GameObject CreateWorkbench(Vector2 position)
    {
        GameObject root = new GameObject("TutorialWorkbench");
        root.transform.position = position;

        Sprite sprite = workbenchSprite;
#if UNITY_EDITOR
        if (sprite == null)
        {
            UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/room/jakup.png");
            foreach (UnityEngine.Object asset in assets)
                if (asset is Sprite s) { sprite = s; break; }
        }
#endif

        if (sprite != null)
        {
            GameObject go = new GameObject("WorkbenchSprite");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            go.transform.localScale = new Vector3(2.5f, 2.5f, 1f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 7;
        }
        else
        {
            AddSpriteBlock(root.transform, "Top",  new Vector2(0f,     0.20f), new Vector2(2.0f, 0.34f), new Color(0.41f, 0.24f, 0.14f, 1f), 8);
            AddSpriteBlock(root.transform, "Front",new Vector2(0f,    -0.20f), new Vector2(1.75f,0.56f), new Color(0.58f, 0.36f, 0.21f, 1f), 7);
            AddSpriteBlock(root.transform, "LegL", new Vector2(-0.72f,-0.76f), new Vector2(0.22f,0.74f), new Color(0.33f, 0.19f, 0.11f, 1f), 6);
            AddSpriteBlock(root.transform, "LegR", new Vector2( 0.72f,-0.76f), new Vector2(0.22f,0.74f), new Color(0.33f, 0.19f, 0.11f, 1f), 6);
        }

        GameObject labelObject = new GameObject("WorkbenchLabel");
        labelObject.transform.SetParent(root.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 1.15f, 0f);
        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = "작업대";
        label.font = UIThinDungFont.Get();
        label.fontSize = 2.4f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.08f, 0.06f, 0.04f, 0.82f);
        return root;
    }

    void EnsureWorkbenchShadow(Transform workbenchRoot)
    {
        if (workbenchRoot == null)
            return;
        Transform spriteChild = workbenchRoot.Find("WorkbenchSprite");
        if (spriteChild == null)
            return;

        // 이미 있으면 건드리지 않음 (씬에서 수동 조정 유지)
        if (spriteChild.Find("Character Direction Shadow") != null)
            return;

        SpriteRenderer ownerSr = spriteChild.GetComponent<SpriteRenderer>();
        if (ownerSr == null || ownerSr.sprite == null)
            return;

        GameObject shadowGo = new GameObject("Character Direction Shadow");
        shadowGo.transform.SetParent(spriteChild, false);
        SpriteRenderer shadowSr = shadowGo.AddComponent<SpriteRenderer>();
        shadowSr.sprite = ownerSr.sprite;
        shadowSr.color = new Color(0.03f, 0.022f, 0.018f, 0.3f);
        shadowSr.sortingLayerID = ownerSr.sortingLayerID;
        shadowSr.sortingOrder = ownerSr.sortingOrder - 1;
        shadowSr.sharedMaterial = ownerSr.sharedMaterial;
        shadowGo.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
        shadowGo.transform.localScale = new Vector3(1.08f, 0.42f, 1f);
        shadowGo.transform.localPosition = new Vector3(0.12f, -0.12f, 0.02f);
    }

    void AddSpriteBlock(Transform parent, string objectName, Vector2 localPosition, Vector2 scale, Color color, int sortingOrder)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
    }

    TutorialEnemy CreateEnemy(Vector2 position)
    {
        GameObject enemy = new GameObject("TutorialEnemy");
        enemy.transform.position = position;
        SpriteRenderer renderer = enemy.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 110;
        BoxCollider2D collider = enemy.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1.0f, 1.2f);
        TutorialEnemy tutorialEnemy = enemy.AddComponent<TutorialEnemy>();
        return tutorialEnemy;
    }

    void PrepareHudForTutorial()
    {
        mapButtonObject = FindHudChild("MapIconButton");
        if (mapButtonObject != null)
            mapButtonObject.SetActive(false);

        inventoryButtonObject = FindHudChild("InventoryIconButton");
        if (inventoryButtonObject != null)
            inventoryButtonObject.SetActive(false);

        menuButtonObject = FindHudChild("MenuIconButton");
        if (menuButtonObject != null)
            menuButtonObject.SetActive(false);

        if (runHud != null)
        {
            runHud.mapKeyAllowed       = () => step >= TutorialStep.Map;
            runHud.inventoryKeyAllowed = () => step >= TutorialStep.Inventory;
            runHud.menuKeyAllowed      = () => step >= TutorialStep.Menu;
        }
    }

    GameObject FindHudChild(string childName)
    {
        if (runHud == null)
            return null;

        Transform found = FindChildRecursive(runHud.transform, childName);
        return found != null ? found.gameObject : null;
    }

    static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
                return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    void UpdateArrow()
    {
        if (arrowRoot == null || player == null || workbench == null || !arrowRoot.gameObject.activeSelf)
            return;

        Vector2 playerPosition = player.transform.position;
        Vector2 targetPosition = workbench.position;
        Vector2 toTarget = targetPosition - playerPosition;
        if (toTarget.sqrMagnitude <= 0.001f)
            toTarget = Vector2.right;

        Vector2 direction = toTarget.normalized;
        float bob = Mathf.Sin(Time.unscaledTime * 3.2f) * 0.16f;
        arrowRoot.position = playerPosition + direction * 1.16f + Vector2.up * (0.66f + bob);
        arrowRoot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    void SetArrowVisible(bool visible)
    {
        if (arrowRoot != null)
            arrowRoot.gameObject.SetActive(visible);
    }

    void ShowOnly(CanvasGroup prompt)
    {
        HideAllPrompts();
        SetPromptVisible(prompt, true);
    }

    void HideAllPrompts()
    {
        SetPromptVisible(movePrompt, false);
        SetPromptVisible(interactPrompt, false);
        SetPromptVisible(mapPrompt, false);
        SetPromptVisible(mapScrollPrompt, false);
        SetPromptVisible(inventoryPrompt, false);
        SetPromptVisible(inventoryDetachPrompt, false);
        SetPromptVisible(inventoryReattachPrompt, false);
        SetPromptVisible(menuPrompt, false);
        SetPromptVisible(attackPrompt, false);
        SetPromptVisible(doorPrompt, false);
    }

    bool IsMapOpen()
    {
        GameObject mapOverlay = FindHudChild("MapOverlay");
        return mapOverlay != null && mapOverlay.activeSelf;
    }

    void SetPromptVisible(CanvasGroup group, bool visible)
    {
        if (group == null)
            return;

        group.gameObject.SetActive(visible);
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    void PulsePrompt(CanvasGroup group)
    {
        if (group == null || !group.gameObject.activeSelf)
            return;

        float pulse = Mathf.Sin(promptPulseTime * 4.4f) * 0.5f + 0.5f;
        group.alpha = Mathf.Lerp(0.42f, 1f, pulse);
    }

    void SetCanvasGroup(CanvasGroup group, bool visible, float alpha)
    {
        if (group == null)
            return;

        group.alpha = alpha;
        group.gameObject.SetActive(visible);
        group.blocksRaycasts = visible;
        group.interactable = visible;
    }

    IEnumerator AnimateRect(RectTransform rect, Vector2 from, Vector2 to, float duration, System.Func<float, float> easing)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            float t = Mathf.Clamp01(elapsed / safeDuration);
            rect.anchoredPosition = Vector2.LerpUnclamped(from, to, easing(t));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        rect.anchoredPosition = to;
    }

    static bool WasMovePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null
            && (keyboard.wKey.wasPressedThisFrame
                || keyboard.aKey.wasPressedThisFrame
                || keyboard.sKey.wasPressedThisFrame
                || keyboard.dKey.wasPressedThisFrame);
    }

    static bool WasAttackPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null
            && (keyboard.upArrowKey.wasPressedThisFrame
                || keyboard.downArrowKey.wasPressedThisFrame
                || keyboard.leftArrowKey.wasPressedThisFrame
                || keyboard.rightArrowKey.wasPressedThisFrame);
    }

    static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    static float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
            return;

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        System.Type inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputModuleType != null)
            eventSystemObject.AddComponent(inputModuleType);
        else
            eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    static Sprite SquareSprite()
    {
        if (squareSprite != null)
            return squareSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        squareSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return squareSprite;
    }

    static Sprite CircleSprite()
    {
        if (circleSprite != null)
            return circleSprite;

        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.48f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= radius ? Color.white : new Color(1f, 1f, 1f, 0f));
            }
        }

        texture.Apply();
        circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return circleSprite;
    }
}
