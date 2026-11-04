using System.Collections;
using InspectorLocalization;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AllyHealth : MonoBehaviour
{
    [Header("Здоровье союзника")]
    [InspectorLabel("Максимальное здоровье", "Запас здоровья союзника. Он ниже, чем у игрока, поэтому союзник может погибнуть в бою.")]
    [SerializeField, Min(1f)] private float maxHealth = 18f;

    [InspectorLabel("Сила отбрасывания", "Насколько сильно союзник отталкивается после попадания.")]
    [SerializeField, Min(0f)] private float knockbackForce = 2.5f;

    [Header("Реакция на попадание")]
    [InspectorLabel("Длительность вспышки", "Сколько секунд союзник подсвечивается после получения урона.")]
    [SerializeField, Min(0f)] private float hitFlashDuration = 0.08f;

    [InspectorLabel("Цвет попадания", "Цвет спрайта на короткое время после попадания.")]
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.45f, 0.35f, 1f);

    [Header("Полоска здоровья")]
    [InspectorLabel("Ширина полоски", "Ширина небольшой полоски здоровья над союзником в пикселях экрана.")]
    [SerializeField, Min(20f)] private float healthBarWidth = 54f;

    [InspectorLabel("Высота полоски", "Высота полоски здоровья над союзником в пикселях экрана.")]
    [SerializeField, Min(4f)] private float healthBarHeight = 6f;

    [InspectorLabel("Высота над союзником", "Смещение полоски вверх относительно центра союзника в единицах Unity.")]
    [SerializeField, Min(0f)] private float healthBarVerticalOffset = 0.8f;

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
        healthBarWidth = Mathf.Max(20f, healthBarWidth);
        healthBarHeight = Mathf.Max(4f, healthBarHeight);
        healthBarVerticalOffset = Mathf.Max(0f, healthBarVerticalOffset);

        currentHealth = maxHealth;
        body = GetComponent<Rigidbody2D>();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
            originalColors[i] = spriteRenderers[i] != null ? spriteRenderers[i].color : Color.white;
    }

    public bool TakeDamage(float damage, Vector2 hitPoint, Vector2 hitDirection)
    {
        if (isDead || damage <= 0f)
            return false;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        PlayHitFeedback();
        ApplyKnockback(hitPoint, hitDirection);

        if (currentHealth <= 0f)
            Die();

        return true;
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
            x + 1f,
            y + 1f,
            Mathf.Max(0f, (healthBarWidth - 2f) * ratio),
            Mathf.Max(0f, healthBarHeight - 2f));

        Color previousColor = GUI.color;
        GUI.color = new Color(0.04f, 0.04f, 0.04f, 0.9f);
        GUI.DrawTexture(background, Texture2D.whiteTexture);
        GUI.color = new Color(0.2f, 0.8f, 1f, 1f);
        GUI.DrawTexture(fill, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        AllyUnitAI allyAI = GetComponent<AllyUnitAI>();
        if (allyAI != null)
            allyAI.enabled = false;

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (hitFlashRoutine != null)
            StopCoroutine(hitFlashRoutine);

        RestoreOriginalColors();
    }

    private void RestoreOriginalColors()
    {
        if (spriteRenderers == null || originalColors == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = originalColors[i];
        }
    }
}
