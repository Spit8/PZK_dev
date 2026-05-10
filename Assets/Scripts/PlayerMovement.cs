using Mirror;
using UnityEngine;

/// <summary>
/// Déplacement TPS/FPS unifié — Projet PZK.
/// Le joueur se déplace toujours en strafe (WASD relatif à son forward).
/// La rotation est gérée par PlayerCameraController (mouselook).
/// Compatible avec ServerMovementValidator (client-authoritative + validation serveur).
/// </summary>
public class PlayerMovement : NetworkBehaviour
{
    [Header("Vitesses")]
    public float walkSpeed = 3.0f;
    public float runSpeed = 5.5f;
    public float gravity = -15f;

    private CharacterController cc;
    [SerializeField]
    private Animator playerAnimator;
    private PlayerCameraController cameraController;
    private PlayerInventory inventory;
    private PlayerHighlightObject highlighter;
    private PlayerInputHandler inputHandler;

    private float verticalVelocity = 0f;

    [SyncVar(hook = nameof(OnWalkingChanged))]
    private bool walking;

    [SyncVar(hook = nameof(OnRunningChanged))]
    private bool running;

    private void Awake()
    {
        if (playerAnimator == null)
        {
            playerAnimator = GetComponentInChildren<Animator>();
        }
    }

    private void Start()
    {
        cc = GetComponent<CharacterController>();
        cameraController = GetComponent<PlayerCameraController>();
        inventory = GetComponent<PlayerInventory>();
        highlighter = GetComponent<PlayerHighlightObject>();

        if (isLocalPlayer)
        {
            inputHandler = GetComponent<PlayerInputHandler>();
            SubscribeInput();
        }
    }

    private void OnEnable()
    {
        if (inputHandler != null)
        {
            SubscribeInput();
        }
    }

    private void OnDisable()
    {
        UnsubscribeInput();
    }

    private void OnDestroy()
    {
        UnsubscribeInput();
    }

    private void SubscribeInput()
    {
        if (inputHandler == null) return;
        inputHandler.OnInteractPressed -= HandleInteract;
        inputHandler.OnInteractPressed += HandleInteract;
    }

    private void UnsubscribeInput()
    {
        if (inputHandler == null) return;
        inputHandler.OnInteractPressed -= HandleInteract;
    }

    private void HandleInteract()
    {
        if (!NetworkClient.ready) return;

        if (inventory != null && highlighter != null)
        {
            GameObject hovered = highlighter.GetCurrentHovered();
            if (hovered != null)
            {
                NetworkIdentity netId = hovered.GetComponentInParent<NetworkIdentity>();
                if (netId != null)
                {
                    inventory.CmdPickupItem(netId);
                    highlighter.ClearUI();
                }
            }
        }
    }

    private void Update()
    {
        if (!isLocalPlayer || !NetworkClient.active || inputHandler == null) return;

        Vector2 input = inputHandler.MoveInput;
        bool isSprinting = inputHandler.IsSprintHeld;
        bool isMoving = input.sqrMagnitude > 0.01f;

        if (cc.isGrounded)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
            if (verticalVelocity < -20f) verticalVelocity = -20f;
        }

        float currentSpeed = isMoving ? (isSprinting ? runSpeed : walkSpeed) : 0f;

        bool isWalkingLocal = isMoving;
        bool isRunningLocal = isMoving && isSprinting;

        if (playerAnimator != null)
        {
            playerAnimator.SetBool("isWalking", isWalkingLocal);
            playerAnimator.SetBool("isRunning", isRunningLocal);
        }

        if (walking != isWalkingLocal || running != isRunningLocal)
        {
            CmdSetMovementState(isWalkingLocal, isRunningLocal);
        }

        Vector3 moveDir = transform.forward * input.y + transform.right * input.x;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        Vector3 finalMove = (moveDir * currentSpeed + Vector3.up * verticalVelocity) * Time.deltaTime;
        cc.Move(finalMove);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Network — Sync animation
    // ─────────────────────────────────────────────────────────────────────────

    [Command]
    private void CmdSetMovementState(bool isWalkingState, bool isRunningState)
    {
        walking = isWalkingState;
        running = isRunningState;
    }

    private void OnWalkingChanged(bool oldWalking, bool newWalking)
    {
        if (isLocalPlayer) return;

        if (playerAnimator != null)
        {
            playerAnimator.SetBool("isWalking", newWalking);
        }
    }

    private void OnRunningChanged(bool oldRunning, bool newRunning)
    {
        if (isLocalPlayer) return;

        if (playerAnimator != null)
        {
            playerAnimator.SetBool("isRunning", newRunning);
        }
    }
}
