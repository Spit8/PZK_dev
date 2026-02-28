using UnityEngine;
using Mirror;
using UnityEngine.InputSystem; // Vérifie que tu as bien installé le package Input System

public class PlayerCameraController : NetworkBehaviour
{
    [Header("Références")]
    public Transform cameraPivot;
    public Transform fpsEyes;
    public Camera playerCamera;

    [Header("Paramètres")]
    public float sensitivity = 0.1f;
    public float smoothSpeed = 15f;
    
    [Header("Distances")]
    public float minDistance = 0.0f;
    public float maxDistance = 8.0f;
    public float currentDistance = 4.0f;

    private float xRotation = 0f;

    public override void OnStartLocalPlayer()
    {
        // On cherche la caméra si elle n'est pas assignée
        if (playerCamera == null)
            playerCamera = Camera.main;
        
        if (playerCamera != null)
        {
            playerCamera.transform.SetParent(cameraPivot);
            playerCamera.transform.localPosition = new Vector3(0, 0, -currentDistance);
            playerCamera.transform.localRotation = Quaternion.identity;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        // On ne fait rien si ce n'est pas nous ou s'il manque le pivot
        if (!isLocalPlayer || cameraPivot == null) return;

        HandleRotation();
        HandleZoom();
        UpdateCameraPosition();
    }

    private void HandleRotation()
    {
        if (Mouse.current == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * sensitivity;

        xRotation -= mouseDelta.y;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        cameraPivot.localRotation = Quaternion.Euler(xRotation, 0, 0);
        transform.Rotate(Vector3.up * mouseDelta.x);
    }

    private void HandleZoom()
    {
        if (Mouse.current == null) return;

        float scrollValue = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scrollValue) > 0.1f)
        {
            // Normalisation du scroll pour le New Input System
            float scrollDir = Mathf.Sign(scrollValue);
            currentDistance -= scrollDir * 0.5f;
            currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
        }
    }

    private void UpdateCameraPosition()
    {
        if (playerCamera == null) return;

        Vector3 targetLocalPos;

        // Si on est très proche (FPS), on utilise le point des yeux
        if (currentDistance < 0.5f)
        {
            targetLocalPos = cameraPivot.InverseTransformPoint(fpsEyes.position);
        }
        else
        {
            targetLocalPos = new Vector3(0, 0, -currentDistance);
        }

        playerCamera.transform.localPosition = Vector3.Lerp(
            playerCamera.transform.localPosition, 
            targetLocalPos, 
            Time.deltaTime * smoothSpeed
        );
    }
}