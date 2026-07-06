using UnityEngine;

[DefaultExecutionOrder(-20)]
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("Movement")]
    [SerializeField, Min(0f)] float moveSpeed = 5f;
    [SerializeField, Range(0f, 1f)] float missingLegSpeedMultiplier = 0.5f;

    [Header("Attack")]
    [SerializeField, Min(0)] int attackDamage = 1;
    [SerializeField, Min(0f)] float attackCooldown = 0.3f;
    [SerializeField, Min(0f)] float attackDistance = 0.55f;
    [SerializeField, Min(0f)] float attackDistanceBonus = 0.2f;
    [SerializeField, Min(0f)] float verticalAttackDistanceBonus = 0.06f;
    [SerializeField] Vector2 attackSize = new Vector2(1f, 1f);
    [SerializeField, Range(0.05f, 0.5f)] float attackDuration = 0.12f;
    [SerializeField] Vector2 fistScale = new Vector2(1.65f, 1.65f);
    public float MoveSpeed => moveSpeed;
    public float MissingLegSpeedMultiplier => missingLegSpeedMultiplier;
    public int AttackDamage => attackDamage;
    public float AttackCooldown => attackCooldown;
    public float AttackDistance => attackDistance;
    public float AttackDistanceBonus => attackDistanceBonus;
    public float VerticalAttackDistanceBonus => verticalAttackDistanceBonus;
    public Vector2 AttackSize => attackSize;
    public float AttackDuration => attackDuration;
    public Vector2 FistScale => fistScale;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Instance.gameObject.scene == gameObject.scene)
            {
                Destroy(gameObject);
                return;
            }

            Instance = null;
        }

        Instance = this;
        ApplyToExistingPlayer();
    }

    void Start()
    {
        ApplyToExistingPlayer();
    }

    void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        attackDamage = Mathf.Max(0, attackDamage);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        attackDistance = Mathf.Max(0f, attackDistance);
        attackDistanceBonus = Mathf.Max(0f, attackDistanceBonus);
        verticalAttackDistanceBonus = Mathf.Max(0f, verticalAttackDistanceBonus);
        attackSize = new Vector2(Mathf.Max(0f, attackSize.x), Mathf.Max(0f, attackSize.y));
        fistScale = new Vector2(Mathf.Max(0f, fistScale.x), Mathf.Max(0f, fistScale.y));

        if (Application.isPlaying)
            ApplyToExistingPlayer();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ApplyToExistingPlayer()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            PlayerController controller = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (controller != null)
                player = controller.gameObject;
        }

        if (player != null)
            ApplyTo(player);
    }

    public void ApplyTo(GameObject player)
    {
        if (player == null)
            return;

        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null)
            controller.ApplyPlayerManagerSettings(moveSpeed, missingLegSpeedMultiplier);

        PlayerAttack attack = player.GetComponent<PlayerAttack>();
        if (attack != null)
        {
            attack.ApplyPlayerManagerSettings(
                attackDamage,
                attackCooldown,
                attackDistance,
                attackDistanceBonus,
                verticalAttackDistanceBonus,
                attackSize,
                attackDuration,
                fistScale);
        }

        if (player.GetComponent<PlayerDamageReceiver>() == null)
            player.AddComponent<PlayerDamageReceiver>();
    }

}
