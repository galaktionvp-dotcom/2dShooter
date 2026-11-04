using InspectorLocalization;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer))]
public sealed class EnemyProjectile : MonoBehaviour
{
    [Header("Видимый снаряд")]
    [InspectorLabel("Время жизни", "Через сколько секунд снаряд отключается, если ни во что не попал.")]
    [SerializeField, Min(0.1f)] private float maxLifetime = 1.25f;

    private Rigidbody2D body;
    private CircleCollider2D hitCollider;
    private SpriteRenderer spriteRenderer;
    private Transform ownerRoot;
    private float damage;
    private float remainingLifetime;
    private bool isTravelling;

    public bool IsTravelling => isTravelling;

    private void Awake()
    {
        CacheComponents();
    }

    public void PreparePool(Transform owner)
    {
        ownerRoot = owner;
        CacheComponents();

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }
    }

    public bool Launch(
        Transform owner,
        Vector2 startPosition,
        Vector2 direction,
        float damageAmount,
        float projectileSpeed)
    {
        if (isTravelling || owner == null || direction.sqrMagnitude < 0.001f)
            return false;

        CacheComponents();
        if (body == null || hitCollider == null || spriteRenderer == null)
            return false;

        ownerRoot = owner;
        damage = Mathf.Max(0f, damageAmount);
        remainingLifetime = Mathf.Max(0.1f, maxLifetime);
        direction.Normalize();

        transform.SetParent(null, true);
        transform.position = startPosition;
        transform.rotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        isTravelling = true;
        gameObject.SetActive(true);

        CacheComponents();
        spriteRenderer.enabled = true;
        hitCollider.enabled = true;
        body.simulated = true;
        body.linearVelocity = direction * Mathf.Max(1f, projectileSpeed);
        return true;
    }

    private void Update()
    {
        if (!isTravelling)
            return;

        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f)
            ReturnToPool();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isTravelling || other == null)
            return;

        if (ownerRoot != null &&
            (other.transform == ownerRoot || other.transform.IsChildOf(ownerRoot)))
        {
            return;
        }

        if (other.GetComponentInParent<EnemyProjectile>() != null ||
            other.GetComponentInParent<AllyProjectile>() != null ||
            other.GetComponentInParent<Bullet>() != null)
        {
            return;
        }

        // Не допускаем попадания по своим бойцам.
        if (other.GetComponentInParent<EnemyHealth>() != null)
            return;

        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player != null && !player.IsDead)
        {
            Vector2 hitDirection = body != null && body.linearVelocity.sqrMagnitude > 0.001f
                ? body.linearVelocity.normalized
                : (Vector2)transform.right;

            player.TakeDamage(damage, transform.position, hitDirection);
            ReturnToPool();
            return;
        }

        AllyHealth ally = other.GetComponentInParent<AllyHealth>();
        if (ally != null && !ally.IsDead)
        {
            Vector2 hitDirection = body != null && body.linearVelocity.sqrMagnitude > 0.001f
                ? body.linearVelocity.normalized
                : (Vector2)transform.right;

            ally.TakeDamage(damage, transform.position, hitDirection);
            ReturnToPool();
            return;
        }

        // Стены и другие препятствия останавливают снаряд.
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (!isTravelling)
            return;

        isTravelling = false;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }

        if (ownerRoot != null)
        {
            transform.SetParent(ownerRoot, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        gameObject.SetActive(false);
    }

    private void CacheComponents()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();

        if (hitCollider == null)
            hitCollider = GetComponent<CircleCollider2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnDisable()
    {
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }

        isTravelling = false;
    }
}
