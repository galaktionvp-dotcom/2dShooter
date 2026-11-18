using InspectorLocalization;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Размещает статические предметы добычи в сцене только в Unity Editor.
/// Во время Play Mode и в собранной игре новые объекты не создаются.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class LootSceneSetup : MonoBehaviour
{
    [Header("Создание предметов в редакторе")]
    [InspectorLabel("Создавать добычу, если её нет", "При открытии сцены один раз создаёт статичные предметы добычи в Unity Editor. Во время игры объекты не создаются.")]
    [SerializeField] private bool autoGenerateWhenMissing = true;

    [InspectorLabel("Позиции газявы", "Мировые координаты трёх предметов «Газява» на карте. Все точки должны находиться внутри границ арены.")]
    [SerializeField] private Vector2[] gazyavaPositions =
    {
        new Vector2(-10f, 6f),
        new Vector2(-10f, -6f),
        new Vector2(-5f, 6f)
    };

    [InspectorLabel("Позиции хрумков", "Мировые координаты трёх предметов «Хрумки» на карте. Все точки должны находиться внутри границ арены.")]
    [SerializeField] private Vector2[] khrumkiPositions =
    {
        new Vector2(10f, 6f),
        new Vector2(10f, -6f),
        new Vector2(5f, -6f)
    };

    [Header("Внешний вид добычи")]
    [InspectorLabel("Цвет газявы", "Основной бирюзовый цвет банки газявы.")]
    [SerializeField] private Color gazyavaColor = new Color(0.1f, 0.72f, 0.88f, 1f);

    [InspectorLabel("Цвет упаковки хрумков", "Основной оранжевый цвет упаковки хрумков.")]
    [SerializeField] private Color khrumkiColor = new Color(0.95f, 0.48f, 0.12f, 1f);

#if UNITY_EDITOR
    private void OnEnable()
    {
        if (!autoGenerateWhenMissing || Application.isPlaying)
            return;

        EditorApplication.delayCall -= EnsureLootItems;
        EditorApplication.delayCall += EnsureLootItems;
    }

    private void OnDisable()
    {
        EditorApplication.delayCall -= EnsureLootItems;
    }

    private void EnsureLootItems()
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

        if (transform.Find("LootItems") != null)
            return;

        CreateLootItems();
    }

    [ContextMenu("Создать статичные предметы добычи")]
    private void CreateLootItems()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Предметы добычи можно размещать только в Unity Editor, не во время игры.", this);
            return;
        }

        if (transform.Find("LootItems") != null)
        {
            Debug.Log("Контейнер LootItems уже существует. Удалите его в Hierarchy, если нужно создать предметы заново.", this);
            return;
        }

        Sprite itemSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (itemSprite == null)
            itemSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");

        if (itemSprite == null)
        {
            Debug.LogError("Не удалось получить встроенный спрайт Unity. Предметы добычи не были созданы.", this);
            return;
        }

        GameObject root = new GameObject("LootItems");
        Undo.RegisterCreatedObjectUndo(root, "Создать контейнер добычи");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;

        if (gazyavaPositions != null)
        {
            for (int i = 0; i < gazyavaPositions.Length; i++)
                CreateGazyava(root.transform, itemSprite, gazyavaPositions[i], i + 1);
        }

        if (khrumkiPositions != null)
        {
            for (int i = 0; i < khrumkiPositions.Length; i++)
                CreateKhrumki(root.transform, itemSprite, khrumkiPositions[i], i + 1);
        }

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("На сцене размещены статичные предметы: " +
                  (gazyavaPositions != null ? gazyavaPositions.Length : 0) + " газявы и " +
                  (khrumkiPositions != null ? khrumkiPositions.Length : 0) + " хрумков. Сохраните сцену (Ctrl+S).", root);
    }

    private void CreateGazyava(Transform parent, Sprite sprite, Vector2 position, int number)
    {
        GameObject item = CreatePickupObject(
            parent, "Газява_" + number, LootItemType.Gazyava, position, 0.7f);

        CreateVisualPart(item.transform, "Банка", sprite, Vector2.zero,
            new Vector2(0.34f, 0.5f), gazyavaColor, 4);
        CreateVisualPart(item.transform, "Крышка", sprite, new Vector2(0f, 0.27f),
            new Vector2(0.36f, 0.07f), new Color(0.78f, 0.84f, 0.87f, 1f), 5);
        CreateVisualPart(item.transform, "Этикетка", sprite, new Vector2(0f, -0.02f),
            new Vector2(0.3f, 0.15f), new Color(0.9f, 0.97f, 1f, 1f), 5);
        CreateVisualPart(item.transform, "Полоса этикетки", sprite, new Vector2(0.07f, -0.02f),
            new Vector2(0.045f, 0.15f), new Color(0.95f, 0.22f, 0.18f, 1f), 6);
    }

    private void CreateKhrumki(Transform parent, Sprite sprite, Vector2 position, int number)
    {
        GameObject item = CreatePickupObject(
            parent, "Хрумки_" + number, LootItemType.Khrumki, position, 0.7f);

        CreateVisualPart(item.transform, "Пакет", sprite, Vector2.zero,
            new Vector2(0.52f, 0.42f), khrumkiColor, 4);
        CreateVisualPart(item.transform, "Верх упаковки", sprite, new Vector2(0f, 0.21f),
            new Vector2(0.44f, 0.08f), new Color(0.58f, 0.22f, 0.08f, 1f), 5);
        CreateVisualPart(item.transform, "Этикетка", sprite, new Vector2(0f, -0.01f),
            new Vector2(0.31f, 0.2f), new Color(1f, 0.84f, 0.38f, 1f), 5);
        CreateVisualPart(item.transform, "Чипсы", sprite, new Vector2(0f, -0.01f),
            new Vector2(0.08f, 0.12f), new Color(1f, 0.98f, 0.8f, 1f), 6);
    }

    private static GameObject CreatePickupObject(
        Transform parent,
        string objectName,
        LootItemType itemType,
        Vector2 position,
        float interactionColliderRadius)
    {
        GameObject item = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(item, "Создать предмет добычи");
        item.transform.SetParent(parent, false);
        item.transform.localPosition = position;

        LootPickup pickup = Undo.AddComponent<LootPickup>(item);
        pickup.Configure(itemType);
        EditorUtility.SetDirty(pickup);

        // LootPickup требует CircleCollider2D, поэтому Unity может добавить его
        // автоматически при добавлении компонента. Используем этот коллайдер,
        // чтобы не создавать второй.
        CircleCollider2D pickupCollider = item.GetComponent<CircleCollider2D>();
        if (pickupCollider == null)
            pickupCollider = Undo.AddComponent<CircleCollider2D>(item);

        pickupCollider.isTrigger = true;
        pickupCollider.radius = interactionColliderRadius;

        return item;
    }

    private static void CreateVisualPart(
        Transform parent,
        string objectName,
        Sprite sprite,
        Vector2 localPosition,
        Vector2 localScale,
        Color color,
        int sortingOrder)
    {
        GameObject visual = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(visual, "Создать изображение предмета");
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = localPosition;
        visual.transform.localScale = new Vector3(localScale.x, localScale.y, 1f);

        SpriteRenderer renderer = Undo.AddComponent<SpriteRenderer>(visual);
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
    }
#endif
}
