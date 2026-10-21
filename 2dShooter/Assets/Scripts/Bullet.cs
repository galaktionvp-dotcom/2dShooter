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

        if ((enemy == null || enemy.IsDead) && (player == null || player.IsDead))
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

        if (enemy != null && !enemy.IsDead)
        {
            enemy.TakeDamage(damage, transform.position, hitDirection);
        }
        else if (player != null && !player.IsDead)
        {
            player.TakeDamage(damage, transform.position, hitDirection);
        }

        Destroy(gameObject);
    }
}
