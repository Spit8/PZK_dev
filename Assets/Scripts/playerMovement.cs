using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Déplacement hybride ISO / FPS.
///
/// VUE ISO
///   - ZQSD sur les axes fixes de la grille ISO
///   - Le personnage pivote visuellement vers sa direction de marche
///
/// VUE FPS
///   - Z/S  → avancer / reculer selon transform.forward (souris gère la direction)
///   - Q/D  → strafe gauche / droite
///   - Yaw  → géré par PlayerCameraController (souris X)
/// </summary>
public class PlayerMovement : NetworkBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Actions de mouvement")]
    public InputAction MoveAction;  // ZQSD → Vector2
    public InputAction RunAction;   // Shift → float

    [Header("Vitesses")]
    public float walkSpeed = 2.0f;
    public float runSpeed = 4.0f;
    public float gravity = -9.81f;

    [Header("Rotation ISO")]
    [Tooltip("Vitesse à laquelle le personnage pivote vers sa direction de marche en ISO")]
    public float isoTurnSpeed = 15f;

    [Header("Push des objets")]
    public float pushForce = 5f;
    public LayerMask pushLayers;

    // -------------------------------------------------------------------------
    // Composants
    // -------------------------------------------------------------------------

    private CharacterController cc;
    private Animator m_Animator;
    private PlayerCameraController cameraController;

    private float verticalVelocity = 0f;

    // -------------------------------------------------------------------------
    // Init
    // -------------------------------------------------------------------------

    private void Start()
    {
        cc = GetComponent<CharacterController>();
        m_Animator = GetComponent<Animator>();
        cameraController = GetComponent<PlayerCameraController>();

        MoveAction.Enable();
        RunAction.Enable();
    }

    // -------------------------------------------------------------------------
    // Boucle principale
    // -------------------------------------------------------------------------

    private void Update()
    {
        if (!isLocalPlayer) return;

        bool isGrounded = cc.isGrounded;
        Vector2 input = MoveAction.ReadValue<Vector2>();
        bool isRunning = RunAction.ReadValue<float>() > 0f;
        bool isMoving = input.sqrMagnitude > 0.01f;

        bool inFPS = cameraController != null && cameraController.IsInFPSMode();

        // ----------------------------------------------------------------
        // Direction de déplacement
        // ----------------------------------------------------------------
        Vector3 moveDirection = Vector3.zero;

        if (!inFPS)
        {
            // ── ISO : axes fixes de la grille ────────────────────────────
            if (isMoving)
            {
                moveDirection = cameraController.IsoForward * input.y
                              + cameraController.IsoRight * input.x;
                moveDirection.Normalize();

                // Pivote visuellement vers la direction de marche
                Quaternion targetRot = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRot, Time.deltaTime * isoTurnSpeed);
            }
        }
        else
        {
            // ── FPS : Z/S = avant/arrière, Q/D = strafe ─────────────────
            // Le yaw est entièrement géré par PlayerCameraController (souris X)
            // On ne touche pas à transform.rotation ici
            moveDirection = transform.forward * input.y
                          + transform.right * input.x;

            if (moveDirection.sqrMagnitude > 1f)
                moveDirection.Normalize();
        }

        // ----------------------------------------------------------------
        // Gravité
        // ----------------------------------------------------------------
        if (isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        // ----------------------------------------------------------------
        // Animations
        // ----------------------------------------------------------------
        if (m_Animator != null)
        {
            m_Animator.SetBool("isWalking", isMoving);
            m_Animator.SetBool("isRunning", isMoving && isRunning);
        }

        // ----------------------------------------------------------------
        // Application du mouvement
        // ----------------------------------------------------------------
        float speed = isMoving ? (isRunning ? runSpeed : walkSpeed) : 0f;

        cc.Move((moveDirection * speed + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    // -------------------------------------------------------------------------
    // Push d'objets physiques
    // -------------------------------------------------------------------------

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic) return;
        if (hit.moveDirection.y < -0.3f) return;
        if (hit.normal.y > 0.7f) return;
        if ((pushLayers & (1 << hit.gameObject.layer)) == 0) return;

        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        body.AddForce(pushDir * pushForce, ForceMode.Force);
    }

    private void OnTriggerEnter(Collider other)
    {
        if ((pushLayers & (1 << other.gameObject.layer)) == 0) return;
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}
