using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

public class PlayerCameraHandler : NetworkBehaviour
{
    [Header("Points d'ancrage")]
    public Transform cameraPivot; 
    public Transform fpsEyes;     

    [Header("Reglages de la Vue")]
    public Vector3 fixedOffset = new Vector3(0, 2, -4);
    public float zoomSpeed = 0.05f;
    public float minZoom = 1.5f;
    public float maxZoom = 10f;
    public float mouseSensitivity = 0.15f; 

    private Camera mainCam;
    private float currentZoomDistance;
    private bool isFPS = false;
    private float verticalRotation = 0f;

    public override void OnStartLocalPlayer()
    {
        mainCam = Camera.main;
        currentZoomDistance = fixedOffset.magnitude;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (!isLocalPlayer || mainCam == null) return;

        HandleInputs();

        // On gère la rotation du corps dans tous les cas pour pouvoir tourner
        HandleRotation();

        if (isFPS)
            UpdateFPSView();
        else
            UpdateThirdPersonView();
    }

    private void HandleInputs()
    {
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            isFPS = !isFPS;
        }

        if (!isFPS)
        {
            float scrollValue = Mouse.current.scroll.ReadValue().y;
            if (scrollValue != 0)
            {
                currentZoomDistance = Mathf.Clamp(currentZoomDistance - (scrollValue * zoomSpeed), minZoom, maxZoom);
            }
        }
    }

    private void HandleRotation()
    {
        // Lecture du mouvement de souris
        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;

        // Rotation horizontale du corps (Axe Y)
        transform.Rotate(Vector3.up * mouseDelta.x);

        // Calcul de la rotation verticale (Axe X) pour le mode FPS
        verticalRotation -= mouseDelta.y;
        verticalRotation = Mathf.Clamp(verticalRotation, -80f, 80f);
    }

    private void UpdateThirdPersonView()
    {
        // POSITION : On part du pivot et on recule selon l'orientation arrière du joueur
        // On utilise -transform.forward pour être toujours derrière le dos du joueur
        Vector3 backDirection = -transform.forward;
        Vector3 targetPosition = cameraPivot.position + (backDirection * currentZoomDistance) + (transform.up * fixedOffset.y);

        mainCam.transform.position = targetPosition;
        
        // REGARD : La caméra pointe vers le pivot
        mainCam.transform.LookAt(cameraPivot.position + transform.up * 0.5f);
    }

    private void UpdateFPSView()
    {
        // POSITION : Calée sur les yeux
        mainCam.transform.position = fpsEyes.position;

        // ROTATION : Combine la rotation du corps et l'inclinaison verticale
        mainCam.transform.rotation = Quaternion.Euler(verticalRotation, transform.eulerAngles.y, 0);
    }
}