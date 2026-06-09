using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] int attackDamage = 1;
    [SerializeField] float attackRange = 1.5f;
    [SerializeField] float attackCooldown = 0.3f;
    [SerializeField] Vector2 attackSize = new Vector2(1.2f, 1.2f);
    [SerializeField] float flashDuration = 0.12f;
    [SerializeField] float attackFacingLockDuration = 0.25f;

    float cooldownTimer;
    float pendingKeyTimer;
    Key pendingAttackKey = Key.None;
    int pendingPressCount;
    GameObject flashObj;
    PlayerController playerController;

    [SerializeField, Min(0f)] float pressTimeout = 0.5f;
    [SerializeField, Min(1)] int requiredPressCount = 3;

    static readonly Key[] dirKeys = { Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow };
    static readonly Vector2[] dirVecs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

    void Awake()
    {
        playerController = GetComponent<PlayerController>();

        flashObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        flashObj.name = "AttackFlash";
        flashObj.transform.SetParent(transform);
        flashObj.transform.localScale = new Vector3(attackSize.x, attackSize.y, 1f);

        var mc = flashObj.GetComponent<MeshCollider>();
        if (mc != null) Destroy(mc);

        var rend = flashObj.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", new Color(1f, 0f, 0f, 1f));
        rend.material = mat;
        rend.sortingOrder = 10;

        flashObj.SetActive(false);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        cooldownTimer -= Time.deltaTime;
        pendingKeyTimer -= Time.deltaTime;
        if (pendingKeyTimer <= 0f)
        {
            pendingAttackKey = Key.None;
            pendingPressCount = 0;
        }

        if (cooldownTimer > 0f) return;

        BodyState bodyState = BodyConditionUtility.CurrentState();
        bool needsMultiplePress = bodyState != null && bodyState.armLeft != bodyState.armRight;

        for (int i = 0; i < dirKeys.Length; i++)
        {
            if (!kb[dirKeys[i]].wasPressedThisFrame)
                continue;

            playerController?.FaceDirection(dirVecs[i], attackFacingLockDuration);

            if (needsMultiplePress)
            {
                if (pendingAttackKey != dirKeys[i])
                {
                    pendingAttackKey = dirKeys[i];
                    pendingPressCount = 1;
                    pendingKeyTimer = pressTimeout;
                }
                else
                {
                    pendingPressCount++;
                    pendingKeyTimer = pressTimeout;
                    if (pendingPressCount >= requiredPressCount)
                    {
                        Attack(dirVecs[i]);
                        cooldownTimer = attackCooldown;
                        pendingAttackKey = Key.None;
                        pendingPressCount = 0;
                        pendingKeyTimer = 0f;
                        break;
                    }
                }

                break;
            }

            Attack(dirVecs[i]);
            cooldownTimer = attackCooldown;
            pendingAttackKey = Key.None;
            pendingPressCount = 0;
            pendingKeyTimer = 0f;
            break;
        }
    }

    void Attack(Vector2 dir)
    {
        Vector2 origin = (Vector2)transform.position + dir * attackRange;

        StartCoroutine(ShowFlash(dir));

        Collider2D[] hits = Physics2D.OverlapBoxAll(origin, attackSize, 0f);
        foreach (var hit in hits)
        {
            var enemy = hit.GetComponent<EnemyBase>();
            if (enemy != null)
                enemy.TakeDamage(attackDamage);
        }
    }

    IEnumerator ShowFlash(Vector2 dir)
    {
        flashObj.transform.localPosition = new Vector3(dir.x * attackRange, dir.y * attackRange, -0.01f);
        flashObj.SetActive(true);
        yield return new WaitForSeconds(flashDuration);
        flashObj.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        foreach (var dir in dirVecs)
            Gizmos.DrawWireCube((Vector2)transform.position + dir * attackRange, attackSize);
    }
}
