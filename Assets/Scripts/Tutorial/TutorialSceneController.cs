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
        Inventory,
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

    [Header("Tuning")]
    [SerializeField] float workbenchPromptDistance = 2.0f;
    [SerializeField] float doorPromptDistance = 1.65f;
    [SerializeField] Vector2 enemyEntranceStart = new Vector2(-12f, -0.6f);
    [SerializeField] Vector2 enemyEntranceEnd = new Vector2(-3.1f, -0.6f);
    [SerializeField] Vector2 doorPosition = new Vector2(0f, 1.1f);
    [SerializeField] Vector2 cameraBoundsPadding = new Vector2(0.55f, 0.78f);

    Canvas tutorialCanvas;
    CanvasGroup movePrompt;
    CanvasGroup interactPrompt;
    CanvasGroup inventoryPrompt;
    CanvasGroup attackPrompt;
    CanvasGroup doorPrompt;
    CanvasGroup paperGroup;
    CanvasGroup pauseOverlay;
    Image fadeImage;
    RectTransform paperRect;
    Transform arrowRoot;
    Transform doorRoot;
    TutorialEnemy activeEnemy;
    InventoryUI inventoryUI;
    GameObject inventoryButtonObject;
    RunHudUI runHud;
    TutorialStep step;
    bool paperReadyForClick;
    bool inventoryOpened;
    bool attackPromptDismissed;
    bool doorPromptVisible;
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
            workbench = CreateWorkbench(new Vector2(5.4f, -0.9f)).transform;

        EnsureCamera();
        EnsureEventSystem();
        EnsureTutorialCanvas();
        EnsureRunHud();
        BuildUi();
        BuildArrow();
        BuildDoor();
        PrepareHudForTutorial();
    }

    void Start()
    {
        Time.timeScale = 1f;

        if (player != null)
            player.transform.position = new Vector3(-5.6f, -1.2f, 0f);

        ShowOnly(movePrompt);
        step = TutorialStep.Move;
    }

    void Update()
    {
        promptPulseTime += Time.unscaledDeltaTime;
        PulsePrompt(movePrompt);
        PulsePrompt(interactPrompt);
        PulsePrompt(inventoryPrompt);
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
            case TutorialStep.Inventory:
                UpdateInventoryStep();
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
        if (!paperReadyForClick || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        paperReadyForClick = false;
        StartCoroutine(ClosePaperRoutine());
    }

    void UpdateInventoryStep()
    {
        if (inventoryUI == null)
            inventoryUI = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);

        if (inventoryUI != null && inventoryUI.IsOpen)
        {
            inventoryOpened = true;
            SetPromptVisible(inventoryPrompt, false);
            return;
        }

        if (inventoryOpened)
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
        Vector2 hidden = new Vector2(0f, -720f);
        Vector2 overshoot = new Vector2(0f, 42f);
        Vector2 shown = Vector2.zero;
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
        Vector2 hidden = new Vector2(0f, -760f);
        yield return AnimateRect(paperRect, shown, bump, 0.10f, EaseOutCubic);
        yield return AnimateRect(paperRect, bump, hidden, 0.34f, EaseInCubic);
        SetCanvasGroup(paperGroup, false, 0f);
        ShowInventoryPrompt();
    }

    void ShowInventoryPrompt()
    {
        if (inventoryButtonObject != null)
            inventoryButtonObject.SetActive(true);

        inventoryOpened = false;
        ShowOnly(inventoryPrompt);
        step = TutorialStep.Inventory;
    }

    IEnumerator EnemyIntroRoutine()
    {
        step = TutorialStep.EnemyIntro;
        SetPromptVisible(inventoryPrompt, false);
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
        step = TutorialStep.Done;
        SetPromptVisible(doorPrompt, false);
        SetArrowVisible(false);
        Time.timeScale = 1f;

        if (runHud != null)
            Destroy(runHud.gameObject);

        RunHudUI.ShowControlHintsOnNextRoom = true;

        fadeImage.transform.SetAsLastSibling();
        float elapsed = 0f;
        const float duration = 0.55f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetImageAlpha(fadeImage, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        SceneManager.LoadScene(roomSceneName);
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
        GameObject canvasObject = new GameObject("TutorialCanvas");
        tutorialCanvas = canvasObject.AddComponent<Canvas>();
        tutorialCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        tutorialCanvas.sortingOrder = 120;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
    }

    void EnsureRunHud()
    {
        runHud = FindFirstObjectByType<RunHudUI>(FindObjectsInactive.Include);
        if (runHud == null)
            return;

        inventoryUI = runHud.GetComponentInChildren<InventoryUI>(true);
    }

    void BuildUi()
    {
        pauseOverlay = CreateFullScreenOverlay("TutorialPauseOverlay", new Color(0f, 0f, 0f, 0.36f), false);
        fadeImage = CreateFullScreenImage("TutorialFade", Color.black);
        SetImageAlpha(fadeImage, 0f);

        movePrompt = CreatePrompt("MovePrompt", "[WASD]로 움직이기", new Vector2(-560f, 250f), new Vector2(430f, 142f));
        interactPrompt = CreatePrompt("InteractPrompt", "[E] 키를 눌러 상호작용", new Vector2(530f, -305f), new Vector2(520f, 132f));
        inventoryPrompt = CreatePrompt("InventoryPrompt", "[Tab] 키를 눌러 인벤토리 열기", new Vector2(0f, 338f), new Vector2(650f, 132f));
        attackPrompt = CreatePrompt("AttackPrompt", "방향키로 공격", new Vector2(530f, 260f), new Vector2(420f, 132f));
        doorPrompt = CreatePrompt("DoorPrompt", "[Enter]를 눌러 들어가기", new Vector2(0f, -335f), new Vector2(560f, 132f));
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

        AddDashedBorder(rect, size, new Color(0.04f, 0.035f, 0.03f, 0.96f));

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
        GameObject paper = new GameObject("S6Paper");
        paper.transform.SetParent(tutorialCanvas.transform, false);
        paperRect = paper.AddComponent<RectTransform>();
        paperRect.anchorMin = paperRect.anchorMax = new Vector2(0.5f, 0.5f);
        paperRect.pivot = new Vector2(0.5f, 0.5f);
        paperRect.sizeDelta = new Vector2(860f, 520f);
        paperRect.anchoredPosition = new Vector2(0f, -760f);
        paperGroup = paper.AddComponent<CanvasGroup>();

        Image paperImage = paper.AddComponent<Image>();
        paperImage.color = new Color(0.98f, 0.96f, 0.90f, 1f);

        AddDashedBorder(paperRect, paperRect.sizeDelta, new Color(0.04f, 0.035f, 0.03f, 0.64f));

        TextMeshProUGUI number = AddPaperText(paper.transform, "#S6", new Vector2(-370f, 210f), new Vector2(180f, 70f), 48f, TextAlignmentOptions.Center);
        number.color = Color.black;

        Image cloud = AddPaperImage(paper.transform, "PaperCloud", new Vector2(0f, 34f), new Vector2(640f, 280f), new Color(0.72f, 0.72f, 0.70f, 0.34f));
        cloud.sprite = CircleSprite();
        cloud.type = Image.Type.Sliced;

        AddPaperText(paper.transform, "언제든 준비가 되면\n떠나자.\n단추가 너의 여정을\n도와줄거야.", new Vector2(40f, 24f), new Vector2(650f, 300f), 43f, TextAlignmentOptions.Center);
        AddPaperText(paper.transform, "화면 아무 곳이나 클릭", new Vector2(240f, -202f), new Vector2(360f, 54f), 28f, TextAlignmentOptions.Center);
        SetCanvasGroup(paperGroup, false, 0f);
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
        GameObject root = new GameObject("WorkbenchArrow");
        arrowRoot = root.transform;

        AddArrowPart(root.transform, "Shaft", new Vector2(-0.10f, 0f), new Vector2(1.15f, 0.18f), 0f);
        AddArrowPart(root.transform, "HeadA", new Vector2(0.54f, 0.17f), new Vector2(0.48f, 0.18f), 38f);
        AddArrowPart(root.transform, "HeadB", new Vector2(0.54f, -0.17f), new Vector2(0.48f, 0.18f), -38f);
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
        renderer.color = new Color(0.03f, 0.025f, 0.02f, 0.58f);
        renderer.sortingOrder = 220;
    }

    void BuildDoor()
    {
        GameObject door = new GameObject("TutorialExitDoor");
        doorRoot = door.transform;
        doorRoot.position = doorPosition;
        AddSpriteBlock(doorRoot, "DoorPanel", Vector2.zero, new Vector2(1.15f, 1.75f), new Color(0.18f, 0.12f, 0.09f, 1f), 20);
        AddSpriteBlock(doorRoot, "DoorInner", new Vector2(0f, -0.04f), new Vector2(0.82f, 1.38f), new Color(0.48f, 0.30f, 0.18f, 1f), 21);
        AddSpriteBlock(doorRoot, "DoorKnob", new Vector2(0.28f, -0.05f), new Vector2(0.11f, 0.11f), new Color(0.94f, 0.72f, 0.28f, 1f), 22);
        door.SetActive(false);
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
        AddSpriteBlock(root.transform, "Top", new Vector2(0f, 0.20f), new Vector2(2.0f, 0.34f), new Color(0.41f, 0.24f, 0.14f, 1f), 8);
        AddSpriteBlock(root.transform, "Front", new Vector2(0f, -0.20f), new Vector2(1.75f, 0.56f), new Color(0.58f, 0.36f, 0.21f, 1f), 7);
        AddSpriteBlock(root.transform, "LegL", new Vector2(-0.72f, -0.76f), new Vector2(0.22f, 0.74f), new Color(0.33f, 0.19f, 0.11f, 1f), 6);
        AddSpriteBlock(root.transform, "LegR", new Vector2(0.72f, -0.76f), new Vector2(0.22f, 0.74f), new Color(0.33f, 0.19f, 0.11f, 1f), 6);

        GameObject labelObject = new GameObject("WorkbenchLabel");
        labelObject.transform.SetParent(root.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = "작업대";
        label.font = UIThinDungFont.Get();
        label.fontSize = 2.4f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.08f, 0.06f, 0.04f, 0.82f);
        return root;
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
        inventoryButtonObject = FindHudChild("InventoryIconButton");
        if (inventoryButtonObject != null)
            inventoryButtonObject.SetActive(false);

        GameObject mapButton = FindHudChild("MapIconButton");
        if (mapButton != null)
            mapButton.SetActive(false);

        GameObject menuButton = FindHudChild("MenuIconButton");
        if (menuButton != null)
            menuButton.SetActive(false);
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
        arrowRoot.position = playerPosition + direction * 1.05f + Vector2.up * (0.72f + bob);
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
        SetPromptVisible(inventoryPrompt, false);
        SetPromptVisible(attackPrompt, false);
        SetPromptVisible(doorPrompt, false);
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

        float t = Mathf.Sin(promptPulseTime * 2.2f) * 0.5f + 0.5f;
        group.alpha = Mathf.Lerp(0.88f, 1f, t);
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
