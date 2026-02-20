using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Touches")]
    public InputAction MoveAction;
    public InputAction RunAction;

    [Header("Configuration")]
    public float walkSpeed = 1.0f;
    public float runSpeed = 1.5f;
    public float turnSpeed = 20f;
    public float gravity = -9.81f;

    [Header("Push des objets")]
    public float pushForce = 50f;
    public LayerMask pushLayers; // Mets "Pickable" ici dans l'inspector

    private CharacterController m_CharacterController;

    // Composants
    CharacterController cc;
    Animator m_Animator;

    // Mouvement
    public Vector3 m_Movement;
    Quaternion m_Rotation = Quaternion.identity;
    float verticalVelocity = 0f;

    void Start()
    {
        m_CharacterController = GetComponent<CharacterController>();
        cc = GetComponent<CharacterController>();
        m_Animator = GetComponent<Animator>();
        MoveAction.Enable();
        RunAction.Enable();
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        bool isGrounded = cc.isGrounded || Physics.Raycast(transform.position, Vector3.down, 0.2f);
        var pos = MoveAction.ReadValue<Vector2>();
        float horizontal = pos.x;
        float vertical = pos.y;
        bool hasHorizontalInput = !Mathf.Approximately(horizontal, 0f);
        bool hasVerticalInput = !Mathf.Approximately(vertical, 0f);
        bool isWalking = hasHorizontalInput || hasVerticalInput;
        bool isRunning = isWalking && RunAction.ReadValue<float>() > 0;

        m_Animator.SetBool("isWalking", isWalking);
        m_Animator.SetBool("isRunning", isRunning);

        m_Movement.Set(horizontal, 0f, vertical);
        m_Movement.Normalize();

        if (isWalking)
        {
            Vector3 desiredForward = Vector3.RotateTowards(transform.forward, m_Movement, turnSpeed * Time.deltaTime, 0f);
            m_Rotation = Quaternion.LookRotation(desiredForward);
            transform.rotation = m_Rotation;
        }

        if (isGrounded)
            verticalVelocity = -9.81f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        float currentSpeed = isWalking ? (isRunning ? runSpeed : walkSpeed) : 0f;
        Vector3 move = m_Movement * currentSpeed + Vector3.up * verticalVelocity;
        cc.Move(move * Time.deltaTime);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody rb = hit.collider.attachedRigidbody;
        if (rb == null || rb.isKinematic) return;

        if ((pushLayers & (1 << hit.gameObject.layer)) == 0) return;

        // Pousse l'objet
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        rb.AddForce(pushDir * pushForce, ForceMode.Force);

        // Ignore la collision pour éviter que le joueur monte dessus
        // Physics.IgnoreCollision(cc.GetComponent<Collider>(), hit.collider, true);
        // StartCoroutine(ReenableCollision(hit.collider));
    }

    /*private IEnumerator ReenableCollision(Collider other)
    {
        yield return new WaitForSeconds(0.1f);
        if (other != null)
            Physics.IgnoreCollision(cc.GetComponent<Collider>(), other, false);
    }*/

    void OnTriggerEnter(Collider other)
    {
        if ((pushLayers & (1 << other.gameObject.layer)) == 0) return;

        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        // Annule la vélocité vers le joueur pour absorber l'impact
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

}
