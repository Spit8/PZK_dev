using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

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
        if (!isLocalPlayer)
        {
            Debug.Log("Pas le joueur local, Update ignoré");
            return;
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            Debug.Log("Touche E détectée");

            if (currentItem == null)
            {
                Debug.Log("Tentative de ramassage...");
                TryPickup();
            }
            else
            {
                Debug.Log("Tentative de lâché...");
                TryDrop();
            }
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
            // On vérifie qu'il ne s'agit pas du joueur lui-même (ou d'un enfant du joueur)
            if (hit.transform.root == transform.root) continue;

            PickupItem item = hit.GetComponentInParent<PickupItem>();
            Debug.Log($"Objet trouvé : {hit.name}, PickupItem : {item != null}, IsHeld : {item?.IsHeld}");
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
            Debug.Log($"Ramassage de : {closest.name}");
            closest.CmdPickup(netIdentity);
            currentItem = closest;
        }
        else
        {
            Debug.Log("Aucun objet ramassable à proximité");
        }
    }

    // -------------------------------------------------------
    // LÂCHÉ
    // -------------------------------------------------------

    private void TryDrop()
    {
        if (currentItem == null) return;

        Vector3 dropPosition = transform.position + transform.forward * 0.3f + Vector3.up * 0.8f;

        currentItem.CmdDrop(dropPosition);
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
