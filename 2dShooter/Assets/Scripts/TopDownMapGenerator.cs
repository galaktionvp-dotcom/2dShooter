using InspectorLocalization;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Создаёт статическую арену в открытой сцене Unity Editor.
/// Во время Play Mode и в собранной игре генерация не выполняется.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class TopDownMapGenerator : MonoBehaviour
{
    [Header("Размер арены")]
    [InspectorLabel("Ширина арены", "Ширина игровой площадки между внутренними сторонами граничных стен, в единицах Unity.")]
    [SerializeField, Min(10f)] private float width = 30f;

    [InspectorLabel("Высота арены", "Высота игровой площадки между внутренними сторонами граничных стен, в единицах Unity.")]
    [SerializeField, Min(8f)] private float height = 20f;

    [InspectorLabel("Толщина стен", "Толщина физических стен по периметру. Стены должны быть достаточно толстыми, чтобы персонажи не проходили сквозь них.")]
    [SerializeField, Min(0.25f)] private float wallThickness = 1f;

    [Header("Цвета арены")]
    [InspectorLabel("Цвет пола", "Основной тёмный цвет пола арены.")]
    [SerializeField] private Color floorColor = new Color(0.18f, 0.22f, 0.23f, 1f);

    [InspectorLabel("Цвет стен", "Цвет массивных стен по краям карты.")]
    [SerializeField] private Color wallColor = new Color(0.06f, 0.09f, 0.1f, 1f);

    [InspectorLabel("Цвет разметки союзников", "Цвет визуальной разметки стартовой зоны команды игрока. Разметка не имеет коллайдера.")]
    [SerializeField] private Color playerZoneColor = new Color(0.18f, 0.55f, 0.72f, 0.65f);

    [InspectorLabel("Цвет разметки врагов", "Цвет визуальной разметки стартовой зоны вражеской команды. Разметка не имеет коллайдера.")]
    [SerializeField] private Color enemyZoneColor = new Color(0.72f, 0.3f, 0.18f, 0.65f);

    [Header("Создание в редакторе")]
    [InspectorLabel("Создавать карту, если её нет", "Если включено, карта автоматически создаётся один раз при открытии сцены в Unity Editor. Создание выполняется только в режиме редактирования, не во время игры.")]
    [SerializeField] private bool autoGenerateWhenMissing = true;


    /// <summary>
    /// Keeps NPC destinations and velocity inside the authored arena.
    /// This only steers existing Rigidbody2D objects; it creates nothing at runtime.
    /// </summary>
    public Vector2 ClampMovementDestination(Vector2 worldPosition, float padding = 0.65f)
    {
        Vector3 localPosition = transform.InverseTransformPoint(
            new Vector3(worldPosition.x, worldPosition.y, transform.position.z));

        float inset = Mathf.Max(0f, wallThickness * 0.5f) + Mathf.Max(0f, padding);
        float halfWidth = Mathf.Max(0.1f, width * 0.5f - inset);
        float halfHeight = Mathf.Max(0.1f, height * 0.5f - inset);

        localPosition.x = Mathf.Clamp(localPosition.x, -halfWidth, halfWidth);
        localPosition.y = Mathf.Clamp(localPosition.y, -halfHeight, halfHeight);

        Vector3 clampedWorld = transform.TransformPoint(localPosition);
        return new Vector2(clampedWorld.x, clampedWorld.y);
    }

    /// <summary>
    /// Prevents an NPC from crossing the safe inner edge of the arena.
    /// If it is already outside, it is steered back in without teleporting.
    /// </summary>
    public Vector2 ConstrainMovementVelocity(
        Vector2 worldPosition,
        Vector2 desiredVelocity,
        float maxSpeed,
        float deltaTime,
        float padding = 0.65f)
    {
        maxSpeed = Mathf.Max(0f, maxSpeed);
        if (maxSpeed <= 0f)
            return Vector2.zero;

        float step = Mathf.Max(0.0001f, deltaTime);
        Vector3 localPosition = transform.InverseTransformPoint(
            new Vector3(worldPosition.x, worldPosition.y, transform.position.z));
        Vector3 localVelocity = transform.InverseTransformVector(
            new Vector3(desiredVelocity.x, desiredVelocity.y, 0f));

        float inset = Mathf.Max(0f, wallThickness * 0.5f) + Mathf.Max(0f, padding);
        float halfWidth = Mathf.Max(0.1f, width * 0.5f - inset);
        float halfHeight = Mathf.Max(0.1f, height * 0.5f - inset);

        localVelocity.x = ConstrainAxis(localPosition.x, localVelocity.x, halfWidth, maxSpeed, step);
        localVelocity.y = ConstrainAxis(localPosition.y, localVelocity.y, halfHeight, maxSpeed, step);

        Vector3 worldVelocity = transform.TransformVector(localVelocity);
        return Vector2.ClampMagnitude(new Vector2(worldVelocity.x, worldVelocity.y), maxSpeed);
    }

    private static float ConstrainAxis(
        float position,
        float velocity,
        float boundary,
        float maxSpeed,
        float deltaTime)
    {
        if (position > boundary)
            return -Mathf.Min(maxSpeed, Mathf.Max(0.8f, (position - boundary) * 4f));

        if (position < -boundary)
            return Mathf.Min(maxSpeed, Mathf.Max(0.8f, (-boundary - position) * 4f));

        float predictedPosition = position + velocity * deltaTime;
        if (predictedPosition > boundary && velocity > 0f)
            return Mathf.Max(0f, (boundary - position) / deltaTime);

        if (predictedPosition < -boundary && velocity < 0f)
            return Mathf.Min(0f, (-boundary - position) / deltaTime);

        return velocity;
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        if (!autoGenerateWhenMissing || Application.isPlaying)
            return;

        EditorApplication.delayCall -= EnsureMapExists;
        EditorApplication.delayCall += EnsureMapExists;
    }

    private void OnDisable()
    {
        EditorApplication.delayCall -= EnsureMapExists;
    }

    private void EnsureMapExists()
    {
        if (this == null || !isActiveAndEnabled || Application.isPlaying)
            return;

        if (!gameObject.scene.IsValid() ||
            !gameObject.scene.isLoaded ||
            EditorSceneManager.IsPreviewScene(gameObject.scene) ||
            EditorUtility.IsPersistent(gameObject))
        {
            return;
        }

        if (transform.Find("Map") == null)
            CreateMapInEditor();
    }

    [ContextMenu("Создать или пересоздать статическую карту")]
    private void CreateMapInEditor()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Карта создаётся только в режиме редактирования Unity Editor.", this);
            return;
        }

        width = Mathf.Max(10f, width);
        height = Mathf.Max(8f, height);
        wallThickness = Mathf.Max(0.25f, wallThickness);

        Transform existingMap = transform.Find("Map");
        if (existingMap != null)
            Undo.DestroyObjectImmediate(existingMap.gameObject);

        GameObject map = new GameObject("Map");
        Undo.RegisterCreatedObjectUndo(map, "Создать статическую карту");
        map.transform.SetParent(transform, false);

        Sprite squareSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (squareSprite == null)
            squareSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");

        if (squareSprite == null)
            Debug.LogWarning("Не удалось получить встроенный спрайт Unity. Физические границы всё равно будут созданы.", this);

        CreateRectangle("Floor", Vector2.zero, new Vector2(width, height),
            floorColor, false, map.transform, squareSprite, -20);

        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float halfWall = wallThickness * 0.5f;

        // Четыре неподвижные BoxCollider2D образуют непрерывный периметр.
        CreateRectangle("Wall_Top",
            new Vector2(0f, halfHeight + halfWall),
            new Vector2(width + wallThickness * 2f, wallThickness),
            wallColor, true, map.transform, squareSprite, -15);

        CreateRectangle("Wall_Bottom",
            new Vector2(0f, -halfHeight - halfWall),
            new Vector2(width + wallThickness * 2f, wallThickness),
            wallColor, true, map.transform, squareSprite, -15);

        CreateRectangle("Wall_Left",
            new Vector2(-halfWidth - halfWall, 0f),
            new Vector2(wallThickness, height),
            wallColor, true, map.transform, squareSprite, -15);

        CreateRectangle("Wall_Right",
            new Vector2(halfWidth + halfWall, 0f),
            new Vector2(wallThickness, height),
            wallColor, true, map.transform, squareSprite, -15);

        CreateArenaMarkings(map.transform, squareSprite, halfWidth, halfHeight);

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("Статическая арена создана в сцене. Стены имеют физические коллайдеры; во время Play Mode объекты не создаются.", map);
    }

    private void CreateArenaMarkings(Transform parent, Sprite sprite, float halfWidth, float halfHeight)
    {
        float sideOffset = Mathf.Min(8f, halfWidth * 0.55f);
        float zoneHalfHeight = Mathf.Min(5.5f, halfHeight * 0.65f);
        Vector2 zoneSize = new Vector2(3.2f, zoneHalfHeight * 2f);

        // Разметка двух стартовых зон — это только рисунок на полу, не препятствия.
        CreateOutline("Player_Start_Zone", new Vector2(-sideOffset, 0f), zoneSize,
            playerZoneColor, parent, sprite);
        CreateOutline("Enemy_Start_Zone", new Vector2(sideOffset, 0f), zoneSize,
            enemyZoneColor, parent, sprite);

        // Центральная пунктирная линия.
        int dashCount = Mathf.Max(4, Mathf.FloorToInt(height / 1.7f));
        float dashSpacing = height / (dashCount + 1f);
        for (int i = 0; i < dashCount; i++)
        {
            float y = -halfHeight + dashSpacing * (i + 1);
            CreateRectangle("Center_Mark_" + i,
                new Vector2(0f, y),
                new Vector2(0.08f, 0.55f),
                new Color(0.8f, 0.82f, 0.76f, 0.28f),
                false, parent, sprite, -10);
        }

        // Небольшие предупредительные полосы возле углов.
        for (int i = 0; i < 4; i++)
        {
            float xSign = (i % 2 == 0) ? -1f : 1f;
            float ySign = (i < 2) ? 1f : -1f;
            float x = xSign * (halfWidth - 2.2f);
            float y = ySign * (halfHeight - 1.6f);

            CreateRectangle("Corner_Mark_" + i,
                new Vector2(x, y),
                new Vector2(1.2f, 0.12f),
                new Color(0.82f, 0.62f, 0.24f, 0.55f),
                false, parent, sprite, -10);
        }
    }

    private void CreateOutline(
        string objectName,
        Vector2 center,
        Vector2 size,
        Color color,
        Transform parent,
        Sprite sprite)
    {
        float line = 0.08f;
        CreateRectangle(objectName + "_Top",
            center + Vector2.up * (size.y * 0.5f),
            new Vector2(size.x, line), color, false, parent, sprite, -10);
        CreateRectangle(objectName + "_Bottom",
            center - Vector2.up * (size.y * 0.5f),
            new Vector2(size.x, line), color, false, parent, sprite, -10);
        CreateRectangle(objectName + "_Left",
            center - Vector2.right * (size.x * 0.5f),
            new Vector2(line, size.y), color, false, parent, sprite, -10);
        CreateRectangle(objectName + "_Right",
            center + Vector2.right * (size.x * 0.5f),
            new Vector2(line, size.y), color, false, parent, sprite, -10);
    }

    private static void CreateRectangle(
        string objectName,
        Vector2 position,
        Vector2 size,
        Color color,
        bool hasCollider,
        Transform parent,
        Sprite sprite,
        int sortingOrder)
    {
        GameObject obj = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(obj, "Создать объект карты");
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = size;

        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        if (hasCollider)
        {
            BoxCollider2D wallCollider = obj.AddComponent<BoxCollider2D>();
            wallCollider.isTrigger = false;
        }
    }
#endif
}
