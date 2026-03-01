using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

/// <summary>
/// Caméra hybride ISO / FPS - Projet PZK.
/// 
/// MODIFICATIONS INTÉGRÉES :
/// - ISO PANNING : La caméra se déporte vers le curseur lors du CLIC DROIT (Zomboid style).
/// - ISO AXES : Calcul des vecteurs Forward/Right basés sur l'angle NW 45°.
/// - FPS STABILITY : Maintien de la position stable via eyeHeight.
/// </summary>
public class PlayerCameraController : NetworkBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Références")]
    public Transform cameraPivot;
    public Camera playerCamera;

    [Header("Angle isométrique")]
    [Tooltip("Inclinaison verticale (PZK ≈ 40°)")]
    public float isoAngleX = 40f;
    [Tooltip("Rotation horizontale fixe (45° = vue NW)")]
    public float isoAngleY = 45f;

    [Header("Zoom")]
    public float minDistance = 0f;
    public float maxDistance = 12f;
    public float currentDistance = 7f;
    public float zoomSpeed = 0.6f;

    [Header("Transition FPS")]
    public float fpsThreshold = 0.6f;
    public float smoothSpeed = 12f;
    public float fpsSmoothSpeed = 20f;

    [Header("Vue FPS — Position")]
    public float eyeHeight = 1.65f;
    public float eyeForwardOffset = 0.1f;

    [Header("Near clip plane")]
    public float nearClipISO = 0.3f;
    public float nearClipFPS = 0.02f;

    [Header("FPS — Souris")]
    public float mouseYawSensitivity = 0.15f;
    public float mousePitchSensitivity = 0.15f;
    public float pitchMin = -80f;
    public float pitchMax = 80f;

    [Header("ISO — Visée (Zomboid)")]
    [Tooltip("Distance maximum de déport de la caméra vers le curseur")]
    public float maxAimOffset = 4f;
    [Tooltip("Vitesse de lissage du déport de caméra")]
    public float aimSmoothSpeed = 5f;

    // -------------------------------------------------------------------------
    // État interne
    // -------------------------------------------------------------------------

    private bool isFPSMode = false;
    private float fpsPitch = 0f;
    private bool fpsMouseLocked = true;
    private Vector3 currentAimOffset = Vector3.zero;

    public bool IsMouseLocked() => fpsMouseLocked;
    public bool IsInFPSMode() => isFPSMode;

    public Vector3 IsoForward { get; private set; }
    public Vector3 IsoRight { get; private set; }

    public Ray GetFPSLookRay()
    {
        if (playerCamera == null) return new Ray();
        return playerCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
    }

    // -------------------------------------------------------------------------
    // Init
    // -------------------------------------------------------------------------

    public override void OnStartLocalPlayer()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        ComputeIsoAxes();

        if (playerCamera != null)
        {
            playerCamera.transform.SetParent(cameraPivot);
            playerCamera.transform.localPosition = new Vector3(0f, 0f, -currentDistance);
            playerCamera.transform.localRotation = Quaternion.identity;
            playerCamera.nearClipPlane = nearClipISO;
        }

        SetCursorISO();
        ApplyIsoRotation();
    }

    // -------------------------------------------------------------------------
    // Boucle principale
    // -------------------------------------------------------------------------

    private void LateUpdate()
    {
        if (!isLocalPlayer || cameraPivot == null) return;

        HandleZoom();
        UpdateMode();
        UpdateCameraTransform();
    }

    private void HandleZoom()
    {
        if (Mouse.current == null) return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.1f)
        {
            currentDistance -= Mathf.Sign(scroll) * zoomSpeed;
            currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
        }
    }

    private void UpdateMode()
    {
        bool wasFPS = isFPSMode;
        isFPSMode = currentDistance < fpsThreshold;

        if (isFPSMode && !wasFPS)
        {
            fpsPitch = 0f;
            fpsMouseLocked = true;
            cameraPivot.localRotation = Quaternion.identity;

            if (playerCamera != null)
            {
                playerCamera.transform.localPosition = ComputeFPSLocalPos();
                playerCamera.nearClipPlane = nearClipFPS;
            }
            SetCursorFPS();
        }
        else if (!isFPSMode && wasFPS)
        {
            if (playerCamera != null)
            {
                playerCamera.transform.localPosition = new Vector3(0f, 0f, -currentDistance);
                playerCamera.nearClipPlane = nearClipISO;
            }
            SetCursorISO();
        }

        if (!isFPSMode)
            ApplyIsoRotation();
        else
            HandleFPSMouseLook();
    }

    private void HandleFPSMouseLook()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            fpsMouseLocked = !fpsMouseLocked;
            if (fpsMouseLocked) SetCursorFPS();
            else SetCursorISO();
        }

        if (!fpsMouseLocked) return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        if (Mathf.Abs(delta.x) > 0f)
            transform.Rotate(Vector3.up, delta.x * mouseYawSensitivity, Space.World);

        if (Mathf.Abs(delta.y) > 0f)
        {
            fpsPitch -= delta.y * mousePitchSensitivity;
            fpsPitch = Mathf.Clamp(fpsPitch, pitchMin, pitchMax);
            cameraPivot.localRotation = Quaternion.Euler(fpsPitch, 0f, 0f);
        }
    }

    private void UpdateCameraTransform()
    {
        if (playerCamera == null) return;

        if (isFPSMode)
        {
            playerCamera.transform.localPosition = Vector3.Lerp(
                playerCamera.transform.localPosition,
                ComputeFPSLocalPos(),
                Time.deltaTime * fpsSmoothSpeed);
        }
        else
        {
            // --- LOGIQUE PANNING ISO (Zomboid style) ---
            Vector3 targetLocalPos = new Vector3(0f, 0f, -currentDistance);

            if (Mouse.current.rightButton.isPressed)
            {
                // Projection pour trouver le point au sol sous la souris
                Ray ray = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                Plane groundPlane = new Plane(Vector3.up, transform.position);

                if (groundPlane.Raycast(ray, out float dist))
                {
                    Vector3 mouseWorld = ray.GetPoint(dist);
                    Vector3 offsetDir = mouseWorld - transform.position;

                    // On convertit le décalage monde en espace local du pivot pour le déport
                    Vector3 localOffset = cameraPivot.InverseTransformDirection(offsetDir * 0.5f);
                    localOffset.z = 0; // Pas de décalage sur l'axe de profondeur du zoom

                    Vector3 targetOffset = Vector3.ClampMagnitude(localOffset, maxAimOffset);
                    currentAimOffset = Vector3.Lerp(currentAimOffset, targetOffset, Time.deltaTime * aimSmoothSpeed);
                }
            }
            else
            {
                currentAimOffset = Vector3.Lerp(currentAimOffset, Vector3.zero, Time.deltaTime * aimSmoothSpeed);
            }

            playerCamera.transform.localPosition = Vector3.Lerp(
                playerCamera.transform.localPosition,
                targetLocalPos + currentAimOffset,
                Time.deltaTime * smoothSpeed);
        }

        playerCamera.transform.localRotation = Quaternion.identity;
    }

    private Vector3 ComputeFPSLocalPos()
    {
        Vector3 worldPos = transform.position + Vector3.up * eyeHeight;
        if (eyeForwardOffset > 0f)
        {
            Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            worldPos += flatForward * eyeForwardOffset;
        }
        return cameraPivot.InverseTransformPoint(worldPos);
    }

    private void ApplyIsoRotation() => cameraPivot.rotation = Quaternion.Euler(isoAngleX, isoAngleY, 0f);

    private void ComputeIsoAxes()
    {
        var rot = Quaternion.Euler(0f, isoAngleY, 0f);
        IsoForward = rot * Vector3.forward;
        IsoRight = rot * Vector3.right;
    }

    private static void SetCursorFPS() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    private static void SetCursorISO() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
}