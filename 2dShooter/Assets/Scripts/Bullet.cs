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

        // Не даём пуле попадать в самого стрелка или его дочерние коллайдеры.
        if (ownerRoot != null &&
            (other.transform == ownerRoot || other.transform.IsChildOf(ownerRoot)))
        {
            return;
        }

        // Пули не должны уничтожать друг друга.
        if (other.GetComponentInParent<Bullet>() != null)
            return;

        hasHit = true;

        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();
        if (enemy != null && !enemy.IsDead)
        {
            Rigidbody2D bulletBody = GetComponent<Rigidbody2D>();
            Vector2 hitDirection = bulletBody != null && bulletBody.linearVelocity.sqrMagnitude > 0.001f
                ? bulletBody.linearVelocity.normalized
                : (Vector2)transform.right;

            enemy.TakeDamage(damage, transform.position, hitDirection);
        }

        // Попадание во врага, стену или препятствие поглощает пулю.
        Destroy(gameObject);
    }
}
