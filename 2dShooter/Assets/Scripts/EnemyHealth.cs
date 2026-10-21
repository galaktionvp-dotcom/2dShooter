using System.Collections;
using InspectorLocalization;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyHealth : MonoBehaviour
{
    [Header("Здоровье")]
    [InspectorLabel("Максимальное здоровье", "Общее количество здоровья врага в начале игры.")]
    [SerializeField, Min(1f)] private float maxHealth = 3f;

    [Header("Контактный урон")]
    [InspectorLabel("Урон при касании", "Количество здоровья, которое игрок теряет при столкновении с врагом.")]
    [SerializeField, Min(0f)] private float contactDamage = 10f;

    [InspectorLabel("Интервал контактного урона", "Минимальное время между ударами врага по игроку.")]
    [SerializeField, Min(0.1f)] private float contactDamageInterval = 0.8f;

    [Header("Реакция на попадание")]
    [InspectorLabel("Сила отбрасывания", "Насколько сильно враг отталкивается от попадания. Работает с динамическим Rigidbody2D.")]
    [SerializeField, Min(0f)] private float knockbackForce = 3f;

    [InspectorLabel("Длительность вспышки", "Сколько секунд враг подсвечивается после получения урона.")]
    [SerializeField, Min(0f)] private float hitFlashDuration = 0.08f;

    [InspectorLabel("Цвет попадания", "Цвет спрайта на короткое время после попадания.")]
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Header("Полоска здоровья")]
    [InspectorLabel("Ширина полоски", "Ширина полоски здоровья над врагом в пикселях экрана.")]
    [SerializeField, Min(20f)] private float healthBarWidth = 76f;

    [InspectorLabel("Высота полоски", "Высота полоски здоровья над врагом в пикселях экрана.")]
    [SerializeField, Min(4f)] private float healthBarHeight = 8f;

    [InspectorLabel("Высота над врагом", "Смещение полоски вверх относительно центра врага в единицах Unity.")]
    [SerializeField, Min(0f)] private float healthBarVerticalOffset = 0.95f;

    private float currentHealth;
    private float nextContactDamageTime;
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
        contactDamage = Mathf.Max(0f, contactDamage);
        contactDamageInterval = Mathf.Max(0.1f, contactDamageInterval);
        knockbackForce = Mathf.Max(0f, knockbackForce);
        hitFlashDuration = Mathf.Max(0f, hitFlashDuration);
        healthBarWidth = Mathf.Max(20f, healthBarWidth);
        healthBarHeight = Mathf.Max(4f, healthBarHeight);
        healthBarVerticalOffset = Mathf.Max(0f, healthBarVerticalOffset);

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

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (isDead || contactDamage <= 0f || Time.time < nextContactDamageTime || collision == null)
            return;

        PlayerHealth player = collision.gameObject != null
            ? collision.gameObject.GetComponentInParent<PlayerHealth>()
            : null;

        if (player == null && collision.collider != null)
            player = collision.collider.GetComponentInParent<PlayerHealth>();

        if (player == null && collision.otherCollider != null)
            player = collision.otherCollider.GetComponentInParent<PlayerHealth>();

        if (player == null || player.IsDead)
            return;

        Vector2 direction = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
        Vector2 hitPoint = player.transform.position;
        if (collision.contactCount > 0)
            hitPoint = collision.GetContact(0).point;

        if (player.TakeDamage(contactDamage, hitPoint, direction))
            nextContactDamageTime = Time.time + contactDamageInterval;
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

    private void OnGUI()
    {
        if (isDead || currentHealth <= 0f)
            return;

        Camera targetCamera = Camera.main;
        if (targetCamera == null)
            return;

        Vector3 worldPosition = transform.position + Vector3.up * healthBarVerticalOffset;
        Vector3 screenPosition = targetCamera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0f)
            return;

        float x = screenPosition.x - healthBarWidth * 0.5f;
        float y = Screen.height - screenPosition.y;
        Rect background = new Rect(x, y, healthBarWidth, healthBarHeight);
        float ratio = Mathf.Clamp01(currentHealth / maxHealth);
        Rect fill = new Rect(
            x + 2f,
            y + 2f,
            Mathf.Max(0f, (healthBarWidth - 4f) * ratio),
            Mathf.Max(0f, healthBarHeight - 4f)
        );

        Color previousColor = GUI.color;
        GUI.color = new Color(0.04f, 0.04f, 0.04f, 0.9f);
        GUI.DrawTexture(background, Texture2D.whiteTexture);
        GUI.color = GetHealthColor(ratio);
        GUI.DrawTexture(fill, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private static Color GetHealthColor(float ratio)
    {
        if (ratio > 0.5f)
            return new Color(0.2f, 0.85f, 0.3f, 1f);
        if (ratio > 0.25f)
            return new Color(1f, 0.75f, 0.15f, 1f);
        return new Color(1f, 0.2f, 0.2f, 1f);
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
