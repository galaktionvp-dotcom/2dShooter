using System.Collections.Generic;
using InspectorLocalization;
using UnityEngine;

/// <summary>
/// Plays pre-sliced pixel-art sprites from a Resources atlas. No AnimatorController,
/// runtime-created textures, sprites, or GameObjects are required.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class DirectionalSpriteAnimator : MonoBehaviour
{
    private const int FrameCount = 6;
    private static readonly string[] DirectionNames =
    {
        "S", "SW", "W", "NW", "N", "NE", "E", "SE"
    };

    [Header("Атлас персонажа")]
    [InspectorLabel("Путь к атласу", "Путь к спрайт-листу внутри папки Resources без расширения .png.")]
    [SerializeField] private string resourcesAtlasPath = "Animations/PlayerDirectionalAtlas";

    [InspectorLabel("Префикс спрайтов", "Начало имени кадров в атласе: Player или Enemy.")]
    [SerializeField] private string spritePrefix = "Player";

    [Header("Скорость анимации")]
    [InspectorLabel("Частота покоя", "Количество кадров в секунду, когда персонаж стоит.")]
    [SerializeField, Range(1f, 12f)] private float idleFrameRate = 4f;

    [InspectorLabel("Частота ходьбы", "Количество кадров в секунду при обычном движении.")]
    [SerializeField, Range(4f, 20f)] private float walkFrameRate = 10f;

    [InspectorLabel("Частота бега", "Количество кадров в секунду при быстром движении.")]
    [SerializeField, Range(6f, 24f)] private float runFrameRate = 14f;

    [InspectorLabel("Порог бега", "Скорость Rigidbody2D, после которой включается анимация бега.")]
    [SerializeField, Min(0.1f)] private float runningThreshold = 3.2f;

    [InspectorLabel("Частота стрельбы", "Скорость смены кадров анимации выстрела.")]
    [SerializeField, Range(6f, 30f)] private float shootFrameRate = 20f;

    [InspectorLabel("Длительность выстрела", "Как долго после выстрела проигрывается анимация стрельбы.")]
    [SerializeField, Range(0.05f, 0.8f)] private float shootDuration = 0.28f;

    [InspectorLabel("Частота попадания", "Скорость проигрывания кадров реакции на попадание.")]
    [SerializeField, Range(6f, 30f)] private float hitFrameRate = 18f;

    [InspectorLabel("Длительность попадания", "Время показа реакции на получение урона.")]
    [SerializeField, Range(0.1f, 1f)] private float hitDuration = 0.42f;

    [InspectorLabel("Частота смерти", "Скорость проигрывания кадров анимации смерти.")]
    [SerializeField, Range(4f, 16f)] private float deathFrameRate = 9f;

    private readonly Dictionary<string, Sprite> frames = new Dictionary<string, Sprite>();
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private bool isReloading;
    private Vector2 facingDirection = Vector2.right;
    private bool hasFacingDirection;
    private bool isDead;
    private float reloadStartedAt;
    private float reloadDuration;
    private float shootStartedAt = -10f;
    private float shootEndsAt;
    private float hitStartedAt = -10f;
    private float hitEndsAt;
    private float deathStartedAt;
    private string currentFrameName;

    public bool IsReloading => isReloading;
    public bool IsDead => isDead;
    public float DeathAnimationDuration => FrameCount / Mathf.Max(1f, deathFrameRate);

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (spriteRenderer == null)
        {
            Debug.LogError($"[{nameof(DirectionalSpriteAnimator)}] На объекте '{name}' не найден SpriteRenderer.", this);
            enabled = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(resourcesAtlasPath))
        {
            Debug.LogError($"[{nameof(DirectionalSpriteAnimator)}] Не указан путь к атласу для '{name}'.", this);
            enabled = false;
            return;
        }

        Sprite[] loadedSprites = Resources.LoadAll<Sprite>(resourcesAtlasPath);
        for (int i = 0; i < loadedSprites.Length; i++)
        {
            Sprite sprite = loadedSprites[i];
            if (sprite != null)
                frames[sprite.name] = sprite;
        }

        if (frames.Count == 0)
        {
            Debug.LogError(
                $"[{nameof(DirectionalSpriteAnimator)}] Не найдены кадры в Resources/{resourcesAtlasPath}. " +
                "Проверь Sprite Mode = Multiple и импорт атласа.", this);
            enabled = false;
            return;
        }

        idleFrameRate = Mathf.Max(1f, idleFrameRate);
        walkFrameRate = Mathf.Max(1f, walkFrameRate);
        runFrameRate = Mathf.Max(1f, runFrameRate);
        runningThreshold = Mathf.Max(0.1f, runningThreshold);
        shootFrameRate = Mathf.Max(1f, shootFrameRate);
        hitFrameRate = Mathf.Max(1f, hitFrameRate);
        deathFrameRate = Mathf.Max(1f, deathFrameRate);

        SetFrame("Idle", 0);
    }

    private void LateUpdate()
    {
        if (spriteRenderer == null || frames.Count == 0)
            return;

        // The root rotates toward the aim target. The child artwork uses the
        // pre-rendered 8-way frame instead of being continuously rotated again.
        spriteRenderer.transform.rotation = Quaternion.identity;

        int direction = GetDirectionIndex();
        string state;
        int frame;

        if (isDead)
        {
            state = "Death";
            frame = Mathf.Clamp(Mathf.FloorToInt((Time.time - deathStartedAt) * deathFrameRate), 0, FrameCount - 1);
        }
        else if (Time.time < hitEndsAt)
        {
            state = "Hit";
            frame = Mathf.Clamp(Mathf.FloorToInt((Time.time - hitStartedAt) * hitFrameRate), 0, FrameCount - 1);
        }
        else if (isReloading)
        {
            state = "Reload";
            float progress = reloadDuration <= 0f
                ? 1f
                : Mathf.Clamp01((Time.time - reloadStartedAt) / reloadDuration);
            frame = Mathf.Clamp(Mathf.FloorToInt(progress * FrameCount), 0, FrameCount - 1);
        }
        else if (Time.time < shootEndsAt)
        {
            state = "Shoot";
            frame = Mathf.Clamp(Mathf.FloorToInt((Time.time - shootStartedAt) * shootFrameRate), 0, FrameCount - 1);
        }
        else
        {
            float speed = body != null ? body.linearVelocity.magnitude : 0f;
            if (speed > 0.12f)
            {
                state = speed >= runningThreshold ? "Run" : "Walk";
                float rate = state == "Run" ? runFrameRate : walkFrameRate;
                frame = Mathf.FloorToInt(Time.time * rate) % FrameCount;
            }
            else
            {
                state = "Idle";
                frame = Mathf.FloorToInt(Time.time * idleFrameRate) % FrameCount;
            }
        }

        SetFrame(state, direction, frame);
    }

    private int GetDirectionIndex()
    {
        float angle = hasFacingDirection
            ? Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg
            : transform.eulerAngles.z;

        int index = Mathf.RoundToInt(-(angle + 90f) / 45f);
        index %= DirectionNames.Length;
        if (index < 0) index += DirectionNames.Length;
        return index;
    }

    /// <summary>
    /// Sets an animation-only facing direction without rotating the physics body or collider.
    /// </summary>
    public void SetFacingDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        facingDirection = direction.normalized;
        hasFacingDirection = true;
    }

    private void SetFrame(string state, int frame)
    {
        SetFrame(state, GetDirectionIndex(), frame);
    }

    private void SetFrame(string state, int direction, int frame)
    {
        string spriteName = $"{spritePrefix}_{state}_{DirectionNames[direction]}_{Mathf.Clamp(frame, 0, FrameCount - 1):00}";
        if (spriteName == currentFrameName)
            return;

        if (frames.TryGetValue(spriteName, out Sprite sprite) && sprite != null)
        {
            spriteRenderer.sprite = sprite;
            currentFrameName = spriteName;
        }
        else if (state != "Idle")
        {
            string fallbackName = $"{spritePrefix}_Idle_{DirectionNames[direction]}_00";
            if (frames.TryGetValue(fallbackName, out Sprite fallback) && fallback != null)
            {
                spriteRenderer.sprite = fallback;
                currentFrameName = fallbackName;
            }
        }
    }

    public void PlayShoot()
    {
        if (isDead || isReloading)
            return;

        if (Time.time >= shootEndsAt)
            shootStartedAt = Time.time;
        shootEndsAt = Time.time + shootDuration;
    }

    public void BeginReload(float duration)
    {
        if (isDead)
            return;

        isReloading = true;
        reloadStartedAt = Time.time;
        reloadDuration = Mathf.Max(0.01f, duration);
        shootEndsAt = 0f;
    }

    public void FinishReload()
    {
        isReloading = false;
    }

    public void PlayHit()
    {
        if (isDead)
            return;

        hitStartedAt = Time.time;
        hitEndsAt = Time.time + hitDuration;
    }

    public void PlayDeath()
    {
        if (isDead)
            return;

        isDead = true;
        deathStartedAt = Time.time;
        isReloading = false;
        shootEndsAt = 0f;
        hitEndsAt = 0f;
    }
}
