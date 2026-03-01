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

    [Header("Push des objets")]
    public float pushForce = 5f;
    public LayerMask pushLayers;

    private CharacterController cc;
    private Animator m_Animator;
    private PlayerCameraController cameraController;

    private float verticalVelocity = 0f;

    private void Start()
    {
        cc = GetComponent<CharacterController>();
        m_Animator = GetComponent<Animator>();
        cameraController = GetComponent<PlayerCameraController>();
        
        // On n'active les inputs que si c'est notre joueur
        if (isLocalPlayer)
        {
            MoveAction.Enable();
            RunAction.Enable();
        }
    }

    private void Update()
    {
        // SÉCURITÉ MIRROR : Empêche l'exécution si le client n'est pas prêt
        // ou si ce n'est pas le joueur local.
        if (!isLocalPlayer || !NetworkClient.active) return;

        Vector2 input = MoveAction.ReadValue<Vector2>();
        bool isRunning = RunAction.ReadValue<float>() > 0f;
        bool isMoving = input.sqrMagnitude > 0.01f;
        bool inFPS = cameraController != null && cameraController.IsInFPSMode();
        bool isAiming = Mouse.current.rightButton.isPressed;

        // Calcul du Mouvement relatif (Transition cohérente)
        Vector3 moveDirection = transform.forward * input.y + transform.right * input.x;
        if (moveDirection.sqrMagnitude > 1f) moveDirection.Normalize();

        // Gestion de la Rotation
        if (!inFPS)
        {
            if (isAiming)
            {
                RotateTowardsMouseCursor();
            }
            else if (isMoving)
            {
                Vector3 targetDir = (transform.forward * input.y + transform.right * input.x).normalized;
                if (targetDir != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(targetDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * isoTurnSpeed);
                }
            }
        }

        // Physique & Gravité
        if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        else verticalVelocity += gravity * Time.deltaTime;

        float currentSpeed = isMoving ? (isAiming ? aimWalkSpeed : (isRunning ? runSpeed : walkSpeed)) : 0f;

        // Application du mouvement
        cc.Move((moveDirection * currentSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);

        // Animations
        if (m_Animator != null)
        {
            m_Animator.SetBool("isWalking", isMoving);
            m_Animator.SetBool("isRunning", isMoving && isRunning && !isAiming);
        }
    }

    private void RotateTowardsMouseCursor()
    {
        if (cameraController.playerCamera == null) return;

        Ray ray = cameraController.playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane groundPlane = new Plane(Vector3.up, transform.position);

        if (groundPlane.Raycast(ray, out float dist))
        {
            Vector3 lookDir = (ray.GetPoint(dist) - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * isoTurnSpeed);
            }
        }
    }
}