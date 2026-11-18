using InspectorLocalization;
using UnityEngine;

public enum LootItemType
{
    Gazyava,
    Khrumki
}

[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class LootPickup : MonoBehaviour
{
    [InspectorLabel("Тип добычи", "Определяет, в какой счётчик будет добавлен предмет после успешного сбора.")]
    [SerializeField] private LootItemType itemType = LootItemType.Gazyava;

    private bool isCollected;

    public LootItemType ItemType => itemType;
    public bool IsCollected => isCollected;

    public string DisplayName
    {
        get
        {
            return itemType == LootItemType.Gazyava ? "газяву" : "хрумки";
        }
    }

    private void Awake()
    {
        CircleCollider2D pickupCollider = GetComponent<CircleCollider2D>();
        if (pickupCollider != null)
            pickupCollider.isTrigger = true;
    }

    public void Configure(LootItemType newItemType)
    {
        itemType = newItemType;
    }

    public bool TryCollect()
    {
        if (isCollected || !isActiveAndEnabled)
            return false;

        isCollected = true;
        gameObject.SetActive(false);
        return true;
    }
}
