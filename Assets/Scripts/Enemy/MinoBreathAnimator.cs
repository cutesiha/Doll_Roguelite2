using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class MinoBreathAnimator : MonoBehaviour
{
    [SerializeField] SpriteRenderer targetRenderer;
    [SerializeField] Sprite[] frames = new Sprite[0];
    [SerializeField] float[] frameDurations =
    {
        0.12f, 0.08f, 0.09f, 0.08f, 0.22f, 0.08f, 0.09f, 0.08f, 0.24f
    };
    [SerializeField, Min(0.01f)] float defaultFrameTime = 0.1f;
    [SerializeField] bool loop = true;
    [SerializeField] bool playOnStart = true;

    int frameIndex;
    float elapsedInFrame;
    bool playing;

    public bool HasFrames => frames != null && frames.Length > 0;

    void Awake()
    {
        EnsureRenderer();
        SetFrame(0);
    }

    void OnEnable()
    {
        if (playOnStart)
            Play(true);
    }

    void OnValidate()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();

        defaultFrameTime = Mathf.Max(0.01f, defaultFrameTime);
        if (frameDurations == null)
            return;

        for (int i = 0; i < frameDurations.Length; i++)
            frameDurations[i] = Mathf.Max(0.01f, frameDurations[i]);
    }

    void Update()
    {
        if (!playing || frames == null || frames.Length == 0)
            return;

        elapsedInFrame += Time.deltaTime;
        float duration = CurrentFrameDuration();
        while (elapsedInFrame >= duration && playing)
        {
            elapsedInFrame -= duration;
            AdvanceFrame();
            duration = CurrentFrameDuration();
        }
    }

    public void Configure(Sprite[] newFrames, float[] newFrameDurations, bool shouldLoop, bool shouldPlayOnStart, bool restart)
    {
        frames = newFrames ?? new Sprite[0];
        frameDurations = newFrameDurations ?? new float[0];
        loop = shouldLoop;
        playOnStart = shouldPlayOnStart;

        if (restart)
            Play(true);
        else
            SetFrame(Mathf.Clamp(frameIndex, 0, Mathf.Max(0, frames.Length - 1)));
    }

    public void Play(bool restart = false)
    {
        if (frames == null || frames.Length == 0)
        {
            playing = false;
            return;
        }

        playing = true;
        if (restart)
        {
            elapsedInFrame = 0f;
            SetFrame(0);
        }
        else
        {
            SetFrame(frameIndex);
        }
    }

    public void Stop()
    {
        playing = false;
        elapsedInFrame = 0f;
    }

    public void SetFrame(int index)
    {
        EnsureRenderer();
        if (targetRenderer == null || frames == null || frames.Length == 0)
            return;

        frameIndex = Mathf.Clamp(index, 0, frames.Length - 1);
        if (frames[frameIndex] != null)
            targetRenderer.sprite = frames[frameIndex];
    }

    float CurrentFrameDuration()
    {
        if (frameDurations != null
            && frameDurations.Length == frames.Length
            && frameIndex >= 0
            && frameIndex < frameDurations.Length
            && frameDurations[frameIndex] > 0f)
        {
            return frameDurations[frameIndex];
        }

        return Mathf.Max(0.01f, defaultFrameTime);
    }

    void AdvanceFrame()
    {
        int nextFrame = frameIndex + 1;
        if (nextFrame >= frames.Length)
        {
            if (!loop)
            {
                playing = false;
                SetFrame(frames.Length - 1);
                return;
            }

            nextFrame = 0;
        }

        SetFrame(nextFrame);
    }

    void EnsureRenderer()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();
    }
}
