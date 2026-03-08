using UnityEngine;
using UnityEngine.EventSystems;
using Mirror;
using TMPro;

/// <summary>
/// Gère les interactions sur un slot de l'inventaire.
/// </summary>
public class InventorySlot : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("L'index de ce slot dans la SyncList du joueur")]
    public int slotIndex;

    [Tooltip("Référence au texte du nom de l'item")]
    [SerializeField] private TextMeshProUGUI itemNameText;

    private void Awake()
    {
        // On tente de récupérer le texte s'il n'est pas assigné
        if (itemNameText == null)
            itemNameText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    /// <summary>
    /// Détecte les clics sur l'UI du slot.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // On ne gère que le clic droit pour l'instant (lâcher l'objet)
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (NetworkClient.localPlayer != null)
            {
                PlayerInventory inventory = NetworkClient.localPlayer.GetComponent<PlayerInventory>();
                if (inventory != null)
                {
                    Debug.Log("[PZK] Clic droit sur le slot index : " + slotIndex);
                    inventory.CmdDropItem(slotIndex);
                }
            }
        }
    }

    /// <summary>
    /// Nettoie l'UI du slot si nécessaire.
    /// </summary>
    public void ClearUI()
    {
        if (itemNameText != null)
            itemNameText.text = "";
    }
}
