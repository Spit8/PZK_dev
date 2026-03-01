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

    private void Start()
    {
        cameraController = GetComponent<PlayerCameraController>();
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        // En FPS : raycast depuis le centre de l'écran
        // En ISO : raycast depuis la position de la souris
        Ray ray = (cameraController.IsInFPSMode() && cameraController.IsMouseLocked())
            ? cameraController.GetFPSLookRay()
            : Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        GameObject hit = null;

        if (Physics.Raycast(ray, out RaycastHit hitInfo, interactRange, interactableLayers))
        {
            Debug.Log("Hit : " + hitInfo.collider.gameObject.name);
            hit = hitInfo.collider.gameObject;
        }

        if (hit != currentHovered)
        {
            if (currentHovered != null) HighlightInteraction.Instance.Unhighlight(currentHovered);
            if (hit != null) HighlightInteraction.Instance.Highlight(hit);
            currentHovered = hit;
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
