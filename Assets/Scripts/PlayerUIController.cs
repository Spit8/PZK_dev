using UnityEngine;
using Mirror;

/// <summary>
/// Gère l'affichage de l'UI du joueur, notamment le crosshair et le curseur.
/// Se base sur l'état de la caméra (ISO/FPS) et de l'inventaire (Ouvert/Fermé).
/// </summary>
public class PlayerUIController : NetworkBehaviour
{
    [Header("Références UI")]
    [Tooltip("L'objet visuel du Crosshair (ex: une image au centre de l'écran)")]
    [SerializeField] private GameObject crosshairVisual;

    private PlayerInventory inventory;
    private PlayerCameraController cameraController;

    private void Start()
    {
        if (!isLocalPlayer) return;

        inventory = GetComponent<PlayerInventory>();
        cameraController = GetComponent<PlayerCameraController>();

        // Liaison dynamique du Crosshair (PZK)
        Canvas mainCanvas = GameObject.FindObjectOfType<Canvas>();
        if (mainCanvas != null)
        {
            Transform t = mainCanvas.transform.Find("Crosshair");
            if (t != null)
            {
                crosshairVisual = t.gameObject;
                Debug.Log("[PZK] Crosshair relié avec succès !");
            }
            else
            {
                Debug.LogError("[PZK] Objet 'Crosshair' introuvable dans le Canvas !");
            }
        }

        // Initialisation immédiate de l'état
        RefreshCursorState();
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        // On appelle RefreshCursorState à chaque frame par sécurité pour l'instant
        RefreshCursorState();
    }

    /// <summary>
    /// Automatise l'affichage du Crosshair et l'état du Curseur selon le contexte.
    /// </summary>
    public void RefreshCursorState()
    {
        if (inventory == null || cameraController == null) return;

        bool isFPS = cameraController.IsInFPSMode();
        bool isInventoryOpen = inventory.isUIOpen;

        if (!isFPS)
        {
            // --- MODE ISO ---
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (crosshairVisual != null) crosshairVisual.SetActive(false);
        }
        else
        {
            // --- MODE FPS ---
            if (isInventoryOpen)
            {
                // Inventaire ouvert en FPS
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (crosshairVisual != null) crosshairVisual.SetActive(false);
            }
            else
            {
                // Inventaire fermé en FPS (Jeu normal)
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                if (crosshairVisual != null) crosshairVisual.SetActive(true);
            }
        }
    }
}
