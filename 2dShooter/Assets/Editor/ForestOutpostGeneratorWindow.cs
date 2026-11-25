using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// One-click, editor-only generator for the forest outpost level.
/// Generated objects are saved into the open scene and are not created at runtime.
/// </summary>
public sealed class ForestOutpostGeneratorWindow : EditorWindow
{
    private const string PackRoot = "Assets/Cainos/Pixel Art Top Down - Basic/";
    private const string GeneratedMapName = "Map";

    [SerializeField] private int mapWidth = 30;
    [SerializeField] private int mapHeight = 20;
    [SerializeField] private int randomSeed = 25112026;
    [SerializeField, Range(0f, 1f)] private float forestDensity = 0.72f;
    [SerializeField] private bool moveExistingTeams = true;

    private Vector2 scroll;

    [MenuItem("Tools/2dShooter/Генератор лесной военной базы")]
    public static void OpenWindow()
    {
        ForestOutpostGeneratorWindow window = GetWindow<ForestOutpostGeneratorWindow>("Лесная база");
        window.minSize = new Vector2(360f, 430f);
        window.Show();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Лесная армейская часть", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Генератор использует Cainos Pixel Art Top Down - Basic. " +
            "Он создаёт статические Tilemap-слои, лес, дорожки, пропускной пункт, складские зоны и физические границы.",
            MessageType.Info);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Размер и оформление", EditorStyles.boldLabel);
        mapWidth = EditorGUILayout.IntSlider("Ширина карты", mapWidth, 24, 40);
        mapHeight = EditorGUILayout.IntSlider("Высота карты", mapHeight, 16, 30);
        forestDensity = EditorGUILayout.Slider("Плотность леса", forestDensity, 0.25f, 1f);
        randomSeed = EditorGUILayout.IntField("Сид генерации", randomSeed);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Игровые объекты сцены", EditorStyles.boldLabel);
        moveExistingTeams = EditorGUILayout.Toggle(
            new GUIContent("Расставить существующие команды", "Переместить player, союзников и врагов в свободные стартовые зоны. Сами объекты и их компоненты сохраняются."),
            moveExistingTeams);

        EditorGUILayout.HelpBox(
            "При повторной генерации очищается только содержимое ArenaMap/Map. " +
            "Игрок, союзники, враги, камера и LootItems находятся вне этой папки и не удаляются.",
            MessageType.Warning);

        EditorGUILayout.Space(10f);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("СГЕНЕРИРОВАТЬ УРОВЕНЬ", GUILayout.Height(38f)))
                GenerateLevel();
        }

        if (EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("Останови Play Mode, чтобы создать или пересоздать уровень.", MessageType.Warning);

        EditorGUILayout.EndScrollView();
    }

    private void GenerateLevel()
    {
        if (EditorApplication.isPlaying)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
        {
            EditorUtility.DisplayDialog("Генератор базы", "Открой обычную игровую сцену перед генерацией.", "Понятно");
            return;
        }

        mapWidth = Mathf.Clamp(mapWidth, 24, 40);
        mapHeight = Mathf.Clamp(mapHeight, 16, 30);

        Tile grassTile = LoadTile("Tile Palette/TP Grass/TX Tileset Grass 0.asset");
        Tile pavementTile = LoadTile("Tile Palette/TP Grass/TX Tileset Grass Pavement 0.asset");
        Tile pavementVariant = LoadTile("Tile Palette/TP Grass/TX Tileset Grass Pavement 1.asset");
        Tile stoneTile = LoadTile("Tile Palette/TP Stone Ground/TX Tileset Stone Ground_0.asset");
        Tile grassFlowerTile = LoadTile("Tile Palette/TP Grass/TX Tileset Grass Flower 0.asset");

        GameObject tree01 = LoadPrefab("Plant/PF Plant - Tree 01.prefab");
        GameObject tree02 = LoadPrefab("Plant/PF Plant - Tree 02.prefab");
        GameObject tree03 = LoadPrefab("Plant/PF Plant - Tree 03.prefab");
        GameObject bush01 = LoadPrefab("Plant/PF Plant - Bush 01.prefab");
        GameObject bush02 = LoadPrefab("Plant/PF Plant - Bush 02.prefab");
        GameObject crate01 = LoadPrefab("Props/PF Props - Crate 01.prefab");
        GameObject crate02 = LoadPrefab("Props/PF Props - Crate 02.prefab");
        GameObject barrel = LoadPrefab("Props/PF Props - Barrel 01.prefab");
        GameObject gate = LoadPrefab("Props/PF Props - Wooden Gate 01.prefab");
        GameObject sign = LoadPrefab("Props/PF Props - Road Sign 01 E.prefab");
        GameObject bench = LoadPrefab("Props/PF Props - Stone Bench 01 S.prefab");
        GameObject stone01 = LoadPrefab("Props/PF Props - Stone 01.prefab");
        GameObject chest = LoadPrefab("Props/PF Props - Chest 01.prefab");

        if (grassTile == null || pavementTile == null || stoneTile == null ||
            tree01 == null || crate01 == null || barrel == null || gate == null)
        {
            EditorUtility.DisplayDialog(
                "Не найдены ресурсы Cainos",
                "Не удалось найти обязательные Tile или Prefab. Проверь, что папка Assets/Cainos/Pixel Art Top Down - Basic импортирована целиком и Unity закончила импорт.",
                "Понятно");
            return;
        }

        GameObject arena = GameObject.Find("ArenaMap");
        if (arena == null)
        {
            arena = new GameObject("ArenaMap");
            Undo.RegisterCreatedObjectUndo(arena, "Создать корень карты");
            SceneManager.MoveGameObjectToScene(arena, scene);
        }

        Transform mapRoot = arena.transform.Find(GeneratedMapName);
        if (mapRoot != null && mapRoot.childCount > 0)
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Пересоздать карту?",
                "Содержимое ArenaMap/Map будет заменено новой лесной локацией. Объекты за пределами Map не затрагиваются.",
                "Пересоздать",
                "Отмена");
            if (!confirmed)
                return;

            Undo.RegisterFullObjectHierarchyUndo(mapRoot.gameObject, "Очистить старую карту");
            for (int i = mapRoot.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(mapRoot.GetChild(i).gameObject);
        }

        if (mapRoot == null)
        {
            GameObject mapObject = new GameObject(GeneratedMapName);
            Undo.RegisterCreatedObjectUndo(mapObject, "Создать корень карты");
            mapObject.transform.SetParent(arena.transform, false);
            mapRoot = mapObject.transform;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Создать лесную военную базу");

        UnityEngine.Random.State previousRandomState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(randomSeed);

        try
        {
            BuildTerrain(mapRoot, grassTile, pavementTile, pavementVariant, stoneTile, grassFlowerTile);
            BuildForest(mapRoot, tree01, tree02, tree03, bush01, bush02);
            BuildOutpost(mapRoot, crate01, crate02, barrel, gate, sign, bench, stone01, chest);
            BuildWorldBounds(mapRoot);

            if (moveExistingTeams)
                PlaceExistingTeams(scene);

            UpdateCameraBounds(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = mapRoot.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();

            Debug.Log("Лесная военная база создана. Все объекты сохранены в сцене; генерация во время Play Mode не используется.", mapRoot);
            EditorUtility.DisplayDialog(
                "Уровень создан",
                "Лесная военная база готова. Сохрани сцену сочетанием Ctrl+S. Генератор можно закрыть — уровень уже записан в сцену.",
                "Отлично");
        }
        finally
        {
            UnityEngine.Random.state = previousRandomState;
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static Tile LoadTile(string relativePath)
    {
        return AssetDatabase.LoadAssetAtPath<Tile>(PackRoot + relativePath);
    }

    private static GameObject LoadPrefab(string relativePath)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(PackRoot + "Prefab/" + relativePath);
    }

    private void BuildTerrain(
        Transform parent,
        Tile grass,
        Tile pavement,
        Tile pavementAlt,
        Tile stone,
        Tile grassFlower)
    {
        Tilemap ground = CreateTilemap(parent, "Покрытие — трава", -30);
        Tilemap paths = CreateTilemap(parent, "Дороги — утоптанная земля", -25);
        Tilemap courtyard = CreateTilemap(parent, "Площадки — камень", -24);
        Tilemap details = CreateTilemap(parent, "Мелкие детали травы", -20);

        int minX = -mapWidth / 2;
        int maxX = minX + mapWidth - 1;
        int minY = -mapHeight / 2;
        int maxY = minY + mapHeight - 1;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                ground.SetTile(cell, grass);

                // The central lane and the north branch connect the forest edge to the outpost yard.
                bool mainRoad = Mathf.Abs(y) <= 1;
                bool northBranch = x >= -1 && x <= 1 && y >= 2 && y <= 7;
                if (mainRoad || northBranch)
                {
                    Tile roadTile = pavementAlt != null && ((x + y) & 1) == 0 ? pavementAlt : pavement;
                    paths.SetTile(cell, roadTile);
                }

                // Stone pads mark the small service yard and the supply depot.
                bool commandYard = x >= -4 && x <= 4 && y >= 5 && y <= 8;
                bool supplyYard = x >= 5 && x <= 9 && y >= -7 && y <= -3;
                if (commandYard || supplyYard)
                    courtyard.SetTile(cell, stone);

                bool outsidePlayableLanes =
                    Mathf.Abs(y) > 2 &&
                    !(x >= -4 && x <= 4 && y >= 4 && y <= 9) &&
                    !(x >= 5 && x <= 9 && y >= -8 && y <= -2);

                if (grassFlower != null && outsidePlayableLanes && UnityEngine.Random.value < 0.018f)
                    details.SetTile(cell, grassFlower);
            }
        }
    }

    private Tilemap CreateTilemap(Transform parent, string objectName, int sortingOrder)
    {
        GameObject gridObject = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(gridObject, "Создать слой ландшафта");
        gridObject.transform.SetParent(parent, false);
        Grid grid = gridObject.AddComponent<Grid>();
        grid.cellSize = Vector3.one;

        GameObject layerObject = new GameObject("Tilemap");
        Undo.RegisterCreatedObjectUndo(layerObject, "Создать Tilemap");
        layerObject.transform.SetParent(gridObject.transform, false);

        Tilemap tilemap = layerObject.AddComponent<Tilemap>();
        tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
        TilemapRenderer renderer = layerObject.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = sortingOrder;
        renderer.mode = TilemapRenderer.Mode.Chunk;
        return tilemap;
    }

    private void BuildForest(
        Transform parent,
        GameObject tree01,
        GameObject tree02,
        GameObject tree03,
        GameObject bush01,
        GameObject bush02)
    {
        Transform trees = CreateGroup(parent, "Лесной периметр и деревья");
        Transform bushes = CreateGroup(parent, "Кусты и подлесок");

        float halfWidth = mapWidth * 0.5f;
        float halfHeight = mapHeight * 0.5f;

        // Outer tree belt, with a clear northern entrance.
        for (float x = -halfWidth + 1.1f; x <= halfWidth - 1f; x += 2.7f)
        {
            if (Mathf.Abs(x) > 1.8f)
            {
                PlacePrefab(PickTree(tree01, tree02, tree03), trees, new Vector2(x, halfHeight - 0.65f), 0f, 1f);
                PlacePrefab(PickTree(tree01, tree02, tree03), trees, new Vector2(x, -halfHeight + 0.65f), 0f, 1f);
            }
        }

        for (float y = -halfHeight + 1.2f; y <= halfHeight - 1f; y += 2.7f)
        {
            PlacePrefab(PickTree(tree01, tree02, tree03), trees, new Vector2(-halfWidth + 0.65f, y), 0f, 1f);
            PlacePrefab(PickTree(tree01, tree02, tree03), trees, new Vector2(halfWidth - 0.65f, y), 0f, 1f);
        }

        int treeAttempts = Mathf.RoundToInt(26f * forestDensity);
        int bushAttempts = Mathf.RoundToInt(54f * forestDensity);

        for (int i = 0; i < treeAttempts; i++)
        {
            Vector2 position = new Vector2(
                UnityEngine.Random.Range(-halfWidth + 2f, halfWidth - 2f),
                UnityEngine.Random.Range(-halfHeight + 2f, halfHeight - 2f));

            if (!IsForestEdge(position) || IsClearZone(position))
                continue;

            PlacePrefab(PickTree(tree01, tree02, tree03), trees, position, 0f,
                UnityEngine.Random.Range(0.88f, 1.08f));
        }

        for (int i = 0; i < bushAttempts; i++)
        {
            Vector2 position = new Vector2(
                UnityEngine.Random.Range(-halfWidth + 1.5f, halfWidth - 1.5f),
                UnityEngine.Random.Range(-halfHeight + 1.5f, halfHeight - 1.5f));

            if (!IsForestEdge(position) || IsClearZone(position))
                continue;

            GameObject bush = UnityEngine.Random.value < 0.5f || bush02 == null ? bush01 : bush02;
            if (bush != null)
                PlacePrefab(bush, bushes, position, 0f, UnityEngine.Random.Range(0.85f, 1.12f));
        }
    }

    private bool IsForestEdge(Vector2 position)
    {
        float halfWidth = mapWidth * 0.5f;
        float halfHeight = mapHeight * 0.5f;
        return Mathf.Abs(position.x) > halfWidth * 0.30f ||
               Mathf.Abs(position.y) > halfHeight * 0.34f;
    }

    private static bool IsClearZone(Vector2 position)
    {
        // Keep the cross-map road, both deployment areas and the outpost courtyard open.
        bool centralRoad = Mathf.Abs(position.y) < 1.8f;
        bool playerSpawn = position.x < -8.7f && Mathf.Abs(position.y) < 3.0f;
        bool enemySpawn = position.x > 8.7f && Mathf.Abs(position.y) < 3.0f;
        bool checkpoint = position.x > -5f && position.x < 5f && position.y > 4f && position.y < 9f;
        bool supplyDepot = position.x > 4f && position.x < 10f && position.y > -8f && position.y < -2f;
        return centralRoad || playerSpawn || enemySpawn || checkpoint || supplyDepot;
    }

    private static GameObject PickTree(GameObject tree01, GameObject tree02, GameObject tree03)
    {
        int choice = UnityEngine.Random.Range(0, 3);
        if (choice == 1 && tree02 != null) return tree02;
        if (choice == 2 && tree03 != null) return tree03;
        return tree01;
    }

    private void BuildOutpost(
        Transform parent,
        GameObject crate01,
        GameObject crate02,
        GameObject barrel,
        GameObject gate,
        GameObject sign,
        GameObject bench,
        GameObject stone01,
        GameObject chest)
    {
        Transform checkpoint = CreateGroup(parent, "КПП — северный въезд");
        Transform barracks = CreateGroup(parent, "Казарменный двор");
        Transform supply = CreateGroup(parent, "Склад припасов");
        Transform cover = CreateGroup(parent, "Камни и укрытия");

        PlacePrefab(gate, checkpoint, new Vector2(0f, 8.25f), 0f, 1f);
        if (sign != null)
            PlacePrefab(sign, checkpoint, new Vector2(-2.4f, 7.15f), 0f, 1f);
        if (bench != null)
            PlacePrefab(bench, checkpoint, new Vector2(2.5f, 7.15f), 0f, 0.8f);

        // A compact crate enclosure gives the barracks yard visible, collidable sides.
        PlacePrefab(crate01, barracks, new Vector2(-8.2f, 6.2f), 0f, 1f);
        PlacePrefab(crate02 != null ? crate02 : crate01, barracks, new Vector2(-7.0f, 6.2f), 0f, 1f);
        PlacePrefab(crate01, barracks, new Vector2(-5.8f, 6.2f), 0f, 1f);
        PlacePrefab(crate02 != null ? crate02 : crate01, barracks, new Vector2(-8.2f, 5.1f), 0f, 1f);
        PlacePrefab(crate01, barracks, new Vector2(-5.8f, 5.1f), 0f, 1f);
        PlacePrefab(crate01, barracks, new Vector2(-8.2f, 4.0f), 0f, 1f);
        PlacePrefab(crate02 != null ? crate02 : crate01, barracks, new Vector2(-5.8f, 4.0f), 0f, 1f);
        PlacePrefab(barrel, barracks, new Vector2(-7.5f, 4.8f), 0f, 0.9f);
        if (bench != null)
            PlacePrefab(bench, barracks, new Vector2(-6.2f, 4.8f), 90f, 0.75f);

        PlacePrefab(crate01, supply, new Vector2(6.0f, -4.2f), 0f, 1f);
        if (crate02 != null)
            PlacePrefab(crate02, supply, new Vector2(7.15f, -4.2f), 0f, 1f);
        PlacePrefab(barrel, supply, new Vector2(8.2f, -5.1f), 0f, 0.9f);
        PlacePrefab(barrel, supply, new Vector2(5.7f, -5.7f), 0f, 0.9f);
        if (chest != null)
            PlacePrefab(chest, supply, new Vector2(7.25f, -6.0f), 0f, 0.9f);

        if (stone01 != null)
        {
            PlacePrefab(stone01, cover, new Vector2(-4.8f, -4.0f), 0f, 0.95f);
            PlacePrefab(stone01, cover, new Vector2(-3.5f, -4.5f), 0f, 0.8f);
            PlacePrefab(stone01, cover, new Vector2(4.6f, 3.0f), 0f, 0.9f);
            PlacePrefab(stone01, cover, new Vector2(5.4f, 2.2f), 0f, 0.8f);
            PlacePrefab(stone01, cover, new Vector2(-9.0f, -5.6f), 0f, 1f);
            PlacePrefab(stone01, cover, new Vector2(10.0f, 5.8f), 0f, 1f);
        }
    }

    private void BuildWorldBounds(Transform parent)
    {
        Transform bounds = CreateGroup(parent, "Физические границы уровня");
        float halfWidth = mapWidth * 0.5f;
        float halfHeight = mapHeight * 0.5f;
        float thickness = 0.7f;

        CreateBoundary(bounds, "Северная граница", new Vector2(0f, halfHeight), new Vector2(mapWidth, thickness));
        CreateBoundary(bounds, "Южная граница", new Vector2(0f, -halfHeight), new Vector2(mapWidth, thickness));
        CreateBoundary(bounds, "Западная граница", new Vector2(-halfWidth, 0f), new Vector2(thickness, mapHeight));
        CreateBoundary(bounds, "Восточная граница", new Vector2(halfWidth, 0f), new Vector2(thickness, mapHeight));
    }

    private static void CreateBoundary(Transform parent, string objectName, Vector2 position, Vector2 size)
    {
        GameObject wall = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(wall, "Создать границу уровня");
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = position;

        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.isTrigger = false;
    }

    private static Transform CreateGroup(Transform parent, string objectName)
    {
        GameObject group = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(group, "Создать группу объектов уровня");
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    private static void PlacePrefab(
        GameObject prefab,
        Transform parent,
        Vector2 position,
        float rotation,
        float scale)
    {
        if (prefab == null)
            return;

        Scene destinationScene = parent.gameObject.scene;
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, destinationScene) as GameObject;
        if (instance == null)
            return;

        Undo.RegisterCreatedObjectUndo(instance, "Разместить объект Cainos");
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = new Vector3(position.x, position.y, 0f);
        instance.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        instance.transform.localScale = Vector3.one * scale;

        int depthOffset = -Mathf.RoundToInt(position.y * 10f);
        foreach (SpriteRenderer renderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
            renderer.sortingOrder += depthOffset;
    }

    private void UpdateCameraBounds(Scene scene)
    {
        Vector2 minimum = new Vector2(-mapWidth * 0.5f, -mapHeight * 0.5f);
        Vector2 maximum = new Vector2(mapWidth * 0.5f, mapHeight * 0.5f);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!string.Equals(root.name, "Main Camera", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (MonoBehaviour component in root.GetComponents<MonoBehaviour>())
            {
                if (component == null)
                    continue;

                SerializedObject serializedComponent = new SerializedObject(component);
                SerializedProperty boundsMin = serializedComponent.FindProperty("boundsMin");
                SerializedProperty boundsMax = serializedComponent.FindProperty("boundsMax");

                // Update only the project's CameraFollow component; unrelated camera scripts are left untouched.
                if (boundsMin == null || boundsMax == null ||
                    boundsMin.propertyType != SerializedPropertyType.Vector2 ||
                    boundsMax.propertyType != SerializedPropertyType.Vector2)
                    continue;

                boundsMin.vector2Value = minimum;
                boundsMax.vector2Value = maximum;
                serializedComponent.ApplyModifiedProperties();
                EditorUtility.SetDirty(component);
            }
        }
    }

    private static void PlaceExistingTeams(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        GameObject player = null;
        GameObject mainEnemy = null;
        List<GameObject> allies = new List<GameObject>();
        List<GameObject> enemySoldiers = new List<GameObject>();

        foreach (GameObject root in roots)
        {
            if (string.Equals(root.name, "player", StringComparison.OrdinalIgnoreCase))
                player = root;
            else if (string.Equals(root.name, "Enemy", StringComparison.OrdinalIgnoreCase))
                mainEnemy = root;
            else if (root.name.IndexOf("Союзник", StringComparison.OrdinalIgnoreCase) >= 0)
                allies.Add(root);
            else if (root.name.IndexOf("Вражеский боец", StringComparison.OrdinalIgnoreCase) >= 0)
                enemySoldiers.Add(root);
        }

        if (player != null)
            player.transform.position = new Vector3(-11f, 0f, player.transform.position.z);

        if (allies.Count > 0)
            allies[0].transform.position = new Vector3(-12f, 1.6f, allies[0].transform.position.z);
        if (allies.Count > 1)
            allies[1].transform.position = new Vector3(-12f, -1.6f, allies[1].transform.position.z);

        if (mainEnemy != null)
            mainEnemy.transform.position = new Vector3(10.5f, 0f, mainEnemy.transform.position.z);
        if (enemySoldiers.Count > 0)
            enemySoldiers[0].transform.position = new Vector3(12f, 1.7f, enemySoldiers[0].transform.position.z);
        if (enemySoldiers.Count > 1)
            enemySoldiers[1].transform.position = new Vector3(12f, -1.7f, enemySoldiers[1].transform.position.z);

        Physics2D.SyncTransforms();
    }
}
