using InspectorLocalization;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class CharacterSpriteAnimator : MonoBehaviour
{
    private const int FrameColumns = 8;
    private const int FrameWidth = 24;
    private const int FrameHeight = 48;
    private const int SheetWidth = FrameWidth * FrameColumns;
    private const int SheetHeight = FrameHeight * 12;

    [InspectorLabel("Лист кадров", "Необязательная текстура 8×12 кадров (24×48 пикселей). Если не назначена, создаётся встроенный процедурный атлас.")]
    [SerializeField] private Texture2D animationSheet;

    [InspectorLabel("Это враг", "Включи для врага, чтобы проигрывать кадры из нижней половины атласа.")]
    [SerializeField] private bool isEnemy;

    [InspectorLabel("Пикселей на единицу", "Плотность пикселей для каждого кадра. Значение 45 сохраняет размер около 0,53×1,07 единицы.")]
    [SerializeField, Min(1f)] private float pixelsPerUnit = 45f;

    [InspectorLabel("Частота простоя", "Скорость смены кадров дыхания, когда персонаж стоит.")]
    [SerializeField, Range(1f, 10f)] private float idleFrameRate = 4f;

    [InspectorLabel("Частота ходьбы", "Скорость кадров обычного движения.")]
    [SerializeField, Range(4f, 18f)] private float walkFrameRate = 10f;

    [InspectorLabel("Частота быстрого бега", "Скорость кадров движения при высокой скорости.")]
    [SerializeField, Range(6f, 24f)] private float runFrameRate = 16f;

    [InspectorLabel("Порог быстрого бега", "Скорость, после которой используется быстрый темп ходьбы.")]
    [SerializeField, Min(0.1f)] private float runningThreshold = 3.2f;

    [InspectorLabel("Частота действий", "Скорость кадров стрельбы и реакции на попадание.")]
    [SerializeField, Range(6f, 30f)] private float actionFrameRate = 20f;

    [InspectorLabel("Длительность стрельбы", "Время проигрывания реакции оружия после выстрела.")]
    [SerializeField, Range(0.05f, 0.8f)] private float shootDuration = 0.28f;

    [InspectorLabel("Длительность получения урона", "Время проигрывания анимации попадания.")]
    [SerializeField, Range(0.1f, 1f)] private float hitDuration = 0.42f;

    [InspectorLabel("Частота смерти", "Скорость покадровой анимации падения.")]
    [SerializeField, Range(4f, 16f)] private float deathFrameRate = 9f;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D body;
    private Sprite[,] frames;
    private Texture2D generatedSheet;
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

        if (animationSheet == null)
        {
            generatedSheet = CreateProceduralSheet();
            animationSheet = generatedSheet;
        }

        if (animationSheet == null ||
            animationSheet.width < SheetWidth ||
            animationSheet.height < SheetHeight)
        {
            Debug.LogError("Для " + name + " нужен лист анимаций 8×12 с кадрами 24×48 пикселей.", this);
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

    private Texture2D CreateProceduralSheet()
    {
        Color32[] pixels = new Color32[SheetWidth * SheetHeight];

        for (int row = 0; row < 12; row++)
        {
            bool enemy = row >= 6;
            int state = row % 6;

            for (int column = 0; column < FrameColumns; column++)
            {
                Color32[] framePixels = DrawCharacterFrame(enemy, state, column);
                if (state == 5 && column > 0)
                    framePixels = RotateDeathFrame(framePixels, column);

                for (int y = 0; y < FrameHeight; y++)
                {
                    int atlasY = SheetHeight - 1 - (row * FrameHeight + y);
                    int destination = atlasY * SheetWidth + column * FrameWidth;
                    System.Array.Copy(framePixels, y * FrameWidth, pixels, destination, FrameWidth);
                }
            }
        }

        Texture2D texture = new Texture2D(SheetWidth, SheetHeight, TextureFormat.RGBA32, false);
        texture.name = "ProceduralCharacterAnimationSheet";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static Color32[] DrawCharacterFrame(bool enemy, int state, int frame)
    {
        Color32 transparent = new Color32(0, 0, 0, 0);
        Color32 outline = new Color32(12, 16, 19, 255);
        Color32 helmet = enemy ? new Color32(49, 65, 35, 255) : new Color32(39, 57, 75, 255);
        Color32 helmetLight = enemy ? new Color32(108, 129, 71, 255) : new Color32(95, 124, 149, 255);
        Color32 vest = enemy ? new Color32(65, 84, 43, 255) : new Color32(55, 82, 108, 255);
        Color32 vestLight = enemy ? new Color32(132, 153, 87, 255) : new Color32(113, 145, 170, 255);
        Color32 pants = enemy ? new Color32(63, 65, 43, 255) : new Color32(57, 73, 88, 255);
        Color32 pantsLight = enemy ? new Color32(105, 112, 71, 255) : new Color32(94, 117, 135, 255);
        Color32 skin = new Color32(220, 164, 120, 255);
        Color32 skinLight = new Color32(244, 197, 151, 255);
        Color32 boot = new Color32(19, 24, 28, 255);
        Color32 strap = new Color32(184, 145, 87, 255);
        Color32 gun = new Color32(35, 39, 43, 255);
        Color32 gunLight = new Color32(176, 179, 177, 255);
        Color32 red = new Color32(242, 56, 56, 255);
        Color32 yellow = new Color32(255, 205, 74, 255);

        Color32[] p = new Color32[FrameWidth * FrameHeight];
        for (int i = 0; i < p.Length; i++) p[i] = transparent;

        int bob = state == 0 ? new int[] { 0, 0, -1, 0, 1, 0, -1, 0 }[frame] :
                  state == 1 ? new int[] { 0, -1, 0, 1, 0, -1, 0, 1 }[frame] : 0;
        int step = state == 1 ? new int[] { -3, -2, 0, 2, 3, 2, 0, -2 }[frame] : 0;
        int armShift = state == 1 ? new int[] { 2, 1, 0, -1, -2, -1, 0, 1 }[frame] : 0;
        int recoil = state == 2 ? new int[] { 0, 0, 1, 2, 1, 0, 0, 0 }[frame] : 0;
        int reloadShift = state == 3 ? new int[] { 0, 0, 1, 2, 3, 2, 1, 0 }[frame] : 0;
        int hitLean = state == 4 ? new int[] { 0, 0, 1, 2, 2, 1, 0, 0 }[frame] : 0;
        int xShift = -hitLean;
        int yShift = bob;

        // Shadow and boots.
        DrawEllipse(p, 12, 43, 8, 3, new Color32(25, 28, 30, 255));
        DrawRect(p, 6 + xShift, 35 + yShift, 10 + xShift, 41 + yShift, outline);
        DrawRect(p, 13 + xShift, 35 + yShift, 17 + xShift, 41 + yShift, outline);
        DrawRect(p, 5 + xShift + step / 2, 40 + yShift, 10 + xShift + step / 2, 44 + yShift, boot);
        DrawRect(p, 13 + xShift - step / 2, 40 + yShift, 19 + xShift - step / 2, 44 + yShift, boot);
        DrawLine(p, 7 + xShift, 36 + yShift, 8 + xShift + step / 2, 40 + yShift, 2, pantsLight);
        DrawLine(p, 14 + xShift, 36 + yShift, 15 + xShift - step / 2, 40 + yShift, 2, pantsLight);

        // Pack, torso and vest.
        DrawRect(p, 5 + xShift, 18 + yShift, 10 + xShift, 29 + yShift, outline);
        DrawRect(p, 7 + xShift, 17 + yShift, 16 + xShift, 32 + yShift, outline);
        DrawRect(p, 8 + xShift, 18 + yShift, 15 + xShift, 30 + yShift, vest);
        DrawRect(p, 8 + xShift, 19 + yShift, 12 + xShift, 25 + yShift, vestLight);
        DrawRect(p, 13 + xShift, 21 + yShift, 15 + xShift, 28 + yShift, helmet);
        DrawLine(p, 8 + xShift, 19 + yShift, 14 + xShift, 28 + yShift, 1, strap);
        DrawRect(p, 9 + xShift, 25 + yShift, 12 + xShift, 28 + yShift, helmetLight);

        // Head and helmet; the weapon points along local +X, matching the aiming scripts.
        DrawEllipse(p, 11 + xShift, 13 + yShift, 5, 6, outline);
        DrawEllipse(p, 12 + xShift, 13 + yShift, 4, 5, skin);
        DrawRect(p, 14 + xShift, 11 + yShift, 16 + xShift, 15 + yShift, skinLight);
        DrawEllipse(p, 10 + xShift, 8 + yShift, 5, 5, outline);
        DrawEllipse(p, 10 + xShift, 8 + yShift, 4, 4, helmet);
        DrawEllipse(p, 10 + xShift, 7 + yShift, 3, 2, helmetLight);
        DrawLine(p, 8 + xShift, 8 + yShift, 13 + xShift, 7 + yShift, 1, outline);

        // Arms and rifle.
        DrawLine(p, 9 + xShift, 21 + yShift, 7 + xShift, 26 + yShift, 3, outline);
        DrawLine(p, 9 + xShift, 21 + yShift, 8 + xShift, 26 + yShift, 2, vestLight);
        DrawLine(p, 14 + xShift, 20 + yShift, 17 + xShift, 22 + yShift + armShift / 2, 3, outline);
        DrawEllipse(p, 16 + xShift, 22 + yShift + armShift / 2, 2, 2, skinLight);
        int gunY = 20 + yShift - reloadShift / 2;
        DrawRect(p, 15 + xShift - recoil, gunY - 1, 22 + xShift - recoil, gunY + 1, outline);
        DrawRect(p, 16 + xShift - recoil, gunY - 1, 22 + xShift - recoil, gunY, gun);
        DrawLine(p, 18 + xShift - recoil, gunY - 1, 22 + xShift - recoil, gunY - 1, 1, gunLight);
        DrawRect(p, 17 + xShift - recoil, gunY + 1, 18 + xShift - recoil, gunY + 4, gun);
        if (state == 3 && frame >= 2 && frame <= 5)
            DrawRect(p, 17 + xShift, 25 + reloadShift, 19 + xShift, 29 + reloadShift, outline);
        if (state == 2 && (frame == 2 || frame == 3 || frame == 6))
        {
            DrawLine(p, 22 + xShift - recoil, gunY, 23 + xShift - recoil, gunY - 2, 2, yellow);
            DrawRect(p, 22 + xShift - recoil, gunY - 1, 23 + xShift - recoil, gunY + 1, skinLight);
        }

        // Hurt frames flash red details without changing the sprite's scale.
        if (state == 4 && frame >= 2 && frame <= 4)
        {
            DrawLine(p, 5 + xShift, 17 + yShift, 8 + xShift, 20 + yShift, 2, red);
            DrawRect(p, 16 + xShift, 18 + yShift, 18 + xShift, 20 + yShift, red);
        }

        return p;
    }

    private static Color32[] RotateDeathFrame(Color32[] source, int frame)
    {
        Color32[] result = new Color32[source.Length];
        float angle = new float[] { 0f, 8f, 18f, 32f, 48f, 64f, 78f, 90f }[frame] * Mathf.Deg2Rad;
        float squash = frame >= 5 ? 0.78f : 1f;
        float cx = 12f;
        float cy = 28f;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);

        for (int y = 0; y < FrameHeight; y++)
        {
            for (int x = 0; x < FrameWidth; x++)
            {
                Color32 color = source[y * FrameWidth + x];
                if (color.a == 0) continue;
                float dx = x - cx;
                float dy = (y - cy) * squash;
                int nx = Mathf.RoundToInt(cx + dx * cos - dy * sin);
                int ny = Mathf.RoundToInt(cy + dx * sin + dy * cos);
                if (nx >= 0 && nx < FrameWidth && ny >= 0 && ny < FrameHeight)
                    result[ny * FrameWidth + nx] = color;
            }
        }

        return result;
    }

    private static void DrawRect(Color32[] p, int x1, int y1, int x2, int y2, Color32 color)
    {
        int left = Mathf.Max(0, Mathf.Min(x1, x2));
        int right = Mathf.Min(FrameWidth - 1, Mathf.Max(x1, x2));
        int top = Mathf.Max(0, Mathf.Min(y1, y2));
        int bottom = Mathf.Min(FrameHeight - 1, Mathf.Max(y1, y2));
        for (int y = top; y <= bottom; y++)
            for (int x = left; x <= right; x++)
                p[y * FrameWidth + x] = color;
    }

    private static void DrawEllipse(Color32[] p, int cx, int cy, int rx, int ry, Color32 color)
    {
        if (rx <= 0 || ry <= 0) return;
        for (int y = Mathf.Max(0, cy - ry); y <= Mathf.Min(FrameHeight - 1, cy + ry); y++)
        {
            for (int x = Mathf.Max(0, cx - rx); x <= Mathf.Min(FrameWidth - 1, cx + rx); x++)
            {
                float dx = (x - cx) / (float)rx;
                float dy = (y - cy) / (float)ry;
                if (dx * dx + dy * dy <= 1f)
                    p[y * FrameWidth + x] = color;
            }
        }
    }

    private static void DrawLine(Color32[] p, int x1, int y1, int x2, int y2, int width, Color32 color)
    {
        int steps = Mathf.Max(Mathf.Abs(x2 - x1), Mathf.Abs(y2 - y1));
        if (steps == 0)
        {
            DrawEllipse(p, x1, y1, Mathf.Max(1, width / 2), Mathf.Max(1, width / 2), color);
            return;
        }

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(x1, x2, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(y1, y2, t));
            DrawEllipse(p, x, y, Mathf.Max(1, width / 2), Mathf.Max(1, width / 2), color);
        }
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
        if (frames == null || spriteRenderer == null) return;

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
            SetFrame(3, Mathf.Clamp(Mathf.FloorToInt(progress * FrameColumns), 0, FrameColumns - 1));
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
        if (frames == null || spriteRenderer == null) return;
        int baseRow = isEnemy ? 6 : 0;
        Sprite next = frames[baseRow + stateRow, Mathf.Clamp(frame, 0, FrameColumns - 1)];
        if (next != null && spriteRenderer.sprite != next)
            spriteRenderer.sprite = next;
    }

    public void PlayShoot()
    {
        if (isDead || isReloading) return;
        if (Time.time >= shootEndsAt)
            shootStartedAt = Time.time;
        shootEndsAt = Time.time + shootDuration;
    }

    public void BeginReload(float duration)
    {
        if (isDead) return;
        isReloading = true;
        reloadStartedAt = Time.time;
        reloadDuration = Mathf.Max(0.01f, duration);
        shootEndsAt = 0f;
        if (duration <= 0f) FinishReload();
    }

    public void FinishReload()
    {
        isReloading = false;
    }

    public void PlayHit()
    {
        if (isDead) return;
        hitStartedAt = Time.time;
        hitEndsAt = Time.time + hitDuration;
    }

    public void PlayDeath()
    {
        if (isDead) return;
        isDead = true;
        deathStartedAt = Time.time;
        isReloading = false;
        shootEndsAt = 0f;
        hitEndsAt = 0f;
    }

    private void OnDestroy()
    {
        if (frames != null)
        {
            for (int row = 0; row < frames.GetLength(0); row++)
            {
                for (int column = 0; column < frames.GetLength(1); column++)
                {
                    Sprite frame = frames[row, column];
                    if (frame == null) continue;
                    if (Application.isPlaying) Destroy(frame);
                    else DestroyImmediate(frame);
                }
            }
        }

        if (generatedSheet != null)
        {
            if (Application.isPlaying) Destroy(generatedSheet);
            else DestroyImmediate(generatedSheet);
        }
    }
}
