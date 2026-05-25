using UnityEngine;
using UnityEngine.InputSystem;

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

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // 디버그 키: 1=머리, 2=눈알, 3=몸, 4=팔, 5=다리
        if (kb.digit1Key.wasPressedThisFrame)
            State.head = !State.head;

        if (kb.digit2Key.wasPressedThisFrame)
        {
            if      ( State.eyeLeft &&  State.eyeRight) State.eyeLeft  = false;
            else if (!State.eyeLeft &&  State.eyeRight) State.eyeRight = false;
            else { State.eyeLeft = true; State.eyeRight = true; }
        }

        if (kb.digit3Key.wasPressedThisFrame)
            State.body = !State.body;

        if (kb.digit4Key.wasPressedThisFrame)
        {
            if      ( State.armLeft &&  State.armRight) State.armLeft  = false;
            else if (!State.armLeft &&  State.armRight) State.armRight = false;
            else { State.armLeft = true; State.armRight = true; }
        }

        if (kb.digit5Key.wasPressedThisFrame)
        {
            if      ( State.legLeft &&  State.legRight) State.legLeft  = false;
            else if (!State.legLeft &&  State.legRight) State.legRight = false;
            else { State.legLeft = true; State.legRight = true; }
        }
    }
}
