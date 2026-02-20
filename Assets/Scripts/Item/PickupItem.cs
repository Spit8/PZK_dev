using Mirror;
using System.Collections;
using UnityEngine;

/// <summary>
/// À placer sur chaque objet ramassable.
/// Requiert : NetworkIdentity, Rigidbody
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(Rigidbody))]
public class PickupItem : NetworkBehaviour
{
    [Header("Configuration")]
    [Tooltip("Position locale de l'objet dans la main une fois ramassé")]
    public Vector3 heldLocalPosition = Vector3.zero;
    [Tooltip("Rotation locale de l'objet dans la main une fois ramassé")]
    public Vector3 heldLocalRotation = Vector3.zero;

    [SyncVar(hook = nameof(OnHeldByChanged))]
    private uint heldByNetId = 0;

    private Rigidbody rb;
    private Collider[] cols;
    private NetworkTransformReliable nt;

    public bool IsHeld => heldByNetId != 0;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cols = GetComponentsInChildren<Collider>();
        nt = GetComponent<NetworkTransformReliable>();
    }

    // -------------------------------------------------------
    // COMMANDES (client → serveur)
    // -------------------------------------------------------

    [Command(requiresAuthority = false)]
    public void CmdPickup(NetworkIdentity pickerIdentity)
    {
        if (heldByNetId != 0) return;
        netIdentity.AssignClientAuthority(pickerIdentity.connectionToClient);
        heldByNetId = pickerIdentity.netId;
    }

    [Command]
    public void CmdDrop(Vector3 dropPosition)
    {
        transform.position = dropPosition; // Le serveur déplace l'objet
        heldByNetId = 0;
        netIdentity.RemoveClientAuthority();
    }

    // -------------------------------------------------------
    // HOOK SYNCVAR
    // -------------------------------------------------------

    private void OnHeldByChanged(uint oldNetId, uint newNetId)
    {
        if (newNetId != 0)
        {
            if (NetworkClient.spawned.TryGetValue(newNetId, out NetworkIdentity holderIdentity))
                AttachToHolder(holderIdentity);
            else
                StartCoroutine(RetryAttach(newNetId));
        }
        else
        {
            Detach();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerInventory>() == null) return;

        // Annule la vélocité quand l'extincteur touche un joueur
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    void Update()
    {
        if (IsHeld && transform.parent != null)
        {
            transform.localPosition = heldLocalPosition;
            transform.localRotation = Quaternion.Euler(heldLocalRotation);
        }

    }

    private IEnumerator RetryAttach(uint netId)
    {
        yield return null;
        if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity holderIdentity))
            AttachToHolder(holderIdentity);
    }

    // -------------------------------------------------------
    // ATTACHEMENT / DÉTACHEMENT
    // -------------------------------------------------------

    private void AttachToHolder(NetworkIdentity holderIdentity)
    {
        PlayerInventory inventory = holderIdentity.GetComponent<PlayerInventory>();
        if (inventory == null || inventory.handSlot == null) return;

        // Désactive la physique
        rb.isKinematic = true;
        rb.useGravity = false;

        // Désactive les colliders pendant qu'il est tenu
        foreach (var c in cols) c.enabled = false;

        // Désactive le NetworkTransform (le parenting suffit)
        if (nt != null) nt.enabled = false;

        // Attache à la main
        transform.SetParent(inventory.handSlot);
        transform.localPosition = heldLocalPosition;
        transform.localRotation = Quaternion.Euler(heldLocalRotation);
    }

    private void Detach()
    {
        transform.SetParent(null);

        foreach (var c in cols) c.enabled = false; // Garde désactivé d'abord

        if (isServer)
        {
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.None;
        }
        else
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (nt != null) nt.enabled = true;

        // Réactive les colliders après un court délai
        // pour laisser le temps à l'objet de s'éloigner du joueur
        StartCoroutine(ReenableColliders());
    }

    private IEnumerator ReenableColliders()
    {
        yield return new WaitForSeconds(0.2f);
        foreach (var c in cols) c.enabled = true;
    }
}
