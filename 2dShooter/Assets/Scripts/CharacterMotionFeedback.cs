using InspectorLocalization;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class CharacterMotionFeedback : MonoBehaviour
{
    private const int RunFrameCount = 8;

    [Header("Анимация бега")]
    [InspectorLabel("Визуальный объект", "Дочерний объект со SpriteRenderer. Меняется только изображение, физический коллайдер не затрагивается.")]
    [SerializeField] private Transform visualRoot;

    [InspectorLabel("Кадры бега", "Лист из 8 кадров бега для этого персонажа. Кадры расположены горизонтально.")]
    [SerializeField] private Texture2D runSheet;

    [InspectorLabel("Оттенок кадров бега", "Для врага задаёт оливковый оттенок, чтобы отличать его от игрока; для игрока оставь белый.")]
    [SerializeField] private Color runningTint = Color.white;

    [InspectorLabel("Частота кадров", "Базовая скорость смены кадров при беге. Фактическая скорость немного зависит от скорости движения.")]
    [SerializeField, Range(4f, 18f)] private float runFrameRate = 10f;

    [InspectorLabel("Порог движения", "Если скорость персонажа ниже этого значения, показывается исходная неподвижная картинка.")]
    [SerializeField, Min(0f)] private float movementThreshold = 0.12f;

    [InspectorLabel("Покачивание при беге", "Небольшое вертикальное покачивание изображения во время бега. Коллайдер и Rigidbody2D не двигаются отдельно.")]
    [SerializeField, Range(0f, 0.06f)] private float bobAmount = 0.015f;

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Sprite idleSprite;
    private Color idleColor;
    private Sprite[] runFrames;
    private Vector3 baseLocalPosition;
    private float animationTime;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();

        if (visualRoot == null)
        {
            SpriteRenderer childSprite = GetComponentInChildren<SpriteRenderer>(true);
            if (childSprite != null)
                visualRoot = childSprite.transform;
        }

        if (visualRoot == null)
        {
            Debug.LogError("[" + nameof(CharacterMotionFeedback) + "] Не найден визуальный объект со SpriteRenderer.", this);
            enabled = false;
            return;
        }

        spriteRenderer = visualRoot.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError("[" + nameof(CharacterMotionFeedback) + "] На визуальном объекте должен быть SpriteRenderer.", this);
            enabled = false;
            return;
        }

        idleSprite = spriteRenderer.sprite;
        idleColor = spriteRenderer.color;
        baseLocalPosition = visualRoot.localPosition;

        runFrameRate = Mathf.Clamp(runFrameRate, 4f, 18f);
        movementThreshold = Mathf.Max(0f, movementThreshold);
        bobAmount = Mathf.Max(0f, bobAmount);

        BuildRunFrames();
    }

    private void BuildRunFrames()
    {
        if (runSheet == null)
        {
            Debug.LogWarning("[" + nameof(CharacterMotionFeedback) + "] Не назначен лист кадров бега. Персонаж останется с исходным спрайтом.", this);
            return;
        }

        if (runSheet.width % RunFrameCount != 0 || runSheet.height <= 0)
        {
            Debug.LogError("[" + nameof(CharacterMotionFeedback) + "] Лист бега должен содержать ровно 8 одинаковых кадров в одну горизонтальную строку.", this);
            return;
        }

        int frameWidth = runSheet.width / RunFrameCount;
        if (frameWidth <= 0)
        {
            Debug.LogError("[" + nameof(CharacterMotionFeedback) + "] Неверный размер листа кадров бега.", this);
            return;
        }

        Vector2 pivot = idleSprite != null
            ? new Vector2(idleSprite.pivot.x / idleSprite.rect.width, idleSprite.pivot.y / idleSprite.rect.height)
            : new Vector2(0.5f, 0.5f);
        float pixelsPerUnit = idleSprite != null ? idleSprite.pixelsPerUnit : 80f;

        runFrames = new Sprite[RunFrameCount];
        for (int i = 0; i < RunFrameCount; i++)
        {
            runFrames[i] = Sprite.Create(
                runSheet,
                new Rect(i * frameWidth, 0f, frameWidth, runSheet.height),
                pivot,
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);

            runFrames[i].name = gameObject.name + "_Run_" + i;
            runFrames[i].hideFlags = HideFlags.HideAndDontSave;
        }
    }

    private void Update()
    {
        if (spriteRenderer == null || idleSprite == null)
            return;

        float speed = body != null ? body.linearVelocity.magnitude : 0f;
        if (runFrames == null || runFrames.Length == 0 || speed <= movementThreshold)
        {
            if (spriteRenderer.sprite != idleSprite)
                spriteRenderer.sprite = idleSprite;
            spriteRenderer.color = idleColor;

            float restoreBlend = 1f - Mathf.Exp(-15f * Time.deltaTime);
            visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, baseLocalPosition, restoreBlend);
            return;
        }

        float speedBlend = Mathf.InverseLerp(movementThreshold, 5f, speed);
        float currentFrameRate = runFrameRate * Mathf.Lerp(0.75f, 1.2f, speedBlend);
        animationTime += Time.deltaTime * currentFrameRate;

        int frameIndex = Mathf.FloorToInt(animationTime) % RunFrameCount;
        if (spriteRenderer.sprite != runFrames[frameIndex])
            spriteRenderer.sprite = runFrames[frameIndex];
        spriteRenderer.color = runningTint;

        float phase = (animationTime / RunFrameCount) * Mathf.PI * 2f;
        Vector3 targetPosition = baseLocalPosition + Vector3.up * (Mathf.Abs(Mathf.Sin(phase)) * bobAmount);
        float blend = 1f - Mathf.Exp(-20f * Time.deltaTime);
        visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, targetPosition, blend);
    }

    private void OnDisable()
    {
        RestoreVisual();
    }

    private void OnDestroy()
    {
        RestoreVisual();
        ReleaseRunFrames();
    }

    private void RestoreVisual()
    {
        if (spriteRenderer != null && idleSprite != null)
        {
            spriteRenderer.sprite = idleSprite;
            spriteRenderer.color = idleColor;
        }

        if (visualRoot != null)
            visualRoot.localPosition = baseLocalPosition;
    }

    private void ReleaseRunFrames()
    {
        if (runFrames == null)
            return;

        for (int i = 0; i < runFrames.Length; i++)
        {
            if (runFrames[i] == null)
                continue;

            if (Application.isPlaying)
                Destroy(runFrames[i]);
            else
                DestroyImmediate(runFrames[i]);
        }

        runFrames = null;
    }
}
