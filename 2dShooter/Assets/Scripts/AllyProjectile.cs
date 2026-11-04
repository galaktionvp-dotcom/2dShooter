using InspectorLocalization;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer))]
public sealed class AllyProjectile : MonoBehaviour
{
    [Header("Видимый снаряд")]
    [InspectorLabel("Скорость пули", "Скорость движения видимого снаряда в единицах Unity в секунду.")]
    [SerializeField, Min(1f)] private float speed = 11f;

    [InspectorLabel("Время жизни пули", "Через сколько секунд снаряд отключается, если ни во что не попал.")]
    [SerializeField, Min(0.1f)] private float maxLifetime = 0.9f;

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

        if (hitCollider != null)
            hitCollider.enabled = true;
    }

    public bool Launch(Transform owner, Vector2 startPosition, Vector2 direction, float damageAmount)
    {
        if (isTravelling || owner == null || direction.sqrMagnitude < 0.001f)
            return false;

        CacheComponents();
        if (body == null || hitCollider == null || spriteRenderer == null)
            return false;

        ownerRoot = owner;
        damage = Mathf.Max(0f, damageAmount);
        remainingLifetime = Mathf.Max(0.1f, maxLifetime);
        speed = Mathf.Max(1f, speed);
        direction.Normalize();

        transform.SetParent(null, true);
        transform.position = startPosition;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        isTravelling = true;
        gameObject.SetActive(true);
        spriteRenderer.enabled = true;
        hitCollider.enabled = true;
        body.simulated = true;
        body.linearVelocity = direction * speed;
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

        if (other.GetComponentInParent<AllyProjectile>() != null ||
            other.GetComponentInParent<Bullet>() != null)
        {
            return;
        }

        // Дружественные персонажи не блокируют союзный снаряд.
        if (other.GetComponentInParent<AllyHealth>() != null ||
            other.GetComponentInParent<PlayerHealth>() != null)
        {
            return;
        }

        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();
        if (enemy != null && !enemy.IsDead)
        {
            Rigidbody2D projectileBody = body;
            Vector2 hitDirection = projectileBody != null &&
                                   projectileBody.linearVelocity.sqrMagnitude > 0.001f
                ? projectileBody.linearVelocity.normalized
                : (Vector2)transform.right;

            enemy.TakeDamage(damage, transform.position, hitDirection);
        }

        // Стены и другие препятствия останавливают полёт.
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

        if (hitCollider != null)
            hitCollider.enabled = true;

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
