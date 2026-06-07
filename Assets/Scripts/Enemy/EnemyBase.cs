using UnityEngine;

public class EnemyBase : MonoBehaviour
{
    [SerializeField] protected int maxHp = 3;
    [SerializeField] Color bodyColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    protected int currentHp;

    protected virtual void Awake()
    {
        currentHp = maxHp;
        var spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
            return;
        }

        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor", bodyColor);
            rend.material = mat;
        }
    }

    public void TakeDamage(int damage)
    {
        currentHp -= damage;
        OnDamaged();
        if (currentHp <= 0)
            Die();
    }

    protected virtual void OnDamaged() { }

    protected virtual void Die()
    {
        EnemyManager.Instance?.OnEnemyDied(this);
        Destroy(gameObject);
    }
}
