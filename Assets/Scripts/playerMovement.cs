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
    public float walkSpeed = 2.0f; // Augmenté un peu pour PZK
    public float runSpeed = 4.0f;
    public float turnSpeed = 20f;
    public float gravity = -9.81f;

    [Header("Push des objets")]
    public float pushForce = 5f;
    public LayerMask pushLayers; // Mets "Pickable" ici dans l'inspector

    private CharacterController cc;

    // Composants
    Animator m_Animator;

    // Mouvement
    private Vector3 m_Movement;
    private float verticalVelocity = 0f;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        m_Animator = GetComponent<Animator>();
        
        MoveAction.Enable();
        RunAction.Enable();
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        // 1. Détection du sol
        bool isGrounded = cc.isGrounded;
        
        // 2. Lecture des Inputs
        Vector2 inputPos = MoveAction.ReadValue<Vector2>();
        bool isRunning = RunAction.ReadValue<float>() > 0;

        bool isWalking = inputPos.sqrMagnitude > 0.01f;

        // 3. Animation
        if (m_Animator != null)
        {
            m_Animator.SetBool("isWalking", isWalking);
            m_Animator.SetBool("isRunning", isWalking && isRunning);
        }

        // --- MODIFICATION POUR LA CAMÉRA ---
        // On calcule le mouvement par rapport à l'orientation du JOUEUR (transform.forward)
        // et non plus par rapport au monde (Vector3.forward)
        Vector3 moveDirection = transform.forward * inputPos.y + transform.right * inputPos.x;
        moveDirection.Normalize();
        // ------------------------------------

        // 4. Gestion de la Gravité
        if (isGrounded && verticalVelocity < 0)
            verticalVelocity = -2f; // Petite force pour rester collé au sol
        else
            verticalVelocity += gravity * Time.deltaTime;

        // 5. Vitesse et Application du mouvement
        float currentSpeed = isWalking ? (isRunning ? runSpeed : walkSpeed) : 0f;
        
        Vector3 finalMove = (moveDirection * currentSpeed) + (Vector3.up * verticalVelocity);
        
        cc.Move(finalMove * Time.deltaTime);
    }

    // Gestion du push d'objets (Inchangé)
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic) return;

        if (hit.moveDirection.y < -0.3f) return;

        // Garde suppl�mentaire : ignore si le contact vient du dessus
        if (hit.normal.y > 0.7f) return; // La normale pointe vers le haut = on est au-dessus

        // Filtre par layer
        if ((pushLayers & (1 << hit.gameObject.layer)) == 0) return;

        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
        body.AddForce(pushDir * pushForce, ForceMode.Force); // Force continue, pas instantan�e
    }

    /*private IEnumerator ReenableCollision(Collider other)
    {
        yield return new WaitForSeconds(0.2f);
        if (other != null)
            Physics.IgnoreCollision(cc, other, false);
    }*/

    void OnTriggerEnter(Collider other)
    {
        if ((pushLayers & (1 << other.gameObject.layer)) == 0) return;
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}