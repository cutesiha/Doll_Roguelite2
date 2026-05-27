using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] float moveSpeed = 5f;
    [SerializeField, Range(0f, 1f)] float missingLegSpeedMultiplier = 0.5f;
    [SerializeField] Color bodyColor = new Color(0.3f, 0.6f, 1f, 1f);

    Rigidbody2D rb;
    Vector2 moveInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor", bodyColor);
            rend.material = mat;
        }
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        float h = 0f, v = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;
        moveInput = new Vector2(h, v).normalized;
    }

    void FixedUpdate()
    {
        float speed = moveSpeed;
        var state = BodyManager.Instance != null ? BodyManager.Instance.State : null;
        if (state != null && (!state.legLeft || !state.legRight))
            speed *= missingLegSpeedMultiplier;

        rb.MovePosition(rb.position + moveInput * speed * Time.fixedDeltaTime);
    }
}
