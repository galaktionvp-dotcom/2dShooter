using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Инструмент для создания статического уровня в сцене редактора.
/// Во время игры объекты уровня не создаются.
/// </summary>
public class TopDownMapGenerator : MonoBehaviour
{
    [Header("Размер арены")]
    [Tooltip("Ширина игровой арены в единицах Unity.")]
    [SerializeField] private float width = 30f;

    [Tooltip("Высота игровой арены в единицах Unity.")]
    [SerializeField] private float height = 20f;

    [Tooltip("Толщина стен по краям арены.")]
    [SerializeField] private float wallThickness = 1f;

    [Header("Цвета")]
    [Tooltip("Цвет пола арены.")]
    [SerializeField] private Color floorColor = new Color(0.18f, 0.18f, 0.18f);

    [Tooltip("Цвет стен арены.")]
    [SerializeField] private Color wallColor = new Color(0.08f, 0.08f, 0.08f);

#if UNITY_EDITOR
    [ContextMenu("Создать статический уровень в сцене")]
    private void CreateMapInEditor()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Создание уровня доступно только в режиме редактирования сцены.", this);
            return;
        }

        Transform existingMap = transform.Find("Map");
        if (existingMap != null)
            Undo.DestroyObjectImmediate(existingMap.gameObject);

        GameObject map = new GameObject("Map");
        Undo.RegisterCreatedObjectUndo(map, "Создать уровень");
        map.transform.SetParent(transform, false);

        Sprite squareSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (squareSprite == null)
            squareSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");

        CreateRectangle("Floor", Vector2.zero, new Vector2(width, height), floorColor, false, map.transform, squareSprite);
        CreateRectangle("Wall_Top", new Vector2(0f, height * 0.5f + wallThickness * 0.5f), new Vector2(width + wallThickness * 2f, wallThickness), wallColor, true, map.transform, squareSprite);
        CreateRectangle("Wall_Bottom", new Vector2(0f, -height * 0.5f - wallThickness * 0.5f), new Vector2(width + wallThickness * 2f, wallThickness), wallColor, true, map.transform, squareSprite);
        CreateRectangle("Wall_Left", new Vector2(-width * 0.5f - wallThickness * 0.5f, 0f), new Vector2(wallThickness, height), wallColor, true, map.transform, squareSprite);
        CreateRectangle("Wall_Right", new Vector2(width * 0.5f + wallThickness * 0.5f, 0f), new Vector2(wallThickness, height), wallColor, true, map.transform, squareSprite);

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("Статический уровень создан в сцене. Во время запуска игры генерация не выполняется.", map);
    }

    private static void CreateRectangle(
        string objectName,
        Vector2 position,
        Vector2 size,
        Color color,
        bool hasCollider,
        Transform parent,
        Sprite sprite)
    {
        GameObject obj = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(obj, "Создать часть уровня");
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = size;

        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = -10;

        if (hasCollider)
            obj.AddComponent<BoxCollider2D>();
    }
#endif
}
