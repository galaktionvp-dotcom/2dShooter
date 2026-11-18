using InspectorLocalization;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth))]
public sealed class LootCollector : MonoBehaviour
{
    [Header("Сбор предметов")]
    [InspectorLabel("Радиус взаимодействия", "Максимальное расстояние от игрока до предмета, на котором появляется подсказка сбора.")]
    [SerializeField, Min(0.25f)] private float interactionRadius = 1.5f;

    [InspectorLabel("Время сбора", "Сколько секунд нужно непрерывно удерживать E для сбора одного предмета.")]
    [SerializeField, Min(0.1f)] private float collectionDuration = 2.5f;

    private PlayerHealth playerHealth;
    private LootPickup[] lootItems;
    private LootPickup currentTarget;
    private float collectionProgress;
    private bool mustReleaseKey;

    private int gazyavaCount;
    private int khrumkiCount;

    private GUIStyle headerStyle;
    private GUIStyle rowStyle;
    private GUIStyle promptStyle;
    private GUIStyle progressTextStyle;

    public int GazyavaCount => gazyavaCount;
    public int KhrumkiCount => khrumkiCount;
    public bool IsCollecting => currentTarget != null && collectionProgress > 0f;

    private void Awake()
    {
        interactionRadius = Mathf.Max(0.25f, interactionRadius);
        collectionDuration = Mathf.Max(0.1f, collectionDuration);

        playerHealth = GetComponent<PlayerHealth>();
        lootItems = FindObjectsByType<LootPickup>(FindObjectsSortMode.None);
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            CancelCollection();
            return;
        }

        bool isHoldingKey = Keyboard.current != null && Keyboard.current.eKey.isPressed;

        // Сбрасываем блокировку даже тогда, когда рядом сейчас нет предмета.
        if (!isHoldingKey)
        {
            collectionProgress = 0f;
            mustReleaseKey = false;
        }

        LootPickup nearestItem = FindNearestItem();

        if (nearestItem != currentTarget)
        {
            currentTarget = nearestItem;
            collectionProgress = 0f;
        }

        if (currentTarget == null)
        {
            collectionProgress = 0f;
            return;
        }

        if (!isHoldingKey)
            return;

        // После сбора нужно отпустить E, чтобы случайно не подобрать следующий предмет.
        if (mustReleaseKey)
            return;

        collectionProgress += Time.deltaTime;

        if (collectionProgress < collectionDuration)
            return;

        LootItemType collectedType = currentTarget.ItemType;
        if (currentTarget.TryCollect())
        {
            if (collectedType == LootItemType.Gazyava)
                gazyavaCount++;
            else
                khrumkiCount++;
        }

        currentTarget = null;
        collectionProgress = 0f;
        mustReleaseKey = true;
    }

    private LootPickup FindNearestItem()
    {
        if (lootItems == null || lootItems.Length == 0)
            return null;

        float maximumDistanceSquared = interactionRadius * interactionRadius;
        float closestDistanceSquared = maximumDistanceSquared;
        LootPickup closestItem = null;
        Vector2 playerPosition = transform.position;

        for (int i = 0; i < lootItems.Length; i++)
        {
            LootPickup item = lootItems[i];
            if (item == null || !item.isActiveAndEnabled || item.IsCollected)
                continue;

            float distanceSquared = ((Vector2)item.transform.position - playerPosition).sqrMagnitude;
            if (distanceSquared > closestDistanceSquared)
                continue;

            closestDistanceSquared = distanceSquared;
            closestItem = item;
        }

        return closestItem;
    }

    private void CancelCollection()
    {
        currentTarget = null;
        collectionProgress = 0f;
        mustReleaseKey = false;
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
            return;

        EnsureGuiStyles();
        DrawLootCounter();
        DrawCollectionPrompt();
    }

    private void EnsureGuiStyles()
    {
        if (headerStyle != null)
            return;

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        rowStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleLeft
        };

        promptStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };

        progressTextStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleRight
        };
    }

    private void DrawLootCounter()
    {
        const float panelWidth = 220f;
        const float panelHeight = 92f;
        const float padding = 12f;

        Rect panel = new Rect(Screen.width - panelWidth - 20f, 20f, panelWidth, panelHeight);
        Color previousColor = GUI.color;

        GUI.color = new Color(0.035f, 0.045f, 0.055f, 0.9f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.Label(new Rect(panel.x + padding, panel.y + 5f, panelWidth - padding * 2f, 22f),
            "СОБРАННАЯ ДОБЫЧА", headerStyle);

        GUI.Label(new Rect(panel.x + padding, panel.y + 31f, 112f, 22f),
            "ГАЗЯВА", rowStyle);
        GUI.Label(new Rect(panel.x + 130f, panel.y + 31f, 75f, 22f),
            gazyavaCount.ToString(), rowStyle);

        GUI.Label(new Rect(panel.x + padding, panel.y + 57f, 112f, 22f),
            "ХРУМКИ", rowStyle);
        GUI.Label(new Rect(panel.x + 130f, panel.y + 57f, 75f, 22f),
            khrumkiCount.ToString(), rowStyle);

        GUI.color = previousColor;
    }

    private void DrawCollectionPrompt()
    {
        if (currentTarget == null || !currentTarget.isActiveAndEnabled)
            return;

        const float panelWidth = 370f;
        const float panelHeight = 88f;
        const float barWidth = 320f;
        const float barHeight = 10f;

        float panelX = (Screen.width - panelWidth) * 0.5f;
        float panelY = Screen.height - panelHeight - 34f;
        Rect panel = new Rect(panelX, panelY, panelWidth, panelHeight);
        Color previousColor = GUI.color;

        GUI.color = new Color(0.035f, 0.045f, 0.055f, 0.92f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.Label(
            new Rect(panel.x + 12f, panel.y + 7f, panel.width - 24f, 28f),
            "УДЕРЖИВАЙ E — СОБРАТЬ " + currentTarget.DisplayName.ToUpperInvariant(),
            promptStyle);

        Rect barBackground = new Rect(
            panel.x + (panelWidth - barWidth) * 0.5f,
            panel.y + 47f,
            barWidth,
            barHeight);

        GUI.color = new Color(0.14f, 0.17f, 0.19f, 1f);
        GUI.DrawTexture(barBackground, Texture2D.whiteTexture);

        float progressRatio = Mathf.Clamp01(collectionProgress / collectionDuration);
        Rect barFill = new Rect(
            barBackground.x,
            barBackground.y,
            barBackground.width * progressRatio,
            barBackground.height);

        GUI.color = currentTarget.ItemType == LootItemType.Gazyava
            ? new Color(0.15f, 0.78f, 0.92f, 1f)
            : new Color(1f, 0.62f, 0.16f, 1f);
        GUI.DrawTexture(barFill, Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.Label(
            new Rect(panel.x + 12f, panel.y + 61f, panel.width - 24f, 18f),
            collectionProgress.ToString("0.0") + " / " + collectionDuration.ToString("0.0") + " сек.",
            progressTextStyle);

        GUI.color = previousColor;
    }
}
