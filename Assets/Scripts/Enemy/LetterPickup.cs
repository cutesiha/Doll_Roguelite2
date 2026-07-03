using System.Collections;
using TMPro;
using UnityEngine;

// A collectible word fragment dropped by the Book boss's arms. It sparkles on the floor
// and is gathered when the player walks over it.
public class LetterPickup : MonoBehaviour
{
    static Sprite roundedPanelSprite;

    string word;
    System.Action<string> onCollected;
    TextMeshPro label;
    SpriteRenderer panelRenderer;
    SpriteRenderer glowRenderer;
    ParticleSystem sparkleParticles;
    BoxCollider2D pickupCollider;
    Transform player;
    float spawnTime;
    bool collected;

    public static LetterPickup Spawn(string word, Vector2 position, System.Action<string> onCollected)
    {
        GameObject go = new GameObject("LetterPickup_" + word);
        go.transform.position = new Vector3(position.x, position.y + 0.55f, -0.5f);
        LetterPickup pickup = go.AddComponent<LetterPickup>();
        pickup.Configure(word, position, onCollected);
        return pickup;
    }

    void Configure(string newWord, Vector2 landingPosition, System.Action<string> callback)
    {
        word = newWord;
        onCollected = callback;
        spawnTime = Time.time;

        GameObject glow = new GameObject("PickupGlow");
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = new Vector3(0f, 0f, 0.03f);
        glowRenderer = glow.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = BossVisuals.CircleSprite();
        glowRenderer.color = new Color(1f, 0.64f, 0.38f, 0.0f);
        glowRenderer.sortingOrder = 72;

        GameObject panel = new GameObject("RoundedTextPanel");
        panel.transform.SetParent(transform, false);
        panel.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        panelRenderer = panel.AddComponent<SpriteRenderer>();
        panelRenderer.sprite = RoundedPanelSprite();
        panelRenderer.color = new Color(0.18f, 0.09f, 0.055f, 0.86f);
        panelRenderer.sortingOrder = 74;

        GameObject textObject = new GameObject("WordText");
        textObject.transform.SetParent(transform, false);
        textObject.transform.localPosition = new Vector3(0f, -0.02f, -0.02f);
        label = textObject.AddComponent<TextMeshPro>();
        label.text = word;
        label.font = UIThinDungFont.Get();
        label.fontSize = 4.8f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.9f, 0.34f, 1f);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.sortingOrder = 76;
        label.rectTransform.sizeDelta = new Vector2(6f, 2f);
        FitVisualsToText();

        pickupCollider = gameObject.AddComponent<BoxCollider2D>();
        pickupCollider.isTrigger = true;
        pickupCollider.size = PanelSizeForText() + new Vector2(0.35f, 0.25f);

        CreateSparkles();
        StartCoroutine(DropBounceRoutine(landingPosition));
    }

    void FitVisualsToText()
    {
        Vector2 panelSize = PanelSizeForText();

        if (panelRenderer != null && panelRenderer.sprite != null)
        {
            Vector2 spriteSize = panelRenderer.sprite.bounds.size;
            panelRenderer.transform.localScale = new Vector3(
                panelSize.x / Mathf.Max(0.01f, spriteSize.x),
                panelSize.y / Mathf.Max(0.01f, spriteSize.y),
                1f);
        }

        if (glowRenderer != null)
            glowRenderer.transform.localScale = new Vector3(panelSize.x * 1.18f, panelSize.y * 1.45f, 1f);

        if (label != null)
            label.rectTransform.sizeDelta = new Vector2(panelSize.x + 0.2f, panelSize.y + 0.25f);
    }

    Vector2 PanelSizeForText()
    {
        if (label == null)
            return new Vector2(2.1f, 1.25f);

        label.ForceMeshUpdate();
        Vector2 textSize = label.textBounds.size;
        float width = Mathf.Clamp(textSize.x + 0.85f, 1.8f, 5.2f);
        float height = Mathf.Clamp(textSize.y + 0.45f, 1.1f, 1.55f);
        return new Vector2(width, height);
    }

    void Update()
    {
        if (label == null)
            return;

        float t = Time.time * 6f;
        float sparkle = 0.5f + 0.5f * Mathf.Sin(t);
        label.color = new Color(1f, Mathf.Lerp(0.78f, 1f, sparkle), Mathf.Lerp(0.2f, 0.6f, sparkle), 1f);
        float scale = 1f + 0.12f * Mathf.Sin(t * 0.8f);
        transform.localScale = new Vector3(scale, scale, 1f);

        UpdateProximityGlow();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryCollect(other);
    }

    void TryCollect(Collider2D other)
    {
        if (collected || other == null || !other.CompareTag("Player"))
            return;

        // brief grace so a pickup that spawns under the player isn't grabbed instantly
        if (Time.time - spawnTime < 0.25f)
            return;

        collected = true;
        onCollected?.Invoke(word);
        SoundManager.PlayClick();
        Destroy(gameObject);
    }

    IEnumerator DropBounceRoutine(Vector2 landingPosition)
    {
        Vector3 start = transform.position;
        Vector3 peak = new Vector3(landingPosition.x + Random.Range(-0.25f, 0.25f), landingPosition.y + 2.25f, -0.5f);
        Vector3 end = new Vector3(landingPosition.x, landingPosition.y, -0.5f);

        float upDuration = 0.22f;
        float downDuration = 0.42f;
        float elapsed = 0f;
        while (elapsed < upDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / upDuration);
            transform.position = Vector3.Lerp(start, peak, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < downDuration)
        {
            float t = elapsed / downDuration;
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.position = Vector3.Lerp(peak, end, eased);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = end;
    }

    void UpdateProximityGlow()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;
        }

        float closeness = 0f;
        if (player != null)
        {
            float distance = Vector2.Distance(transform.position, player.position);
            closeness = Mathf.InverseLerp(3.1f, 0.6f, distance);
        }

        if (glowRenderer != null)
            glowRenderer.color = new Color(1f, 0.68f, 0.44f, Mathf.Lerp(0.12f, 0.54f, closeness));

        if (panelRenderer != null)
            panelRenderer.color = Color.Lerp(
                new Color(0.18f, 0.09f, 0.055f, 0.86f),
                new Color(0.48f, 0.22f, 0.10f, 0.96f),
                closeness);

        if (sparkleParticles != null)
        {
            ParticleSystem.EmissionModule emission = sparkleParticles.emission;
            emission.rateOverTime = Mathf.Lerp(8f, 34f, closeness);
        }
    }

    void CreateSparkles()
    {
        GameObject particles = new GameObject("PickupSparkles");
        particles.transform.SetParent(transform, false);
        particles.transform.localPosition = new Vector3(0f, 0f, -0.03f);
        sparkleParticles = particles.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = sparkleParticles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.38f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.24f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.66f, 0.42f, 0.78f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 58;

        ParticleSystem.EmissionModule emission = sparkleParticles.emission;
        emission.rateOverTime = 5f;

        ParticleSystem.ShapeModule shape = sparkleParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.25f;

        ParticleSystemRenderer renderer = sparkleParticles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 77;
        renderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    static Sprite RoundedPanelSprite()
    {
        if (roundedPanelSprite != null)
            return roundedPanelSprite;

        const int width = 128;
        const int height = 48;
        const float radius = 14f;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float cx = Mathf.Clamp(x, radius, width - radius);
                float cy = Mathf.Clamp(y, radius, height - radius);
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                float alpha = Mathf.Clamp01(radius + 1f - distance);
                texture.SetPixel(x, y, alpha > 0f ? new Color(1f, 1f, 1f, alpha) : clear);
            }
        }

        texture.Apply();
        roundedPanelSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), height);
        return roundedPanelSprite;
    }
}
