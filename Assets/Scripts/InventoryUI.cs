using UnityEngine;
using Mirror;
using System.Collections.Generic;

/// <summary>
/// Gère l'affichage dynamique des slots d'inventaire.
/// Reçoit sa référence PlayerInventory via Initialize().
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Tooltip("Prefab du slot d'inventaire à instancier")]
    public GameObject inventorySlotPrefab;

    [Tooltip("Container qui reçoit les slots (VerticalLayoutGroup)")]
    public Transform container;

    private PlayerInventory playerInventory;

    // -------------------------------------------------------
    // INITIALISATION
    // -------------------------------------------------------

    /// <summary>
    /// Appelé par PlayerInventory.Start() — reçoit la référence directement.
    /// </summary>
    public void Initialize(PlayerInventory inventory)
    {
        playerInventory = inventory;
        Debug.Log("[InventoryUI] Initialisé.");
    }

    // -------------------------------------------------------
    // REFRESH
    // -------------------------------------------------------

    /// <summary>
    /// Met à jour l'UI en diff : ne crée/détruit des slots que si le contenu a changé.
    /// </summary>
    public void RefreshUI()
    {
        if (container == null || inventorySlotPrefab == null || playerInventory == null) return;
        if (!gameObject.activeInHierarchy) return;

        ItemDatabase database = playerInventory.itemDatabase;

        // Construire la liste des items non vides attendus
        List<(int slotIndex, ItemSlot slot)> expected = new List<(int, ItemSlot)>();
        for (int i = 0; i < playerInventory.inventorySlots.Count; i++)
        {
            ItemSlot slot = playerInventory.inventorySlots[i];
            if (!slot.IsEmpty)
                expected.Add((i, slot));
        }

        // Récupérer les slots UI existants
        List<InventorySlot> existing = new List<InventorySlot>();
        foreach (Transform child in container)
        {
            InventorySlot s = child.GetComponent<InventorySlot>();
            if (s != null) existing.Add(s);
        }

        // Supprimer les slots UI qui ne correspondent plus à aucun item
        foreach (InventorySlot uiSlot in existing)
        {
            bool stillValid = false;
            foreach (var (idx, _) in expected)
                if (uiSlot.slotIndex == idx) { stillValid = true; break; }

            if (!stillValid)
                Destroy(uiSlot.gameObject);
        }

        // Mettre à jour ou créer les slots
        foreach (var (idx, slot) in expected)
        {
            InventorySlot uiSlot = null;
            foreach (InventorySlot s in existing)
                if (s != null && s.slotIndex == idx) { uiSlot = s; break; }

            if (uiSlot != null)
            {
                uiSlot.Setup(slot, idx, database);
            }
            else
            {
                GameObject slotObj = Instantiate(inventorySlotPrefab, container);
                InventorySlot slotScript = slotObj.GetComponent<InventorySlot>()
                    ?? slotObj.AddComponent<InventorySlot>();
                slotScript.Setup(slot, idx, database);
            }
        }

        Debug.Log($"[InventoryUI] {expected.Count} slot(s) affichés.");
    }
}
