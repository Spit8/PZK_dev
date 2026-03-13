using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Système de combat de base, serveur-autoritaire.
/// Le client ne fait qu'envoyer une intention d'attaque via Command.
/// </summary>
public class WeaponSystem : NetworkBehaviour
{
    [Header("Combat")]
    [SerializeField]
    [Tooltip("Dégâts infligés par une attaque de base.")]
    private int baseDamage = 10;

    [SerializeField]
    [Tooltip("Rayon de la sphère de détection devant le joueur.")]
    private float attackRadius = 2.0f;

    [SerializeField]
    [Tooltip("Distance devant le joueur où la sphère est centrée.")]
    private float attackForwardOffset = 1.5f;
    [SerializeField]
    [Tooltip("Couches valides pour les cibles d'attaque (exclure le joueur local).")]
    private LayerMask attackMask;


    private void Update()
    {
        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log("DEBUG: Clic physique détecté");
        }

        if (!isLocalPlayer)
        {
            return;
        }

        if (UnityEngine.InputSystem.Mouse.current == null)
        {
            return;
        }

        if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log("DEBUG: Clic Joueur Local -> Envoi Cmd");
            CmdAttack();
        }
    }

    /// <summary>
    /// Commande envoyée par le client local pour déclencher une attaque.
    /// La logique de dégâts (raycast et validation de distance) est exécutée exclusivement sur le serveur.
    /// </summary>
    [Command]
    private void CmdAttack()
    {
        float maxDistance = attackRadius;

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        Vector3 origin = mainCamera.transform.position + (mainCamera.transform.forward * attackForwardOffset);
        Vector3 direction = mainCamera.transform.forward;

        int maskValue = attackMask;
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycastLayer >= 0)
        {
            maskValue &= ~(1 << ignoreRaycastLayer);
        }

        int selfLayer = gameObject.layer;
        maskValue &= ~(1 << selfLayer);

        Debug.DrawLine(origin, origin + direction * maxDistance, Color.red, 2.0f);

        RaycastHit hitInfo;
        bool hit = Physics.Raycast(origin, direction, out hitInfo, maxDistance, maskValue, QueryTriggerInteraction.Ignore);
        if (!hit)
        {
            return;
        }

        PlayerHealth targetHealth = hitInfo.collider.GetComponentInParent<PlayerHealth>();
        if (targetHealth == null)
        {
            return;
        }

        if (targetHealth.gameObject == gameObject)
        {
            return;
        }

        targetHealth.TakeDamage(baseDamage);
    }
}
