using Mirror;
using System.Collections;
using UnityEngine;

/// <summary>
/// À placer sur chaque objet ramassable.
/// Requiert : NetworkIdentity, Rigidbody, Collider
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PickupItem : NetworkBehaviour
{
    [Header("Configuration")]
    [Tooltip("Position locale de l'objet dans la main une fois ramassé")]
    public Vector3 heldLocalPosition = Vector3.zero;
    [Tooltip("Rotation locale de l'objet dans la main une fois ramassé")]
    public Vector3 heldLocalRotation = Vector3.zero;

    // SyncVar : quand cette valeur change côté serveur,
    // le hook OnHeldByChanged est automatiquement appelé sur tous les clients
    [SyncVar(hook = nameof(OnHeldByChanged))]
    private uint heldByNetId = 0;

    private Rigidbody rb;
    private Collider col;
    private NetworkTransformReliable nt; // ou NetworkTransform selon ta version de Mirror

    public bool IsHeld => heldByNetId != 0;

    void Awake()
    {
        rb  = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        nt  = GetComponent<NetworkTransformReliable>();
    }

    // -------------------------------------------------------
    // COMMANDES (client → serveur)
    // -------------------------------------------------------

    /// <summary>Appelé par le joueur qui veut ramasser l'objet.</summary>
    [Command(requiresAuthority = false)]
    public void CmdPickup(NetworkIdentity pickerIdentity)
    {
        // Vérifications serveur
        if (heldByNetId != 0) return; // Déjà tenu par quelqu'un

        // Donne l'authority réseau au joueur ramasseur
        // (lui permet de déplacer l'objet via NetworkTransform)
        netIdentity.AssignClientAuthority(pickerIdentity.connectionToClient);

        // La mise à jour de SyncVar déclenche OnHeldByChanged sur tous les clients
        heldByNetId = pickerIdentity.netId;
    }

    /// <summary>Appelé par le joueur qui tient l'objet et veut le lâcher.</summary>
    [Command]
    public void CmdDrop()
    {
        heldByNetId = 0;
        netIdentity.RemoveClientAuthority();
    }

    // -------------------------------------------------------
    // HOOK SYNCVAR — exécuté sur TOUS les clients (+ serveur)
    // -------------------------------------------------------

    private void OnHeldByChanged(uint oldNetId, uint newNetId)
    {
        if (newNetId != 0)
        {
            // Quelqu'un a ramassé l'objet
            if (NetworkClient.spawned.TryGetValue(newNetId, out NetworkIdentity holderIdentity))
            {
                AttachToHolder(holderIdentity);
            }
            else
            {
                // Le NetworkIdentity du porteur n'est pas encore spawné côté client
                // (cas rare mais possible) → on réessaie dans 1 frame
                StartCoroutine(RetryAttach(newNetId));
            }
        }
        else
        {
            // L'objet a été lâché
            Detach();
        }
    }

    private IEnumerator RetryAttach(uint netId)
    {
        yield return null; // attend 1 frame
        if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity holderIdentity))
        {
            AttachToHolder(holderIdentity);
        }
    }

    // -------------------------------------------------------
    // LOGIQUE D'ATTACHEMENT / DÉTACHEMENT
    // -------------------------------------------------------

    private void AttachToHolder(NetworkIdentity holderIdentity)
    {
        PlayerInventory inventory = holderIdentity.GetComponent<PlayerInventory>();
        if (inventory == null || inventory.handSlot == null) return;

        // Désactive la physique et le collider pendant qu'il est tenu
        rb.isKinematic = true;
        col.enabled = false;

        // Désactive la sync réseau du transform (le parenting suffit)
        if (nt != null) nt.enabled = false;

        // Attache l'objet à la main
        transform.SetParent(inventory.handSlot);
        transform.localPosition = heldLocalPosition;
        transform.localRotation = Quaternion.Euler(heldLocalRotation);
    }

    private void Detach()
    {
        // Détache du parent
        transform.SetParent(null);

        // Réactive la physique
        rb.isKinematic = false;
        col.enabled = true;

        // Réactive la sync réseau du transform
        if (nt != null) nt.enabled = true;
    }
}
