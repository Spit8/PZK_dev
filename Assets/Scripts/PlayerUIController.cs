using UnityEngine;
using Mirror;

/// <summary>
/// Gère l'affichage du crosshair et l'état du curseur.
/// En TPS/FPS, le curseur est toujours verrouillé sauf quand l'inventaire est ouvert.
/// Le crosshair est affiché en permanence (centré sur l'écran).
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

        if (crosshairVisual == null)
        {
            Canvas mainCanvas = GameObject.FindFirstObjectByType<Canvas>();
            if (mainCanvas != null)
            {
                Transform t = mainCanvas.transform.Find("Crosshair");
                if (t != null)
                {
                    crosshairVisual = t.gameObject;
                }
            }
        }

        RefreshCursorState();
    }

    /// <summary>
    /// Met à jour le curseur et le crosshair selon le contexte.
    /// Appelé par PlayerCameraController lors des changements de mode,
    /// et par PlayerInventory lors de l'ouverture/fermeture.
    /// </summary>
    public void RefreshCursorState()
    {
        if (inventory == null) return;

        bool isInventoryOpen = inventory.isUIOpen;

        if (isInventoryOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (crosshairVisual != null) crosshairVisual.SetActive(false);
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (crosshairVisual != null) crosshairVisual.SetActive(true);
        }
    }
}
