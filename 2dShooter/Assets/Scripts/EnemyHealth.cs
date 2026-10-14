using System.Collections;
using InspectorLocalization;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyHealth : MonoBehaviour
{
    [Header("Здоровье")]
    [InspectorLabel("Максимальное здоровье", "Общее количество здоровья врага в начале игры.")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    [Header("Реакция на попадание")]
    [InspectorLabel("Сила отбрасывания", "Насколько сильно враг отталкивается от точки попадания. Работает с динамическим Rigidbody2D.")]
    [SerializeField, Min(0f)] private float knockbackForce = 3f;

    [InspectorLabel("Длительность вспышки", "Сколько секунд враг подсвечивается при получении урона.")]
    [SerializeField, Min(0f)] private float hitFlashDuration = 0.08f;

    [InspectorLabel("Цвет попадания", "Цвет спрайта на короткое время после попадания.")]
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.25f, 0.25f, 1f);

    private float currentHealth;
    private bool isDead;
    private Rigidbody2D body;
    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private Coroutine hitFlashRoutine;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        knockbackForce = Mathf.Max(0f, knockbackForce);
        hitFlashDuration = Mathf.Max(0f, hitFlashDuration);

        currentHealth = maxHealth;
        body = GetComponent<Rigidbody2D>();
        if (body == null)
            body = GetComponentInParent<Rigidbody2D>();

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
            originalColors[i] = spriteRenderers[i] != null ? spriteRenderers[i].color : Color.white;
    }

    public void TakeDamage(float damage, Vector2 hitPoint, Vector2 hitDirection)
    {
        if (isDead || damage <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        PlayHitFeedback();
        ApplyKnockback(hitPoint, hitDirection);

        if (currentHealth <= 0f)
            Die();
    }

    private void PlayHitFeedback()
    {
        if (hitFlashDuration <= 0f || spriteRenderers.Length == 0)
            return;

        if (hitFlashRoutine != null)
            StopCoroutine(hitFlashRoutine);

        hitFlashRoutine = StartCoroutine(HitFlash());
    }

    private IEnumerator HitFlash()
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = hitFlashColor;
        }

        yield return new WaitForSeconds(hitFlashDuration);

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = originalColors[i];
        }

        hitFlashRoutine = null;
    }

    private void ApplyKnockback(Vector2 hitPoint, Vector2 hitDirection)
    {
        if (body == null || body.bodyType != RigidbodyType2D.Dynamic || knockbackForce <= 0f)
            return;

        Vector2 direction = hitDirection.sqrMagnitude > 0.001f
            ? hitDirection.normalized
            : ((Vector2)transform.position - hitPoint).normalized;

        if (direction.sqrMagnitude > 0.001f)
            body.AddForce(direction * knockbackForce, ForceMode2D.Impulse);
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        if (body != null)
            body.linearVelocity = Vector2.zero;

        Destroy(gameObject);
    }
}
