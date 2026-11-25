using InspectorLocalization;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth), typeof(LootCollector))]
public sealed class RaidExtractionPoint : MonoBehaviour
{
    [Header("Точка эвакуации")]
    [InspectorLabel("Объект зоны эвакуации", "Ссылка на заранее размещённый объект зоны эвакуации на карте. Во время игры новые объекты не создаются.")]
    [SerializeField] private Transform extractionPoint;

    [InspectorLabel("Радиус зоны", "На каком расстоянии от центра зоны игрок может начать эвакуацию.")]
    [SerializeField, Min(0.25f)] private float extractionRadius = 1.25f;

    [InspectorLabel("Время эвакуации", "Сколько секунд нужно непрерывно удерживать E, чтобы завершить рейд.")]
    [SerializeField, Min(0.5f)] private float extractionDuration = 5f;

    [InspectorLabel("Минимум добычи", "Сколько предметов нужно собрать, чтобы эвакуация стала доступна.")]
    [SerializeField, Min(1)] private int minimumLootCount = 1;

    private PlayerHealth playerHealth;
    private LootCollector lootCollector;
    private PlayerMovement playerMovement;
    private PlayerShooting playerShooting;

    private float progress;
    private float previousHealth;
    private bool isInsideRange;
    private bool isExtracted;

    private GUIStyle promptStyle;
    private GUIStyle detailStyle;
    private GUIStyle resultTitleStyle;

    public bool IsExtracted => isExtracted;
    public float ExtractionProgress => extractionDuration <= 0f ? 1f : Mathf.Clamp01(progress / extractionDuration);

    private void Awake()
    {
        extractionRadius = Mathf.Max(0.25f, extractionRadius);
        extractionDuration = Mathf.Max(0.5f, extractionDuration);
        minimumLootCount = Mathf.Max(1, minimumLootCount);

        playerHealth = GetComponent<PlayerHealth>();
        lootCollector = GetComponent<LootCollector>();
        playerMovement = GetComponent<PlayerMovement>();
        playerShooting = GetComponent<PlayerShooting>();

        previousHealth = playerHealth != null ? playerHealth.CurrentHealth : 0f;

        if (extractionPoint == null)
        {
            Debug.LogError("Для RaidExtractionPoint не назначена точка эвакуации. Укажи объект Extraction_Zone в Inspector.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (isExtracted || playerHealth == null || lootCollector == null || extractionPoint == null)
            return;

        if (playerHealth.IsDead)
        {
            progress = 0f;
            isInsideRange = false;
            previousHealth = playerHealth.CurrentHealth;
            return;
        }

        float currentHealth = playerHealth.CurrentHealth;
        bool tookDamage = currentHealth < previousHealth - 0.01f;
        previousHealth = currentHealth;

        Vector2 playerPosition = transform.position;
        Vector2 zonePosition = extractionPoint.position;
        float distanceSquared = (playerPosition - zonePosition).sqrMagnitude;
        isInsideRange = distanceSquared <= extractionRadius * extractionRadius;

        if (!isInsideRange || tookDamage || lootCollector.TotalLootCount < minimumLootCount)
        {
            progress = 0f;
            return;
        }

        bool isHoldingKey = Keyboard.current != null && Keyboard.current.eKey.isPressed;
        if (!isHoldingKey)
        {
            progress = 0f;
            return;
        }

        progress += Time.deltaTime;

        if (progress >= extractionDuration)
            CompleteExtraction();
    }

    private void CompleteExtraction()
    {
        if (isExtracted)
            return;

        isExtracted = true;
        progress = extractionDuration;

        if (playerMovement != null)
            playerMovement.enabled = false;

        if (playerShooting != null)
            playerShooting.enabled = false;

        Debug.Log(
            "Рейд завершён. Газява: " + lootCollector.GazyavaCount +
            ", хрумки: " + lootCollector.KhrumkiCount + ".",
            this);
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
            return;

        EnsureGuiStyles();

        if (isExtracted)
        {
            DrawExtractionResult();
            return;
        }

        if (isInsideRange && playerHealth != null && !playerHealth.IsDead)
            DrawExtractionPrompt();
    }

    private void EnsureGuiStyles()
    {
        if (promptStyle != null)
            return;

        promptStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };

        detailStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };

        resultTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
    }

    private void DrawExtractionPrompt()
    {
        const float panelWidth = 410f;
        const float panelHeight = 104f;

        float panelX = (Screen.width - panelWidth) * 0.5f;
        float panelY = Screen.height - panelHeight - 34f;
        Rect panel = new Rect(panelX, panelY, panelWidth, panelHeight);

        Color previousColor = GUI.color;
        GUI.color = new Color(0.025f, 0.055f, 0.065f, 0.94f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;

        bool hasEnoughLoot = lootCollector.TotalLootCount >= minimumLootCount;
        string title = hasEnoughLoot
            ? "УДЕРЖИВАЙ E — ЭВАКУАЦИЯ"
            : "СНАЧАЛА СОБЕРИ ДОБЫЧУ";

        GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 30f), title, promptStyle);

        if (hasEnoughLoot)
        {
            const float barWidth = 350f;
            const float barHeight = 11f;
            Rect barBackground = new Rect(
                panel.x + (panelWidth - barWidth) * 0.5f,
                panel.y + 49f,
                barWidth,
                barHeight);

            GUI.color = new Color(0.13f, 0.19f, 0.21f, 1f);
            GUI.DrawTexture(barBackground, Texture2D.whiteTexture);

            Rect barFill = new Rect(
                barBackground.x,
                barBackground.y,
                barBackground.width * ExtractionProgress,
                barBackground.height);

            GUI.color = new Color(0.08f, 0.78f, 0.88f, 1f);
            GUI.DrawTexture(barFill, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(
                new Rect(panel.x + 12f, panel.y + 66f, panel.width - 24f, 22f),
                "Не отходи от зоны и не получай урон",
                detailStyle);
        }
        else
        {
            GUI.Label(
                new Rect(panel.x + 12f, panel.y + 54f, panel.width - 24f, 28f),
                "Нужно собрать предметов: " + minimumLootCount +
                "    Собрано: " + lootCollector.TotalLootCount,
                detailStyle);
        }

        GUI.color = previousColor;
    }

    private void DrawExtractionResult()
    {
        float width = Mathf.Min(460f, Screen.width - 32f);
        const float height = 170f;
        float x = (Screen.width - width) * 0.5f;
        float y = (Screen.height - height) * 0.5f;
        Rect panel = new Rect(x, y, width, height);

        Color previousColor = GUI.color;
        GUI.color = new Color(0.015f, 0.045f, 0.05f, 0.96f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(
            new Rect(panel.x + 16f, panel.y + 18f, panel.width - 32f, 42f),
            "РЕЙД УСПЕШНО ЗАВЕРШЁН",
            resultTitleStyle);

        GUI.Label(
            new Rect(panel.x + 20f, panel.y + 78f, panel.width - 40f, 26f),
            "ГАЗЯВА: " + lootCollector.GazyavaCount,
            promptStyle);

        GUI.Label(
            new Rect(panel.x + 20f, panel.y + 112f, panel.width - 40f, 26f),
            "ХРУМКИ: " + lootCollector.KhrumkiCount,
            promptStyle);

        GUI.color = previousColor;
    }
}
