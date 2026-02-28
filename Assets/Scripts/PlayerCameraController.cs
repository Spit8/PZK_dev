using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

/// <summary>
/// Caméra hybride ISO / FPS.
///
/// VUE ISO (currentDistance >= fpsThreshold)
///   - Angle isométrique fixe en world space
///   - Souris libre (curseur visible)
///
/// VUE FPS (currentDistance < fpsThreshold, via scroll molette)
///   - Caméra collée aux yeux du joueur
///   - Souris X → yaw du joueur  (rotation horizontale, caméra suit)
///   - Souris Y → pitch de la caméra (regard haut/bas)
///   - Curseur verrouillé au centre de l'écran
///   - GetFPSLookRay() → raycast depuis le centre pour sélection par le regard
///
/// MOLETTE → zoom uniquement
/// </summary>
public class PlayerCameraController : NetworkBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Références")]
    public Transform cameraPivot;
    public Transform fpsEyes;
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
    [Tooltip("Distance en dessous de laquelle on passe en vue FPS")]
    public float fpsThreshold = 0.6f;
    [Tooltip("Lissage de la position de la caméra")]
    public float smoothSpeed = 12f;

    [Header("FPS — Souris")]
    [Tooltip("Sensibilité horizontale (yaw du joueur)")]
    public float mouseYawSensitivity = 0.15f;
    [Tooltip("Sensibilité verticale (pitch de la caméra)")]
    public float mousePitchSensitivity = 0.15f;
    [Tooltip("Limite basse du regard")]
    public float pitchMin = -80f;
    [Tooltip("Limite haute du regard")]
    public float pitchMax = 80f;

    // -------------------------------------------------------------------------
    // État interne
    // -------------------------------------------------------------------------

    private bool isFPSMode = false;
    private float fpsPitch = 0f;

    // -------------------------------------------------------------------------
    // API publique
    // -------------------------------------------------------------------------

    public bool IsInFPSMode() => isFPSMode;

    public Vector3 IsoForward { get; private set; }
    public Vector3 IsoRight { get; private set; }

    /// <summary>
    /// Rayon depuis le centre de l'écran dans la direction du regard (FPS).
    /// Utilise ce ray pour la sélection d'objets par le regard.
    /// </summary>
    public Ray GetFPSLookRay()
    {
        if (playerCamera == null) return new Ray();
        return playerCamera.ScreenPointToRay(
            new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
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
        }

        // Démarre en ISO : souris libre
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

    // -------------------------------------------------------------------------
    // Zoom
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Basculement ISO ↔ FPS
    // -------------------------------------------------------------------------

    private void UpdateMode()
    {
        bool wasFPS = isFPSMode;
        isFPSMode = currentDistance < fpsThreshold;

        // Transitions
        if (isFPSMode && !wasFPS)
        {
            fpsPitch = 0f;
            cameraPivot.localRotation = Quaternion.identity;
            SetCursorFPS();
        }
        else if (!isFPSMode && wasFPS)
        {
            SetCursorISO();
        }

        if (!isFPSMode)
            ApplyIsoRotation();     // Pivot figé en world space
        else
            HandleFPSMouseLook();   // Souris → yaw joueur + pitch caméra
    }

    // -------------------------------------------------------------------------
    // FPS — mouse look
    // -------------------------------------------------------------------------

    private void HandleFPSMouseLook()
    {
        if (Mouse.current == null) return;

        Vector2 delta = Mouse.current.delta.ReadValue();

        // Yaw : rotation horizontale du joueur (transform géré ici, pas dans PlayerMovement)
        if (Mathf.Abs(delta.x) > 0f)
            transform.Rotate(Vector3.up, delta.x * mouseYawSensitivity, Space.World);

        // Pitch : inclinaison verticale de la caméra uniquement
        if (Mathf.Abs(delta.y) > 0f)
        {
            fpsPitch -= delta.y * mousePitchSensitivity;
            fpsPitch = Mathf.Clamp(fpsPitch, pitchMin, pitchMax);
            cameraPivot.localRotation = Quaternion.Euler(fpsPitch, 0f, 0f);
        }
    }

    // -------------------------------------------------------------------------
    // Position de la caméra
    // -------------------------------------------------------------------------

    private void UpdateCameraTransform()
    {
        if (playerCamera == null) return;

        Vector3 targetLocalPos = isFPSMode
            ? cameraPivot.InverseTransformPoint(fpsEyes.position)
            : new Vector3(0f, 0f, -currentDistance);

        playerCamera.transform.localPosition = Vector3.Lerp(
            playerCamera.transform.localPosition,
            targetLocalPos,
            Time.deltaTime * smoothSpeed);

        playerCamera.transform.localRotation = Quaternion.identity;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void ApplyIsoRotation()
    {
        cameraPivot.rotation = Quaternion.Euler(isoAngleX, isoAngleY, 0f);
    }

    private void ComputeIsoAxes()
    {
        var rot = Quaternion.Euler(0f, isoAngleY, 0f);
        IsoForward = rot * Vector3.forward;
        IsoRight = rot * Vector3.right;
    }

    private static void SetCursorFPS()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void SetCursorISO()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
