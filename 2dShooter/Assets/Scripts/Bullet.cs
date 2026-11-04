using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Bullet : MonoBehaviour
{
    private float lifetime;
    private float damage;
    private Transform ownerRoot;
    private bool hasHit;

    public void Initialize(float lifetimeSeconds, float damageAmount, Transform owner)
    {
        lifetime = Mathf.Max(0.01f, lifetimeSeconds);
        damage = Mathf.Max(0f, damageAmount);
        ownerRoot = owner != null ? owner.root : null;
    }

    private void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit || other == null)
            return;

        // Не даём снаряду попадать в стрелка или его дочерние коллайдеры.
        if (ownerRoot != null &&
            (other.transform == ownerRoot || other.transform.IsChildOf(ownerRoot)))
        {
            return;
        }

        // Снаряды не должны повреждать друг друга.
        if (other.GetComponentInParent<Bullet>() != null)
            return;

        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();
        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        AllyHealth ally = other.GetComponentInParent<AllyHealth>();

        bool canDamageEnemy = enemy != null && !enemy.IsDead;
        bool canDamagePlayer = player != null && !player.IsDead;
        bool canDamageAlly = ally != null && !ally.IsDead;

        if (!canDamageEnemy && !canDamagePlayer && !canDamageAlly)
        {
            // Попадание в стену или препятствие поглощает снаряд.
            hasHit = true;
            Destroy(gameObject);
            return;
        }

        hasHit = true;

        Rigidbody2D bulletBody = GetComponent<Rigidbody2D>();
        Vector2 hitDirection = bulletBody != null && bulletBody.linearVelocity.sqrMagnitude > 0.001f
            ? bulletBody.linearVelocity.normalized
            : (Vector2)transform.right;

        if (canDamageEnemy)
            enemy.TakeDamage(damage, transform.position, hitDirection);
        else if (canDamageAlly)
            ally.TakeDamage(damage, transform.position, hitDirection);
        else if (canDamagePlayer)
            player.TakeDamage(damage, transform.position, hitDirection);

        Destroy(gameObject);
    }
}
