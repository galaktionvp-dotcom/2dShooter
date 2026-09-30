using UnityEngine;

public class TopDownMapGenerator : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateAtRuntime()
    {
        GameObject mapGeneratorObject = new GameObject("TopDownMapGenerator");
        mapGeneratorObject.AddComponent<TopDownMapGenerator>();
    }
    [Header("Arena Size")]
    [SerializeField] private float width = 30f;
    [SerializeField] private float height = 20f;
    [SerializeField] private float wallThickness = 1f;

    [Header("Colors")]
    [SerializeField] private Color floorColor = new Color(0.18f, 0.18f, 0.18f);
    [SerializeField] private Color wallColor = new Color(0.08f, 0.08f, 0.08f);

    private void Awake()
    {
        CreateMap();
    }

    private void CreateMap()
    {
        Transform existingMap = transform.Find("Map");
        if (existingMap != null)
            Destroy(existingMap.gameObject);

        GameObject map = new GameObject("Map");
        map.transform.SetParent(transform);

        CreateRectangle(
            "Floor",
            Vector2.zero,
            new Vector2(width, height),
            floorColor,
            false,
            map.transform
        );

        CreateRectangle(
            "Wall_Top",
            new Vector2(0f, height * 0.5f + wallThickness * 0.5f),
            new Vector2(width + wallThickness * 2f, wallThickness),
            wallColor,
            true,
            map.transform
        );

        CreateRectangle(
            "Wall_Bottom",
            new Vector2(0f, -height * 0.5f - wallThickness * 0.5f),
            new Vector2(width + wallThickness * 2f, wallThickness),
            wallColor,
            true,
            map.transform
        );

        CreateRectangle(
            "Wall_Left",
            new Vector2(-width * 0.5f - wallThickness * 0.5f, 0f),
            new Vector2(wallThickness, height),
            wallColor,
            true,
            map.transform
        );

        CreateRectangle(
            "Wall_Right",
            new Vector2(width * 0.5f + wallThickness * 0.5f, 0f),
            new Vector2(wallThickness, height),
            wallColor,
            true,
            map.transform
        );
    }

    private void CreateRectangle(
        string objectName,
        Vector2 position,
        Vector2 size,
        Color color,
        bool hasCollider,
        Transform parent)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(parent);
        obj.transform.position = position;
        obj.transform.localScale = size;

        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateWhiteSprite();
        renderer.color = color;

        if (hasCollider)
        {
            BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
        }
    }

    private Sprite CreateWhiteSprite()
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.name = "GeneratedWhiteTexture";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f
        );
    }
}
