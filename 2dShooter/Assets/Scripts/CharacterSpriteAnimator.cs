using InspectorLocalization;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class CharacterSpriteAnimator : MonoBehaviour
{
    private const int FrameColumns = 8;
    private const int FrameWidth = 32;
    private const int FrameHeight = 64;
    private const int PlayerRowOffset = 0;
    private const int EnemyRowOffset = 6;

    [InspectorLabel("Лист анимаций", "Текстура 8×12: по 8 кадров на каждое состояние игрока и врага.")]
    [SerializeField] private Texture2D animationSheet;

    [InspectorLabel("Это враг", "Включи для врага, чтобы использовать вторую половину листа кадров.")]
    [SerializeField] private bool isEnemy;

    [InspectorLabel("Масштаб спрайта", "Количество пикселей на одну единицу Unity; лист кадров настроен на 45.")]
    [SerializeField, Min(1f)] private float pixelsPerUnit = 45f;

    [InspectorLabel("Частота простоя", "Скорость смены кадров лёгкого дыхания, когда персонаж стоит.")]
    [SerializeField, Range(1f, 10f)] private float idleFrameRate = 4f;

    [InspectorLabel("Частота ходьбы", "Скорость кадров при обычном перемещении.")]
    [SerializeField, Range(4f, 18f)] private float walkFrameRate = 10f;

    [InspectorLabel("Частота быстрого бега", "Скорость кадров ходьбы при движении быстрее порога.")]
    [SerializeField, Range(6f, 24f)] private float runFrameRate = 16f;

    [InspectorLabel("Порог быстрого бега", "С какой скорости кадры ходьбы переключаются на более быстрый темп.")]
    [SerializeField, Min(0.1f)] private float runningThreshold = 3.2f;

    [InspectorLabel("Частота действий", "Скорость кадров выстрела и реакции на попадание.")]
    [SerializeField, Range(6f, 30f)] private float actionFrameRate = 20f;

    [InspectorLabel("Длительность выстрела", "Сколько секунд проигрывается реакция оружия после выстрела.")]
    [SerializeField, Range(0.05f, 0.8f)] private float shootDuration = 0.28f;

    [InspectorLabel("Длительность реакции на урон", "Сколько секунд длится анимация получения урона.")]
    [SerializeField, Range(0.1f, 1f)] private float hitDuration = 0.42f;

    [InspectorLabel("Частота анимации смерти", "Скорость покадровой анимации смерти персонажа.")]
    [SerializeField, Range(4f, 16f)] private float deathFrameRate = 9f;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D body;
    private Sprite[,] frames;
    private Sprite originalSprite;
    private bool isReloading;
    private bool isDead;
    private float reloadStartedAt;
    private float reloadDuration;
    private float shootStartedAt;
    private float shootEndsAt;
    private float hitStartedAt;
    private float hitEndsAt;
    private float deathStartedAt;

    public float DeathAnimationDuration => FrameColumns / Mathf.Max(1f, deathFrameRate);

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
        originalSprite = spriteRenderer.sprite;

        if (animationSheet == null)
        {
            Debug.LogError("Для " + name + " не назначен лист анимаций CharacterAnimations.png.", this);
            enabled = false;
            return;
        }

        if (animationSheet.width < FrameColumns * FrameWidth ||
            animationSheet.height < 12 * FrameHeight)
        {
            Debug.LogError("Лист анимаций должен содержать 8 столбцов и 12 строк по 32×64 пикселя.", this);
            enabled = false;
            return;
        }

        pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
        idleFrameRate = Mathf.Max(1f, idleFrameRate);
        walkFrameRate = Mathf.Max(1f, walkFrameRate);
        runFrameRate = Mathf.Max(1f, runFrameRate);
        runningThreshold = Mathf.Max(0.1f, runningThreshold);
        actionFrameRate = Mathf.Max(1f, actionFrameRate);
        deathFrameRate = Mathf.Max(1f, deathFrameRate);

        BuildFrames();
        SetFrame(0, 0);
    }

    private void BuildFrames()
    {
        frames = new Sprite[12, FrameColumns];

        for (int row = 0; row < 12; row++)
        {
            for (int column = 0; column < FrameColumns; column++)
            {
                float x = column * FrameWidth;
                float y = animationSheet.height - (row + 1) * FrameHeight;
                frames[row, column] = Sprite.Create(
                    animationSheet,
                    new Rect(x, y, FrameWidth, FrameHeight),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit);
                frames[row, column].name = (row < 6 ? "Player_" : "Enemy_") +
                    RowName(row % 6) + "_" + column;
                frames[row, column].hideFlags = HideFlags.HideAndDontSave;
            }
        }
    }

    private static string RowName(int row)
    {
        switch (row)
        {
            case 0: return "Idle";
            case 1: return "Walk";
            case 2: return "Shoot";
            case 3: return "Reload";
            case 4: return "Hit";
            default: return "Death";
        }
    }

    private void Update()
    {
        if (frames == null || spriteRenderer == null)
            return;

        if (isDead)
        {
            int deathFrame = Mathf.Clamp(
                Mathf.FloorToInt((Time.time - deathStartedAt) * deathFrameRate),
                0, FrameColumns - 1);
            SetFrame(5, deathFrame);
            return;
        }

        if (Time.time < hitEndsAt)
        {
            SetFrame(4, ElapsedFrame(hitStartedAt, actionFrameRate));
            return;
        }

        if (isReloading)
        {
            float progress = reloadDuration <= 0f
                ? 1f
                : Mathf.Clamp01((Time.time - reloadStartedAt) / reloadDuration);
            int reloadFrame = Mathf.Clamp(Mathf.FloorToInt(progress * FrameColumns), 0, FrameColumns - 1);
            SetFrame(3, reloadFrame);
            return;
        }

        if (Time.time < shootEndsAt)
        {
            SetFrame(2, ElapsedFrame(shootStartedAt, actionFrameRate));
            return;
        }

        float speed = body != null ? body.linearVelocity.magnitude : 0f;
        if (speed > 0.12f)
        {
            float rate = speed >= runningThreshold ? runFrameRate : walkFrameRate;
            SetFrame(1, Mathf.FloorToInt(Time.time * rate) % FrameColumns);
        }
        else
        {
            SetFrame(0, Mathf.FloorToInt(Time.time * idleFrameRate) % FrameColumns);
        }
    }

    private int ElapsedFrame(float startedAt, float frameRate)
    {
        return Mathf.Clamp(Mathf.FloorToInt((Time.time - startedAt) * frameRate), 0, FrameColumns - 1);
    }

    private void SetFrame(int stateRow, int frame)
    {
        if (frames == null || spriteRenderer == null)
            return;

        int row = (isEnemy ? EnemyRowOffset : PlayerRowOffset) + stateRow;
        Sprite nextSprite = frames[row, Mathf.Clamp(frame, 0, FrameColumns - 1)];
        if (nextSprite != null && spriteRenderer.sprite != nextSprite)
            spriteRenderer.sprite = nextSprite;
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

        if (duration <= 0f)
            FinishReload();
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

    private void OnDestroy()
    {
        if (frames == null)
            return;

        for (int row = 0; row < frames.GetLength(0); row++)
        {
            for (int column = 0; column < frames.GetLength(1); column++)
            {
                Sprite frame = frames[row, column];
                if (frame == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(frame);
                else
                    DestroyImmediate(frame);
            }
        }
    }
}
