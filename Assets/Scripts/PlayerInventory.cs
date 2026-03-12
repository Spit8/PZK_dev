using Mirror;
using UnityEngine;using UnityEngine;
using UnityEngine.InputSystem;

using System.Collections.Generic;

/// <summary>
/// Gère l'inventaire du joueur avec synchronisation réseau via Mirror.
/// Le Canvas UI est dans la scène, trouvé au runtime pour le joueur local.
/// </summary>
public class PlayerInventory : NetworkBehaviour
{
    [Header("Références")]
    [Tooltip("Transform enfant représentant la main (ex: RightHand)")]
    public Transform handSlot;

    [Tooltip("Base de données de tous les items du jeu")]
    public ItemDatabase itemDatabase;

    [Header("Configuration")]
    public int inventorySize = 20;
    public float pickupRange = 3.0f;
    public LayerMask pickupLayerMask = Physics.DefaultRaycastLayers;

    /// <summary>Liste synchronisée des slots d'inventaire.</summary>
    public readonly SyncList<ItemSlot> inventorySlots = new SyncList<ItemSlot>();

    [SyncVar(hook = nameof(OnActiveSlotChanged))]
    public int activeSlotIndex = 0;

    /// <summary>Indique si l'inventaire est ouvert sur le client local.</summary>
    public bool isUIOpen = false;

    // ── Références internes ──────────────────────────────────────────────────
    private GameObject inventoryUIPanel;
    private InventoryUI inventoryUIController;
    private PlayerCameraController cameraController;
    private PlayerUIController uiController;
    private GameObject currentHandObject;

    // -------------------------------------------------------
    // INITIALISATION
    // -------------------------------------------------------

    private void Awake()
    {
        if (itemDatabase == null)
            itemDatabase = Resources.Load<ItemDatabase>("ItemDatabase");
    }

    private void Start()
    {
        if (!isLocalPlayer) return;

        cameraController = GetComponent<PlayerCameraController>();
        uiController = GetComponent<PlayerUIController>();

        // PZK : Le Canvas est dans la scène — on cherche InventoryUI_Root via le Canvas de scène
        Canvas sceneCanvas = GameObject.FindFirstObjectByType<Canvas>();
        if (sceneCanvas != null)
        {
            Transform root = sceneCanvas.transform.Find("InventoryUI_Root");
            if (root != null)
                inventoryUIPanel = root.gameObject;
        }

        if (inventoryUIPanel == null)
        {
            Debug.LogError("[PZK] 'InventoryUI_Root' introuvable dans le Canvas de scène !");
            return;
        }

        // Panel fermé par défaut
        inventoryUIPanel.SetActive(false);
        isUIOpen = false;

        // Récupération du contrôleur InventoryUI
        inventoryUIController = inventoryUIPanel.GetComponent<InventoryUI>();
        if (inventoryUIController == null)
            inventoryUIController = inventoryUIPanel.AddComponent<InventoryUI>();

        inventoryUIController.Initialize(this);

        // Abonnement unique à la SyncList
        inventorySlots.Callback += OnInventoryChanged;

        Debug.Log("[PZK] PlayerInventory initialisé.");
    }

    public override void OnStartServer()
    {
        for (int i = 0; i < inventorySize; i++)
            inventorySlots.Add(new ItemSlot(0, 0));
    }

    public override void OnStartClient()
    {
        RefreshHandVisual();
    }

    private void OnDestroy()
    {
        inventorySlots.Callback -= OnInventoryChanged;
    }

    private void Update()
    {
        if (!isLocalPlayer) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            CmdSetActiveSlot(0);
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            CmdSetActiveSlot(1);
        }
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            CmdSetActiveSlot(2);
        }
        else if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            CmdSetActiveSlot(3);
        }
        else if (Keyboard.current.digit5Key.wasPressedThisFrame)
        {
            CmdSetActiveSlot(4);
        }
        else if (Keyboard.current.digit6Key.wasPressedThisFrame)
        {
            CmdSetActiveSlot(5);
        }
        else if (Keyboard.current.digit7Key.wasPressedThisFrame)
        {
            CmdSetActiveSlot(6);
        }
        else if (Keyboard.current.digit8Key.wasPressedThisFrame)
        {
            CmdSetActiveSlot(7);
        }
        else if (Keyboard.current.digit9Key.wasPressedThisFrame)
        {
            CmdSetActiveSlot(8);
        }
    }


    // -------------------------------------------------------
    // TOGGLE INVENTAIRE (touche I / Tab)
    // -------------------------------------------------------

    public void ToggleInventory()
    {
        if (!isLocalPlayer) return;
        if (inventoryUIPanel == null) return;

        isUIOpen = !isUIOpen;
        inventoryUIPanel.SetActive(isUIOpen);

        if (isUIOpen)
        {
            if (cameraController != null && cameraController.IsInFPSMode())
                cameraController.enabled = false;

            inventoryUIController?.RefreshUI();
        }
        else
        {
            if (cameraController != null)
                cameraController.enabled = true;
        }

        uiController?.RefreshCursorState();
        Debug.Log("[PZK] Inventaire : " + (isUIOpen ? "ouvert" : "fermé"));
    }

    // -------------------------------------------------------
    // CALLBACKS
    // -------------------------------------------------------

    private void OnInventoryChanged(SyncList<ItemSlot>.Operation op, int itemIndex, ItemSlot oldItem, ItemSlot newItem)
    {
        if (itemIndex == activeSlotIndex)
            RefreshHandVisual();

        if (isLocalPlayer && isUIOpen)
            inventoryUIController?.RefreshUI();
    }

    private void OnActiveSlotChanged(int oldIndex, int newIndex)
    {
        RefreshHandVisual();
    }

    private void RefreshHandVisual()
    {
        if (currentHandObject != null)
            Destroy(currentHandObject);

        if (activeSlotIndex < 0 || activeSlotIndex >= inventorySlots.Count) return;

        ItemSlot slot = inventorySlots[activeSlotIndex];
        if (slot.IsEmpty || itemDatabase == null) return;

        ItemData data = itemDatabase.GetItemById(slot.itemId);
        if (data != null && data.handPrefab != null && handSlot != null)
        {
            currentHandObject = Instantiate(data.handPrefab, handSlot);
            currentHandObject.transform.localPosition = Vector3.zero;
            currentHandObject.transform.localRotation = Quaternion.identity;
        }
    }

    // -------------------------------------------------------
    // COMMANDES (Client → Serveur)
    // -------------------------------------------------------

    [Command]
    public void CmdPickupItem(NetworkIdentity itemIdentity)
    {
        if (itemIdentity == null) return;

        float distance = Vector3.Distance(transform.position, itemIdentity.transform.position);
        if (distance > pickupRange)
        {
            Debug.LogWarning($"[Inventory] Objet trop loin ({distance:F1}m)");
            return;
        }

        PickupItem pickup = itemIdentity.GetComponent<PickupItem>();
        if (pickup == null || pickup.IsHeld) return;

        if (AddItem(pickup.itemId, 1))
        {
            Debug.Log($"[Inventory] Ramassé : {pickup.displayName} (ID:{pickup.itemId})");
            NetworkServer.Destroy(itemIdentity.gameObject);
        }
        else
        {
            Debug.Log("[Inventory] Inventaire plein !");
        }
    }

    [Command]
    public void CmdDropItem(int index)
    {
        if (index < 0 || index >= inventorySlots.Count) return;

        ItemSlot slot = inventorySlots[index];
        if (slot.IsEmpty) return;

        ItemData data = itemDatabase?.GetItemById(slot.itemId);
        if (data != null && data.worldPrefab != null)
        {
            Vector3 spawnPos = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;
            NetworkServer.Spawn(Instantiate(data.worldPrefab, spawnPos, Quaternion.identity));
            inventorySlots[index] = new ItemSlot(0, 0);
            Debug.Log($"[PZK] Jeté : {data.itemName} (index {index})");
        }
    }

    [Command]
    public void CmdSetActiveSlot(int newIndex)
    {
        if (newIndex < 0 || newIndex >= inventorySize) return;
        activeSlotIndex = newIndex;
    }


    [Server]
    public bool AddItem(int id, int amount)
    {
        if (itemDatabase == null)
            itemDatabase = Resources.Load<ItemDatabase>("ItemDatabase");

        if (itemDatabase == null) return false;

        ItemData data = itemDatabase.GetItemById(id);
        if (data == null) return false;

        int remaining = amount;

        if (data.isStackable)
        {
            for (int i = 0; i < inventorySlots.Count && remaining > 0; i++)
            {
                if (inventorySlots[i].itemId == id)
                {
                    int toAdd = Mathf.Min(data.maxStack - inventorySlots[i].amount, remaining);
                    if (toAdd > 0)
                    {
                        ItemSlot s = inventorySlots[i];
                        s.amount += toAdd;
                        inventorySlots[i] = s;
                        remaining -= toAdd;
                    }
                }
            }
        }

        for (int i = 0; i < inventorySlots.Count && remaining > 0; i++)
        {
            if (inventorySlots[i].IsEmpty)
            {
                int toAdd = Mathf.Min(data.maxStack, remaining);
                inventorySlots[i] = new ItemSlot(id, toAdd);
                remaining -= toAdd;
            }
        }

        return remaining < amount;
    }
}
