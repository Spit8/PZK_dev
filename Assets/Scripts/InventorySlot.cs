using UnityEngine;
using UnityEngine.EventSystems;
using Mirror;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Représente un slot de l'inventaire dans l'UI.
/// Layout en colonnes : icône | nom | quantité | poids
/// </summary>
public class InventorySlot : MonoBehaviour, IPointerClickHandler
{
    public int slotIndex;

    [Header("Colonnes UI")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemAmountText;
    [SerializeField] private TextMeshProUGUI itemWeightText;

    public bool IsEmpty { get; private set; } = true;
    public int itemId { get; private set; }
    public int amount { get; private set; }

    private ItemDatabase itemDatabase;

    public void Setup(ItemSlot slot, int index, ItemDatabase database = null)
    {
        slotIndex = index;
        itemId = slot.itemId;
        amount = slot.amount;
        IsEmpty = slot.IsEmpty;
        itemDatabase = database;

        if (IsEmpty)
        {
            ClearUI();
            return;
        }

        ItemData data = database?.GetItemById(itemId);

        // Icône
        if (itemIcon != null)
        {
            if (data?.icon != null)
            {
                itemIcon.sprite = data.icon;
                itemIcon.enabled = true;
            }
            else
            {
                itemIcon.sprite = null;
                itemIcon.enabled = false;
            }
        }

        // Nom
        if (itemNameText != null)
            itemNameText.text = data != null ? data.itemName : $"Item#{itemId}";

        // Quantité (affiché seulement si stackable)
        if (itemAmountText != null)
        {
            if (data != null && data.isStackable)
                itemAmountText.text = amount.ToString();
            else
                itemAmountText.text = "-";
        }

        // Poids total (quantité x poids unitaire)
        if (itemWeightText != null)
            itemWeightText.text = data != null ? $"{data.weight * amount} kg" : "-";
    }

    private void ClearUI()
    {
        if (itemIcon != null) itemIcon.enabled = false;
        if (itemNameText != null) itemNameText.text = "";
        if (itemAmountText != null) itemAmountText.text = "";
        if (itemWeightText != null) itemWeightText.text = "";
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;

        if (NetworkClient.localPlayer != null)
        {
            PlayerInventory inventory = NetworkClient.localPlayer.GetComponent<PlayerInventory>();
            inventory?.CmdDropItem(slotIndex);
        }
    }
}
