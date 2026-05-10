using UnityEngine;
using Mirror;

/// <summary>
/// Caméra hybride TPS (Third-Person) / FPS — Projet PZK.
///
/// Fonctionnement :
/// - Scroll molette = zoom in/out (orbite autour du joueur)
/// - En dessous de fpsThreshold → FPS (caméra yeux, mouselook classique)
/// - Au-dessus → TPS (caméra derrière, over-the-shoulder)
/// - La souris contrôle TOUJOURS la rotation du personnage (yaw) + la caméra (pitch)
/// - Mouvement WASD toujours relatif au forward du personnage (strafe)
/// - Collision caméra TPS avec le décor (évite de traverser les murs)
/// </summary>
public class PlayerCameraController : NetworkBehaviour
{
    [Header("Références")]
    public Transform cameraPivot;
    public Camera playerCamera;

    [Header("Zoom / Distance")]
    [Tooltip("Distance minimale (0 = FPS)")]
    public float minDistance = 0f;
    [Tooltip("Distance maximale TPS")]
    public float maxDistance = 6f;
    [Tooltip("Distance courante (modifiable à runtime par DamageFeedback, etc.)")]
    public float currentDistance = 3f;
    [Tooltip("Vitesse du zoom molette")]
    public float zoomSpeed = 0.5f;
    [Tooltip("Seuil en-dessous duquel on passe en FPS")]
    public float fpsThreshold = 0.5f;

    [Header("Mouselook")]
    [Tooltip("Sensibilité horizontale (yaw)")]
    public float yawSensitivity = 2.0f;
    [Tooltip("Sensibilité verticale (pitch)")]
    public float pitchSensitivity = 2.0f;
    [Tooltip("Angle pitch minimum (regarder en bas)")]
    public float pitchMin = -60f;
    [Tooltip("Angle pitch maximum (regarder en haut)")]
    public float pitchMax = 75f;

    [Header("TPS — Offset")]
    [Tooltip("Décalage latéral (over-the-shoulder). Positif = droite.")]
    public float shoulderOffsetX = 0.4f;
    [Tooltip("Hauteur du pivot par rapport au joueur")]
    public float pivotHeight = 1.5f;

    [Header("FPS — Position")]
    [Tooltip("Hauteur des yeux en FPS")]
    public float eyeHeight = 1.65f;
    [Tooltip("Offset avant (évite de voir l'intérieur du mesh)")]
    public float eyeForwardOffset = 0.1f;

    [Header("Collision TPS")]
    [Tooltip("Active la collision caméra avec le décor")]
    public bool enableCameraCollision = true;
    [Tooltip("Rayon de la sphère de collision caméra")]
    public float collisionRadius = 0.2f;
    [Tooltip("Layers bloquant la caméra")]
    public LayerMask collisionMask = ~0;

    [Header("Smoothing")]
    public float positionSmoothSpeed = 15f;
    public float nearClipFPS = 0.01f;
    public float nearClipTPS = 0.1f;

    // ─────────────────────────────────────────────────────────────────────────
    // État interne
    // ─────────────────────────────────────────────────────────────────────────

    private bool isFPSMode = false;
    private float currentPitch = 0f;
    private float currentYaw = 0f;
    private PlayerUIController uiController;
    private PlayerInputHandler inputHandler;

    // ─────────────────────────────────────────────────────────────────────────
    // API publique
    // ─────────────────────────────────────────────────────────────────────────

    public bool IsInFPSMode() => isFPSMode;
    public bool IsMouseLocked() => Cursor.lockState == CursorLockMode.Locked;

    public Ray GetLookRay()
    {
        if (playerCamera == null) return new Ray(transform.position + Vector3.up * eyeHeight, transform.forward);
        return new Ray(playerCamera.transform.position, playerCamera.transform.forward);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Initialisation
    // ─────────────────────────────────────────────────────────────────────────

    public override void OnStartLocalPlayer()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        uiController = GetComponent<PlayerUIController>();
        inputHandler = GetComponent<PlayerInputHandler>();

        currentYaw = transform.eulerAngles.y;

        if (playerCamera != null && cameraPivot != null)
        {
            playerCamera.transform.SetParent(cameraPivot);
            playerCamera.transform.localPosition = Vector3.zero;
            playerCamera.transform.localRotation = Quaternion.identity;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Start()
    {
        if (isLocalPlayer)
        {
            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>(true);

            if (playerCamera != null)
            {
                playerCamera.enabled = true;
                playerCamera.tag = "MainCamera";

                AudioListener listener = playerCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = true;
            }
            return;
        }

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (playerCamera != null)
        {
            playerCamera.enabled = false;
            AudioListener listener = playerCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Boucle principale (LateUpdate pour suivre le mouvement)
    // ─────────────────────────────────────────────────────────────────────────

    private void LateUpdate()
    {
        if (!isLocalPlayer || cameraPivot == null || playerCamera == null) return;

        HandleZoom();
        HandleMouseLook();
        UpdateMode();
        UpdateCameraPosition();
    }

    private void HandleZoom()
    {
        if (inputHandler == null) return;

        float scroll = inputHandler.ScrollDelta;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            currentDistance -= Mathf.Sign(scroll) * zoomSpeed;
            currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
        }
    }

    private void HandleMouseLook()
    {
        if (inputHandler == null) return;

        Vector2 delta = inputHandler.MouseDelta;

        currentYaw += delta.x * yawSensitivity;
        currentPitch -= delta.y * pitchSensitivity;
        currentPitch = Mathf.Clamp(currentPitch, pitchMin, pitchMax);

        transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
        cameraPivot.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
    }

    private void UpdateMode()
    {
        bool wasFPS = isFPSMode;
        isFPSMode = currentDistance < fpsThreshold;

        if (isFPSMode != wasFPS)
        {
            playerCamera.nearClipPlane = isFPSMode ? nearClipFPS : nearClipTPS;
            if (uiController != null) uiController.RefreshCursorState();
        }
    }

    private void UpdateCameraPosition()
    {
        Vector3 pivotWorldPos = transform.position + Vector3.up * pivotHeight;
        cameraPivot.position = pivotWorldPos;

        if (isFPSMode)
        {
            Vector3 fpsPos = transform.position + Vector3.up * eyeHeight;
            fpsPos += transform.forward * eyeForwardOffset;

            playerCamera.transform.position = Vector3.Lerp(
                playerCamera.transform.position, fpsPos, Time.deltaTime * positionSmoothSpeed);
            playerCamera.transform.rotation = cameraPivot.rotation;
        }
        else
        {
            float shoulderX = shoulderOffsetX;
            Vector3 desiredOffset = cameraPivot.rotation * new Vector3(shoulderX, 0f, -currentDistance);
            Vector3 desiredPos = pivotWorldPos + desiredOffset;

            if (enableCameraCollision)
            {
                desiredPos = ApplyCollision(pivotWorldPos, desiredPos);
            }

            playerCamera.transform.position = Vector3.Lerp(
                playerCamera.transform.position, desiredPos, Time.deltaTime * positionSmoothSpeed);
            playerCamera.transform.rotation = cameraPivot.rotation;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Collision TPS — Empêche la caméra de traverser les murs
    // ─────────────────────────────────────────────────────────────────────────

    private Vector3 ApplyCollision(Vector3 pivotPos, Vector3 desiredPos)
    {
        Vector3 direction = desiredPos - pivotPos;
        float maxDist = direction.magnitude;

        if (maxDist < 0.01f) return desiredPos;

        RaycastHit hit;
        if (Physics.SphereCast(pivotPos, collisionRadius, direction.normalized, out hit, maxDist, collisionMask, QueryTriggerInteraction.Ignore))
        {
            float safeDistance = hit.distance - collisionRadius;
            if (safeDistance < 0f) safeDistance = 0f;
            return pivotPos + direction.normalized * safeDistance;
        }

        return desiredPos;
    }
}
