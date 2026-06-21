using TMPro;
using UnityEngine;

// A collectible word fragment dropped by the Book boss's arms. It sparkles on the floor
// and is gathered when the player walks over it.
public class LetterPickup : MonoBehaviour
{
    string word;
    System.Action<string> onCollected;
    TextMeshPro label;
    float spawnTime;
    bool collected;

    public static LetterPickup Spawn(string word, Vector2 position, System.Action<string> onCollected)
    {
        GameObject go = new GameObject("LetterPickup_" + word);
        go.transform.position = new Vector3(position.x, position.y, -0.5f);
        LetterPickup pickup = go.AddComponent<LetterPickup>();
        pickup.Configure(word, onCollected);
        return pickup;
    }

    void Configure(string newWord, System.Action<string> callback)
    {
        word = newWord;
        onCollected = callback;
        spawnTime = Time.time;

        label = gameObject.AddComponent<TextMeshPro>();
        label.text = word;
        label.font = UIThinDungFont.Get();
        label.fontSize = 3.4f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.92f, 0.4f, 1f);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.sortingOrder = 75;
        label.rectTransform.sizeDelta = new Vector2(6f, 1.4f);

        CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.7f;
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
}
