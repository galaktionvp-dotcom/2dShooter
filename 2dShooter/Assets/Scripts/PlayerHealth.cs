using System.Collections;
using InspectorLocalization;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerHealth : MonoBehaviour
{
    [Header("Здоровье игрока")]
    [InspectorLabel("Максимальное здоровье", "Количество здоровья игрока в начале игры.")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    [InspectorLabel("Защита после попадания", "Сколько секунд игрок не получает повторный урон после одного удара.")]
    [SerializeField, Min(0f)] private float invulnerabilityDuration = 0.65f;

    [Header("Обратная связь")]
    [InspectorLabel("Длительность вспышки", "Как долго после попадания спрайт игрока остаётся ярко подсвеченным.")]
    [SerializeField, Min(0f)] private float hitFlashDuration = 0.12f;

    [InspectorLabel("Цвет попадания", "Цвет подсветки игрока при получении урона.")]
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Header("Полоска здоровья на экране")]
    [InspectorLabel("Отступ слева", "Расстояние от левого края экрана до полоски здоровья в пикселях.")]
    [SerializeField, Min(0f)] private float hudPositionX = 20f;

    [InspectorLabel("Отступ сверху", "Расстояние от верхнего края экрана до полоски здоровья в пикселях.")]
    [SerializeField, Min(0f)] private float hudPositionY = 20f;

    [InspectorLabel("Ширина полоски", "Ширина полоски здоровья игрока в пикселях.")]
    [SerializeField, Min(80f)] private float hudWidth = 230f;

    [InspectorLabel("Высота полоски", "Высота заполненной части полоски здоровья игрока в пикселях.")]
    [SerializeField, Min(6f)] private float hudHeight = 14f;

    private float currentHealth;
    private float invulnerableUntil;
    private bool isDead;
    private Rigidbody2D body;
    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private Coroutine hitFeedbackRoutine;
    private CharacterSpriteAnimator characterAnimator;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        invulnerabilityDuration = Mathf.Max(0f, invulnerabilityDuration);
        hitFlashDuration = Mathf.Max(0f, hitFlashDuration);
        hudPositionX = Mathf.Max(0f, hudPositionX);
        hudPositionY = Mathf.Max(0f, hudPositionY);
        hudWidth = Mathf.Max(80f, hudWidth);
        hudHeight = Mathf.Max(6f, hudHeight);

        currentHealth = maxHealth;
        body = GetComponent<Rigidbody2D>();
        characterAnimator = GetComponent<CharacterSpriteAnimator>();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
            originalColors[i] = spriteRenderers[i] != null ? spriteRenderers[i].color : Color.white;
    }

    public bool TakeDamage(float damage, Vector2 hitPoint, Vector2 hitDirection)
    {
        if (isDead || damage <= 0f || Time.time < invulnerableUntil)
            return false;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        invulnerableUntil = Time.time + invulnerabilityDuration;
        PlayHitFeedback();
        characterAnimator?.PlayHit();

        if (currentHealth <= 0f)
            Die();

        return true;
    }

    private void PlayHitFeedback()
    {
        if (spriteRenderers.Length == 0)
            return;

        if (hitFeedbackRoutine != null)
            StopCoroutine(hitFeedbackRoutine);

        hitFeedbackRoutine = StartCoroutine(HitFeedback());
    }

    private IEnumerator HitFeedback()
    {
        float flashEndsAt = Time.time + hitFlashDuration;
        bool showHitColor = true;

        while (!isDead && Time.time < invulnerableUntil)
        {
            bool shouldFlash = Time.time < flashEndsAt || showHitColor;
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                    spriteRenderers[i].color = shouldFlash ? hitFlashColor : originalColors[i];
            }

            showHitColor = !showHitColor;
            yield return new WaitForSeconds(0.07f);
        }

        RestoreOriginalColors();
        hitFeedbackRoutine = null;
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

    private void OnGUI()
    {
        DrawHealthHud();

        if (isDead)
            DrawDeathMessage();
    }

    private void DrawHealthHud()
    {
        const float panelPadding = 10f;
        const float titleHeight = 20f;
        const float panelHeight = 52f;

        Rect panel = new Rect(
            hudPositionX,
            hudPositionY,
            hudWidth + panelPadding * 2f,
            panelHeight
        );

        Color previousColor = GUI.color;
        GUI.color = new Color(0.035f, 0.045f, 0.055f, 0.88f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.Label(
            new Rect(panel.x + panelPadding, panel.y + 4f, hudWidth, titleHeight),
            $"ЗДОРОВЬЕ  {Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}"
        );

        Rect barBackground = new Rect(
            panel.x + panelPadding,
            panel.y + 29f,
            hudWidth,
            hudHeight
        );
        GUI.color = new Color(0.12f, 0.14f, 0.16f, 1f);
        GUI.DrawTexture(barBackground, Texture2D.whiteTexture);

        float ratio = Mathf.Clamp01(currentHealth / maxHealth);
        Rect barFill = new Rect(
            barBackground.x,
            barBackground.y,
            barBackground.width * ratio,
            barBackground.height
        );
        GUI.color = GetHealthColor(ratio);
        GUI.DrawTexture(barFill, Texture2D.whiteTexture);

        GUI.color = previousColor;
    }

    private void DrawDeathMessage()
    {
        float width = Mathf.Min(320f, Screen.width - 32f);
        float x = (Screen.width - width) * 0.5f;
        float y = Screen.height * 0.5f - 36f;

        Color previousColor = GUI.color;
        GUI.color = new Color(0.02f, 0.02f, 0.025f, 0.92f);
        GUI.DrawTexture(new Rect(x, y, width, 72f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(
            new Rect(x + 14f, y + 17f, width - 28f, 40f),
            "ВЫ ПОГИБЛИ — здоровье закончилось"
        );
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
        characterAnimator?.PlayDeath();
        RestoreOriginalColors();

        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.enabled = false;

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
            shooting.enabled = false;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }
    }

    private void OnDestroy()
    {
        RestoreOriginalColors();
    }
}
