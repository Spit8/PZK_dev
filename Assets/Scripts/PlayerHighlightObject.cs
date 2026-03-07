using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

public class PlayerHighlightObject : NetworkBehaviour
{
    [Header("Raycast")]
    public float interactRange = 4f;
    public LayerMask interactableLayers;

    private PlayerCameraController cameraController;
    private GameObject currentHovered;

    /// <summary>Retourne l'objet actuellement survolé par le raycast d'interaction.</summary>
    public GameObject GetCurrentHovered() => currentHovered;

    /// <summary>Force la suppression de la surbrillance et du texte UI (appelé après ramassage).</summary>
    public void ClearUI()
    {
        if (currentHovered != null && HighlightInteraction.Instance != null)
        {
            HighlightInteraction.Instance.Unhighlight(currentHovered);
            if (HighlightInteraction.Instance.hoverLabel != null)
            {
                HighlightInteraction.Instance.hoverLabel.text = "";
            }
        }
        currentHovered = null;
    }

    private void Start()
    {
        cameraController = GetComponent<PlayerCameraController>();
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        // En FPS : raycast depuis le centre de l'�cran
        // En ISO : raycast depuis la position de la souris
        Ray ray = (cameraController.IsInFPSMode() && cameraController.IsMouseLocked())
            ? cameraController.GetFPSLookRay()
            : Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        GameObject hit = null;

        if (Physics.Raycast(ray, out RaycastHit hitInfo, interactRange, interactableLayers))
        {
            hit = hitInfo.collider.gameObject;
        }

        if (hit != currentHovered)
        {
            if (currentHovered != null && HighlightInteraction.Instance != null) 
            {
                HighlightInteraction.Instance.Unhighlight(currentHovered);
                if (HighlightInteraction.Instance.hoverLabel != null)
                {
                    HighlightInteraction.Instance.hoverLabel.text = "";
                }
            }
            
            if (hit != null && HighlightInteraction.Instance != null) 
                HighlightInteraction.Instance.Highlight(hit);
            
            currentHovered = hit;
        }

        // Sécurité supplémentaire : si l'objet est détruit (ramassé) mais toujours considéré comme "survolé"
        if (currentHovered == null && hit == null)
        {
            if (HighlightInteraction.Instance != null && HighlightInteraction.Instance.hoverLabel != null)
            {
                HighlightInteraction.Instance.hoverLabel.text = "";
                HighlightInteraction.Instance.hoverLabel.gameObject.SetActive(false);
            }
        }
    }

    private void OnDisable()
    {
        if (currentHovered != null)
        {
            HighlightInteraction.Instance.Unhighlight(currentHovered);
            currentHovered = null;
        }
    }
}
