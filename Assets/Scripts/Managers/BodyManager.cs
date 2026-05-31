using UnityEngine;

[DefaultExecutionOrder(-1)]
public class BodyManager : MonoBehaviour
{
    public static BodyManager Instance { get; private set; }

    public BodyState State { get; private set; } = new BodyState();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
