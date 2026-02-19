using Mirror;
using UnityEngine;

/// <summary>
/// À placer sur le prefab joueur.
/// Gère le ramassage et le lâché d'objets.
/// </summary>
public class PlayerInventory : NetworkBehaviour
{
    [Header("Références")]
    [Tooltip("Transform enfant représentant la main (ex: RightHand)")]
    public Transform handSlot;

    [Header("Configuration")]
    [Tooltip("Distance maximale pour ramasser un objet")]
    public float pickupRange = 2f;
    [Tooltip("Touche pour ramasser / lâcher")]
    public KeyCode pickupKey = KeyCode.E;
    [Tooltip("Layers sur lesquels chercher des objets ramassables")]
    public LayerMask pickupLayerMask = Physics.DefaultRaycastLayers;

    // L'objet actuellement tenu (référence locale, pas synchronisée)
    private PickupItem currentItem;

    // -------------------------------------------------------
    // UNITY LIFECYCLE
    // -------------------------------------------------------

    void Update()
    {
        // Seul le joueur local gère les inputs
        if (!isLocalPlayer) return;

        if (Input.GetKeyDown(pickupKey))
        {
            if (currentItem == null)
                TryPickup();
            else
                TryDrop();
        }
    }

    // -------------------------------------------------------
    // RAMASSAGE
    // -------------------------------------------------------

    private void TryPickup()
    {
        // Cherche tous les objets ramassables dans le rayon
        Collider[] hits = Physics.OverlapSphere(transform.position, pickupRange, pickupLayerMask);

        PickupItem closest = null;
        float closestDist = Mathf.Infinity;

        foreach (var hit in hits)
        {
            PickupItem item = hit.GetComponent<PickupItem>();
            if (item == null || item.IsHeld) continue;

            float dist = Vector3.Distance(transform.position, item.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = item;
            }
        }

        if (closest != null)
        {
            closest.CmdPickup(netIdentity);
            currentItem = closest;
        }
    }

    // -------------------------------------------------------
    // LÂCHÉ
    // -------------------------------------------------------

    private void TryDrop()
    {
        if (currentItem == null) return;

        currentItem.CmdDrop();
        currentItem = null;
    }

    // -------------------------------------------------------
    // UTILITAIRE
    // -------------------------------------------------------

    /// <summary>Retourne l'objet actuellement tenu, ou null.</summary>
    public PickupItem GetCurrentItem() => currentItem;

    // Dessine le rayon de ramassage dans l'éditeur
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRange);
    }
}
