using Mirror;
using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using System.Linq;

/// <summary>
/// Gère l'inventaire du joueur avec synchronisation réseau via Mirror.
/// Le serveur est l'unique source de vérité pour les slots d'inventaire.
/// </summary>
public class PlayerInventory : NetworkBehaviour
{
    [Header("Références")]
    [Tooltip("Transform enfant représentant la main (ex: RightHand)")]
    public Transform handSlot;
    [Tooltip("Base de données de tous les items du jeu")]
    public ItemDatabase itemDatabase;
    [Tooltip("Panel UI de l'inventaire")]
    [SerializeField] private GameObject inventoryUIPanel;
    [Tooltip("Liste des textes TMP fixes dans l'UI")]
    [SerializeField] private List<TextMeshProUGUI> uiSlots = new List<TextMeshProUGUI>();

    // PZK : Référence au contrôleur de caméra pour figer la vue
    private PlayerCameraController cameraController;
    private PlayerUIController uiController;

    [Header("Configuration")]
    public int inventorySize = 20;
    public float pickupRange = 3.0f;
    public LayerMask pickupLayerMask = Physics.DefaultRaycastLayers;

    /// <summary>
    /// Liste synchronisée des slots d'inventaire.
    /// Mirror gère la propagation des modifications du serveur vers les clients.
    /// </summary>
    public readonly SyncList<ItemSlot> inventorySlots = new SyncList<ItemSlot>();

    /// <summary>
    /// Index du slot actuellement sélectionné par le joueur.
    /// </summary>
    [SyncVar(hook = nameof(OnActiveSlotChanged))]
    public int activeSlotIndex = 0;

    /// <summary>
    /// Indique si l'interface de l'inventaire est actuellement ouverte sur le client local.
    /// Utilisé pour bloquer les actions de mouvement.
    /// </summary>
    public bool isUIOpen = false;

    /// <summary>
    /// Bascule l'affichage de l'inventaire.
    /// </summary>
    public void ToggleInventory()
    {
        if (!isLocalPlayer) return;

        isUIOpen = !isUIOpen;
        if (inventoryUIPanel != null)
        {
            inventoryUIPanel.SetActive(isUIOpen);
            
            // Gestion du rendu et de l'état (PZK : Fix UI state)
            if (isUIOpen)
            {
                // Forcer le rendu en ScreenSpaceOverlay pour éviter les problèmes de caméra
                Canvas canvas = inventoryUIPanel.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }

                // PZK : Figer la vue en mode FPS si l'inventaire est ouvert
                if (cameraController != null && cameraController.IsInFPSMode())
                {
                    cameraController.enabled = false;
                }

                // Mise à jour visuelle
                UpdateUI();
                
                if (uiController != null) uiController.RefreshCursorState();

                // Comptage des objets pour le test
                Debug.Log("[PZK] Inventaire contient " + inventorySlots.Count + " objets.");
            }
            else
            {
                // PZK : Réactiver la vue en fermant l'inventaire
                if (cameraController != null)
                {
                    cameraController.enabled = true;
                }

                if (uiController != null) uiController.RefreshCursorState();
            }
        }
        Debug.Log("[PZK] État UI : " + isUIOpen);
    }

    /// <summary>
    /// Référence locale à l'objet visuel actuellement tenu en main.
    /// </summary>
    private GameObject currentHandObject;

    // -------------------------------------------------------
    // INITIALISATION
    // -------------------------------------------------------

    private void Awake()
    {
        // Tentative de récupération automatique de la database si manquante
        if (itemDatabase == null)
        {
            itemDatabase = Resources.Load<ItemDatabase>("ItemDatabase");
            if (itemDatabase != null)
            {
                Debug.Log("[PlayerInventory] itemDatabase récupérée via Resources.Load.");
            }
        }
    }

    private void Start()
    {
        // On n'exécute la liaison UI QUE pour le joueur local
        if (isLocalPlayer)
        {
            // Récupération des contrôleurs
            cameraController = GetComponent<PlayerCameraController>();
            uiController = GetComponent<PlayerUIController>();

            // 1. Recherche automatique du panel même si inactif
            if (inventoryUIPanel == null)
            {
                // Tentative via Canvas
                inventoryUIPanel = GameObject.Find("Canvas")?.transform.Find("InventoryUI_Root")?.gameObject;
                
                // Tentative profonde si toujours nul
                if (inventoryUIPanel == null)
                {
                    inventoryUIPanel = Resources.FindObjectsOfTypeAll<GameObject>()
                        .FirstOrDefault(g => g.name == "InventoryUI_Root");
                }
            }

            // 2. Si on a trouvé le panel, on lie les slots
            if (inventoryUIPanel != null)
            {
                // Initialisation forcée : on l'éteint pour être sûr de l'état
                inventoryUIPanel.SetActive(false);

                uiSlots.Clear();
                // On récupère tous les composants TextMeshProUGUI (ItemNameText) des slots
                TextMeshProUGUI[] foundSlots = inventoryUIPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
                uiSlots.AddRange(foundSlots);

                // PZK : Liaison des composants d'interaction (InventorySlot)
                for (int i = 0; i < uiSlots.Count; i++)
                {
                    GameObject slotObj = uiSlots[i].transform.parent.gameObject;
                    InventorySlot interactionScript = slotObj.GetComponent<InventorySlot>();
                    if (interactionScript == null) interactionScript = slotObj.AddComponent<InventorySlot>();
                    interactionScript.slotIndex = i;
                }
                
                Debug.Log("[PZK] Liaison effectuée : " + uiSlots.Count + " slots détectés.");
                
                // 3. Initialisation de l'état
                isUIOpen = false;
                Cursor.visible = false;
            }
            else
            {
                Debug.LogError("[PZK] Erreur : Impossible de trouver 'InventoryUI_Root' même en recherche profonde !");
            }
        }
    }

    public override void OnStartServer()
    {
        // Initialisation de l'inventaire avec des slots vides sur le serveur
        for (int i = 0; i < inventorySize; i++)
        {
            inventorySlots.Add(new ItemSlot(0, 0));
        }
    }

    public override void OnStartClient()
    {
        // Enregistre le callback pour mettre à jour l'UI ou le visuel
        inventorySlots.Callback += OnInventoryChanged;
        
        // Initialise le visuel de la main au démarrage si nécessaire
        RefreshHandVisual();
    }

    // -------------------------------------------------------
    // CALLBACKS (Client)
    // -------------------------------------------------------

    /// <summary>
    /// Appelé sur tous les clients quand la SyncList est modifiée sur le serveur.
    /// </summary>
    private void OnInventoryChanged(SyncList<ItemSlot>.Operation op, int itemIndex, ItemSlot oldItem, ItemSlot newItem)
    {
        // Si le slot modifié est celui que nous tenons en main, on rafraîchit le visuel
        if (itemIndex == activeSlotIndex)
        {
            RefreshHandVisual();
        }

        // Met à jour l'UI si elle est ouverte
        if (isLocalPlayer && isUIOpen)
        {
            UpdateUI();
        }
    }

    private void OnActiveSlotChanged(int oldIndex, int newIndex)
    {
        RefreshHandVisual();
    }

    /// <summary>
    /// Met à jour l'objet visuel dans la main du joueur.
    /// </summary>
    private void RefreshHandVisual()
    {
        if (currentHandObject != null)
        {
            Destroy(currentHandObject);
        }

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
        if (itemIdentity == null)
        {
            Debug.LogWarning("[Inventory] CmdPickupItem: itemIdentity est nul");
            return;
        }

        // Validation de la distance côté serveur
        float distance = Vector3.Distance(transform.position, itemIdentity.transform.position);
        if (distance > pickupRange)
        {
            Debug.LogWarning($"[Inventory] {name} tente de ramasser un objet trop loin ({distance}m)");
            return;
        }

        // On tente de récupérer le composant PickupItem
        PickupItem pickup = itemIdentity.GetComponent<PickupItem>();
        if (pickup == null)
        {
            Debug.LogWarning($"[Inventory] {itemIdentity.name} n'a pas de composant PickupItem");
            return;
        }

        if (pickup.IsHeld)
        {
            Debug.LogWarning($"[Inventory] {itemIdentity.name} est déjà tenu");
            return;
        }

        // On ajoute l'objet à l'inventaire via son ID
        if (AddItem(pickup.itemId, 1))
        {
            Debug.Log($"[Inventory] Objet ramassé : {pickup.displayName} (ID:{pickup.itemId})");
            NetworkServer.Destroy(itemIdentity.gameObject);
        }
        else
        {
            Debug.Log("[Inventory] Inventaire plein ou erreur d'ID !");
        }
    }

    /// <summary>
    /// Commande pour lâcher un objet de l'inventaire dans le monde.
    /// </summary>
    /// <param name="index">Index du slot à vider</param>
    [Command]
    public void CmdDropItem(int index)
    {
        if (index < 0 || index >= inventorySlots.Count) return;

        ItemSlot slot = inventorySlots[index];
        if (slot.IsEmpty) return;

        ItemData data = itemDatabase != null ? itemDatabase.GetItemById(slot.itemId) : null;
        if (data != null && data.worldPrefab != null)
        {
            // Position de spawn devant le joueur (1.5m devant, un peu au-dessus du sol)
            Vector3 spawnPos = transform.position + (transform.forward * 1.5f) + Vector3.up * 0.5f;
            GameObject droppedObj = Instantiate(data.worldPrefab, spawnPos, Quaternion.identity);
            
            // Spawn sur le réseau via Mirror
            NetworkServer.Spawn(droppedObj);
            
            // Retirer l'item de la liste synchronisée (on vide le slot)
            inventorySlots[index] = new ItemSlot(0, 0);
            Debug.Log($"[PZK] Objet jeté : {data.itemName} (Index:{index})");
        }
        else
        {
            Debug.LogWarning("[PZK] Impossible de jeter l'objet : Prefab ou Data manquant.");
        }
    }

    /// <summary>
    /// Logique serveur pour ajouter un item à l'inventaire.
    /// </summary>
    [Server]
    public bool AddItem(int id, int amount)
    {
        // Fallback ultime : si la database est nulle, on tente un dernier chargement
        if (itemDatabase == null)
        {
            itemDatabase = Resources.Load<ItemDatabase>("ItemDatabase");
        }

        if (itemDatabase == null)
        {
            Debug.LogError("[PlayerInventory] itemDatabase est NULL sur le serveur ! Impossible d'ajouter l'objet.");
            return false;
        }

        ItemData data = itemDatabase.GetItemById(id);
        if (data == null)
        {
            Debug.LogWarning($"[PlayerInventory] ItemData introuvable pour l'ID {id}");
            return false;
        }

        int remaining = amount;

        // 1. Stack sur les slots existants
        if (data.isStackable)
        {
            for (int i = 0; i < inventorySlots.Count; i++)
            {
                if (inventorySlots[i].itemId == id)
                {
                    int space = data.maxStack - inventorySlots[i].amount;
                    int toAdd = Mathf.Min(space, remaining);
                    
                    if (toAdd > 0)
                    {
                        ItemSlot slot = inventorySlots[i];
                        slot.amount += toAdd;
                        inventorySlots[i] = slot;
                        remaining -= toAdd;
                    }
                }
                if (remaining <= 0) return true;
            }
        }

        // 2. Remplissage des slots vides
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            if (inventorySlots[i].IsEmpty)
            {
                int toAdd = Mathf.Min(data.maxStack, remaining);
                inventorySlots[i] = new ItemSlot(id, toAdd);
                remaining -= toAdd;
            }
            if (remaining <= 0) return true;
        }

        return remaining < amount;
    }

    /// <summary>
    /// Met à jour l'affichage de l'inventaire en utilisant les slots UI fixes.
    /// </summary>
    private void UpdateUI()
    {
        if (inventoryUIPanel == null) return;

        // On parcourt les slots UI disponibles
        for (int i = 0; i < uiSlots.Count; i++)
        {
            TextMeshProUGUI slotText = uiSlots[i];
            if (slotText == null) continue;

            // Récupération du parent (le GameObject du Slot Prefab)
            GameObject slotObject = slotText.transform.parent.gameObject;
            
            // On force le slot à rester visible (grille de 14 slots)
            slotObject.SetActive(true);

            // Si on dépasse les données synchronisées, on vide le texte
            if (i >= inventorySlots.Count)
            {
                slotText.text = "";
                continue;
            }

            ItemSlot slot = inventorySlots[i];

            if (slot.IsEmpty)
            {
                slotText.text = ""; // Texte vide si pas d'item
            }
            else
            {
                ItemData data = itemDatabase != null ? itemDatabase.GetItemById(slot.itemId) : null;
                
                if (data != null)
                {
                    slotText.text = data.itemName + " x" + slot.amount;
                    slotText.color = Color.white;
                    Debug.Log("[PZK] Mise à jour slot UI " + i + " : " + data.itemName + " x" + slot.amount);
                }
                else
                {
                    // PZK : Nettoyage UI si l'item n'est pas trouvé ou nul
                    slotText.text = "";
                }
            }
        }
    }
}
