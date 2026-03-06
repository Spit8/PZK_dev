using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Déplacement hybride ISO / FPS - Projet PZK.
/// Sécurité Mirror ajoutée pour éviter les erreurs RPC au démarrage.
/// </summary>
public class PlayerMovement : NetworkBehaviour
{
    [Header("Actions de mouvement")]
    public InputAction MoveAction;
    public InputAction RunAction;

    [Header("Vitesses")]
    public float walkSpeed = 2.0f;
    public float runSpeed = 4.0f;
    public float aimWalkSpeed = 1.2f;
    public float gravity = -9.81f;

    [Header("Rotation")]
    public float isoTurnSpeed = 10f;
    public float isoStationaryTurnSpeed = 120f;

    [Header("Push des objets")]
    public float pushForce = 5f;
    public LayerMask pushLayers;

    private CharacterController cc;
    private Animator m_Animator;
    private PlayerCameraController cameraController;

    private float verticalVelocity = 0f;
    private Vector3 frozenMoveDirection = Vector3.zero;

    // Rotation cible capturée une seule fois au début d'un turn pur
    private Quaternion turnTargetRotation;
    private bool isTurning = false;
    private float turnClipDuration = 28f / 30f; // 28 frames à 30fps

    private void Start()
    {
        cc = GetComponent<CharacterController>();
        m_Animator = GetComponent<Animator>();
        cameraController = GetComponent<PlayerCameraController>();

        if (isLocalPlayer)
        {
            MoveAction.Enable();
            RunAction.Enable();
        }
    }

    private void Update()
    {
        if (!isLocalPlayer || !NetworkClient.active) return;

        Vector2 input = MoveAction.ReadValue<Vector2>();
        bool isRunning = RunAction.ReadValue<float>() > 0f;
        bool isMoving = input.sqrMagnitude > 0.01f;
        bool inFPS = cameraController != null && cameraController.IsInFPSMode();
        bool isAiming = Mouse.current != null && Mouse.current.rightButton.isPressed;

        if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        else verticalVelocity += gravity * Time.deltaTime;

        float currentSpeed = isMoving
            ? (isAiming ? aimWalkSpeed : (isRunning ? runSpeed : walkSpeed))
            : 0f;

        if (inFPS)
            UpdateFPS(input, currentSpeed, isMoving, isRunning);
        else
            UpdateISO(input, currentSpeed, isMoving, isRunning, isAiming);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FPS
    // ─────────────────────────────────────────────────────────────────────────

    private void UpdateFPS(Vector2 input, float currentSpeed, bool isMoving, bool isRunning)
    {
        isTurning = false;

        Vector3 moveDir = transform.forward * input.y + transform.right * input.x;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        cc.Move((moveDir * currentSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);

        if (m_Animator != null)
        {
            m_Animator.SetBool("isWalking", Mathf.Abs(input.y) > 0.1f);
            m_Animator.SetBool("isRunning", isMoving && isRunning);
            m_Animator.SetBool("isTurningLeft", false);
            m_Animator.SetBool("isTurningRight", false);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ISO
    // ─────────────────────────────────────────────────────────────────────────

    private void UpdateISO(Vector2 input, float currentSpeed, bool isMoving, bool isRunning, bool isAiming)
    {
        bool isPureLateral = Mathf.Abs(input.y) < 0.1f && Mathf.Abs(input.x) > 0.1f;

        // Capture la direction AVANT toute modification de transform.rotation.
        // Pendant un turn pur on conserve la dernière direction forward valide.
        if (isMoving && !isPureLateral)
        {
            frozenMoveDirection = transform.forward * input.y + transform.right * input.x;
            if (frozenMoveDirection.sqrMagnitude > 1f) frozenMoveDirection.Normalize();
        }
        else if (!isMoving)
        {
            frozenMoveDirection = Vector3.zero;
            isTurning = false;
        }

        // ── Rotation ─────────────────────────────────────────────────────────

        if (isAiming)
        {
            isTurning = false;
            RotateTowardsMouseCursor();
        }
        else if (isPureLateral && isMoving)
        {
            isTurning = false;
            float yawDelta = input.x * isoTurnSpeed * 180f * Time.deltaTime;
            transform.Rotate(0f, yawDelta, 0f);
        }
        else if (isMoving)
        {
            isTurning = false;
            Quaternion targetRot = Quaternion.LookRotation(frozenMoveDirection);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, isoTurnSpeed * 180f * Time.deltaTime);
        }
        else
        {
            isTurning = false;
            if (Mathf.Abs(input.x) > 0.01f)
            {
                float yawDelta = input.x * isoStationaryTurnSpeed * Time.deltaTime;
                transform.Rotate(0f, yawDelta, 0f);
            }
        }

        // ── Déplacement ───────────────────────────────────────────────────────
        float appliedSpeed = (isMoving && !isPureLateral) ? currentSpeed : 0f;
        cc.Move((frozenMoveDirection * appliedSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);

        // ── Animator ─────────────────────────────────────────────────────────
        if (m_Animator != null)
        {
            bool isWalkingFwd = Mathf.Abs(input.y) > 0.1f;

            m_Animator.SetBool("isWalking", isWalkingFwd);
            m_Animator.SetBool("isRunning", isWalkingFwd && isRunning && !isAiming);
            m_Animator.SetBool("isTurningLeft", isPureLateral && input.x < -0.1f);
            m_Animator.SetBool("isTurningRight", isPureLateral && input.x > 0.1f);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Utilitaires
    // ─────────────────────────────────────────────────────────────────────────

    private void RotateTowardsMouseCursor()
    {
        if (cameraController == null || cameraController.playerCamera == null) return;

        Ray ray = cameraController.playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane groundPlane = new Plane(Vector3.up, transform.position);

        if (groundPlane.Raycast(ray, out float dist))
        {
            Vector3 lookDir = ray.GetPoint(dist) - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(lookDir);
        }
    }
}
