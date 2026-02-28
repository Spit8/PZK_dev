using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

/// <summary>
/// Caméra hybride ISO / FPS.
///
/// STABILITÉ EN VUE FPS :
///
///   La caméra FPS ne suit PAS les os du squelette (pas de bone tracking).
///   Elle est calculée depuis transform.position + eyeHeight, ce qui donne
///   une position stable indépendante des animations du squelette.
///
///   fpsEyes n'est plus nécessaire. La hauteur des yeux se règle via
///   eyeHeight dans l'inspector.
///
///   Un léger lerp (fpsSmoothSpeed) absorbe les éventuelles secousses
///   du CharacterController lui-même (steps, slopes) sans reproduire
///   le bobbing des animations.
///
/// </summary>
public class PlayerCameraController : NetworkBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Références")]
    public Transform cameraPivot;
    public Camera playerCamera;
    // fpsEyes n'est plus utilisé — la position est calculée depuis transform

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
    [Tooltip("Distance en dessous de laquelle on bascule en FPS (snap instantané)")]
    public float fpsThreshold = 0.6f;
    [Tooltip("Lissage du recul caméra en ISO")]
    public float smoothSpeed = 12f;
    [Tooltip("Lissage de la position FPS — absorbe les secousses du CharacterController.\n" +
             "Valeur haute (20+) = caméra quasi-rigide. Valeur basse (5–8) = légère inertie.")]
    public float fpsSmoothSpeed = 20f;

    [Header("Vue FPS — Position")]
    [Tooltip("Hauteur des yeux en unités Unity depuis transform.position (la base du CharacterController).\n" +
             "Règle cette valeur pour que la caméra soit au niveau des yeux du mesh.")]
    public float eyeHeight = 1.65f;
    [Tooltip("Décalage vers l'avant depuis le centre du joueur.\n" +
             "Augmente si le front du personnage entre encore dans le champ.")]
    public float eyeForwardOffset = 0.1f;

    [Header("Near clip plane")]
    [Tooltip("Near clip en vue ISO (normal, ex: 0.3)")]
    public float nearClipISO = 0.3f;
    [Tooltip("Near clip en vue FPS — réduit pour éviter le clipping du mesh proche.\n" +
             "Recommandé : 0.01 à 0.05.")]
    public float nearClipFPS = 0.02f;

    [Header("FPS — Souris")]
    public float mouseYawSensitivity = 0.15f;
    public float mousePitchSensitivity = 0.15f;
    public float pitchMin = -80f;
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

        if (isFPSMode && !wasFPS)
        {
            fpsPitch = 0f;
            cameraPivot.localRotation = Quaternion.identity;

            if (playerCamera != null)
            {
                // Snap instantané — pas de lerp, pas de traversée de mesh
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

    // -------------------------------------------------------------------------
    // FPS — mouse look
    // -------------------------------------------------------------------------

    private void HandleFPSMouseLook()
    {
        if (Mouse.current == null) return;

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

    // -------------------------------------------------------------------------
    // Position de la caméra
    // -------------------------------------------------------------------------

    private void UpdateCameraTransform()
    {
        if (playerCamera == null) return;

        if (isFPSMode)
        {
            // Lerp vers la position stable calculée depuis le root du joueur.
            // Les animations du squelette n'influencent PAS cette position —
            // seul le déplacement réel du CharacterController est suivi.
            playerCamera.transform.localPosition = Vector3.Lerp(
                playerCamera.transform.localPosition,
                ComputeFPSLocalPos(),
                Time.deltaTime * fpsSmoothSpeed);
        }
        else
        {
            playerCamera.transform.localPosition = Vector3.Lerp(
                playerCamera.transform.localPosition,
                new Vector3(0f, 0f, -currentDistance),
                Time.deltaTime * smoothSpeed);
        }

        playerCamera.transform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// Calcule la position cible de la caméra FPS en espace local du cameraPivot.
    ///
    /// On part du root du joueur (transform.position) et on monte de eyeHeight.
    /// Cette position est totalement indépendante du squelette et de ses animations :
    /// pas de bobbing, pas de secousses, juste le déplacement pur du CharacterController.
    ///
    /// eyeForwardOffset sort légèrement la caméra vers l'avant pour éviter
    /// que le front du mesh entre dans le champ.
    /// </summary>
    private Vector3 ComputeFPSLocalPos()
    {
        // Position monde stable : base du joueur + hauteur des yeux
        Vector3 worldPos = transform.position + Vector3.up * eyeHeight;

        // Léger décalage vers l'avant dans le plan horizontal
        if (eyeForwardOffset > 0f)
        {
            Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            worldPos += flatForward * eyeForwardOffset;
        }

        return cameraPivot.InverseTransformPoint(worldPos);
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
