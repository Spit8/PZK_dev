using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    // Variables de définition des touches
    public InputAction MoveAction;

    // Variables ajustable dans l'inspecteur
    public float walkSpeed = 1.0f;
    public float turnSpeed = 20f;

    // On créé les objets de mouvement, de rotation et d'animation (en réalité, ces composants sont dans l'inspector, on les déclare simplement ici)
    Animator m_Animator;
    Rigidbody m_Rigidbody;
    Vector3 m_Movement;
    Quaternion m_Rotation = Quaternion.identity;

    void Start()
    {
        // Au start(), on récupère les composants de rigidbody et d'animator, et on active les actions de mouvement
        m_Rigidbody = GetComponent<Rigidbody>();
        m_Animator = GetComponent<Animator>();
        MoveAction.Enable();
    }

    void FixedUpdate()
    {
        // à intervalle fixe, on lit les valeurs des touches
        var pos = MoveAction.ReadValue<Vector2>();

        // on sépare les valeurs horizontales et verticales
        float horizontal = pos.x;
        float vertical = pos.y;

        // On vérifie si il y a un mouvement horizontal ou vertical
        bool hasHorizontalInput = !Mathf.Approximately(horizontal, 0f);
        bool hasVerticalInput = !Mathf.Approximately(vertical, 0f);
        // On déclare un booléen, résultat de mouvement  horizontal OU LOGIC mouvement vertial
        bool isWalking = hasHorizontalInput || hasVerticalInput;
        // On transmet le booléen à l'animator pour faire marcher le personnage
        m_Animator.SetBool("isWalking", isWalking);

        // On crée un vecteur de mouvement à partir des valeurs horizontales et verticales, puis on le normalise
        m_Movement.Set(horizontal, 0f, vertical);
        m_Movement.Normalize();

        // On crée un vecteur de direction à partir du vecteur de mouvement, et on utilise RotateTowards pour faire tourner le personnage vers la direction du mouvement
        Vector3 desiredForward = Vector3.RotateTowards(transform.forward, m_Movement, turnSpeed * Time.deltaTime, 0f);
        // On crée une rotation à partir du vecteur de direction
        m_Rotation = Quaternion.LookRotation(desiredForward);

        // On utilise MoveRotation et MovePosition pour faire bouger le personnage en fonction du vecteur de mouvement, de la vitesse de marche et du temps écoulé
        m_Rigidbody.MoveRotation(m_Rotation);
        m_Rigidbody.MovePosition(m_Rigidbody.position + m_Movement * walkSpeed * Time.deltaTime);
    }
}