using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public class CameraShake : MonoBehaviour
{
    [SerializeField, Range(0.02f, 0.5f)] float defaultDuration = 0.12f;
    [SerializeField, Min(0f)] float defaultMagnitude = 0.10f;

    float remainingDuration;
    float totalDuration;
    float magnitude;
    Vector3 lastOffset;
    bool horizontalOnly;
    float oscillations = 5f;

    public static void Shake(float duration, float magnitude)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        CameraShake shaker = camera.GetComponent<CameraShake>();
        if (shaker == null)
            shaker = camera.gameObject.AddComponent<CameraShake>();

        shaker.Play(duration, magnitude);
    }

    public static void ShakeHorizontal(float duration, float magnitude, float oscillations = 5f)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        CameraShake shaker = camera.GetComponent<CameraShake>();
        if (shaker == null)
            shaker = camera.gameObject.AddComponent<CameraShake>();

        shaker.Play(duration, magnitude, true, oscillations);
    }

    public void PlayDefault()
    {
        Play(defaultDuration, defaultMagnitude);
    }

    public void Play(float duration, float newMagnitude)
    {
        Play(duration, newMagnitude, false, 5f);
    }

    void Play(float duration, float newMagnitude, bool useHorizontalOnly, float newOscillations)
    {
        totalDuration = Mathf.Max(0.01f, duration);
        remainingDuration = Mathf.Max(remainingDuration, totalDuration);
        magnitude = Mathf.Max(magnitude, newMagnitude);
        horizontalOnly = useHorizontalOnly;
        oscillations = Mathf.Max(0.5f, newOscillations);
    }

    void Update()
    {
        if (lastOffset == Vector3.zero)
            return;

        transform.position -= lastOffset;
        lastOffset = Vector3.zero;
    }

    void LateUpdate()
    {
        if (remainingDuration <= 0f || magnitude <= 0f)
            return;

        remainingDuration -= Time.deltaTime;
        float t = totalDuration > 0f ? Mathf.Clamp01(remainingDuration / totalDuration) : 0f;
        float strength = magnitude * t;
        if (horizontalOnly)
        {
            float progress = 1f - t;
            float wave = Mathf.Sin(progress * Mathf.PI * 2f * oscillations);
            lastOffset = Vector3.right * wave * strength;
        }
        else
        {
            lastOffset = (Vector3)(Random.insideUnitCircle * strength);
        }
        transform.position += lastOffset;

        if (remainingDuration <= 0f)
            magnitude = 0f;
    }

    void OnDisable()
    {
        if (lastOffset != Vector3.zero)
        {
            transform.position -= lastOffset;
            lastOffset = Vector3.zero;
        }

        remainingDuration = 0f;
        magnitude = 0f;
        horizontalOnly = false;
    }
}
