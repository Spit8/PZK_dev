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
    [Tooltip("Position de l'objet dans la main (dans l'espace du joueur, pas du bone)")]
    public Vector3 heldLocalPosition = Vector3.zero;
    [Tooltip("Rotation de l'objet dans la main (dans l'espace du joueur)")]
    public Vector3 heldLocalRotation = Vector3.zero;

    [SyncVar(hook = nameof(OnHeldByChanged))]
    private uint heldByNetId = 0;

    private Rigidbody rb;
    private Collider[] cols;
    private NetworkTransformReliable nt;

    // Références pour le suivi de la main
    private Transform _targetHand = null;       // Le bone MixamoRig:RightHand
    private Transform _holderTransform = null;  // La racine du joueur

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
        transform.position = dropPosition;
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

        // Annule la vélocité quand l'objet touche un joueur
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    // LateUpdate : s'exécute APRÈS l'Animator, donc le bone est déjà à sa position finale
    void LateUpdate()
    {
        if (!IsHeld || _targetHand == null || _holderTransform == null) return;

        // Position : bone + offset dans l'espace du bone cette fois
        transform.position = _targetHand.TransformPoint(heldLocalPosition);

        // Rotation : rotation du bone + offset
        transform.rotation = _targetHand.rotation * Quaternion.Euler(heldLocalRotation);
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

        // Désactive le NetworkTransform : c'est LateUpdate qui gère le positionnement
        if (nt != null) nt.enabled = false;

        // Stocke les références pour LateUpdate
        _targetHand = inventory.handSlot;           // Le bone MixamoRig:RightHand
        _holderTransform = holderIdentity.transform; // La racine du joueur
    }

    private void Detach()
    {
        // Réinitialise les références
        _targetHand = null;
        _holderTransform = null;

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
        StartCoroutine(ReenableColliders());
    }

    private IEnumerator ReenableColliders()
    {
        Debug.Log("ReenableColliders started");
        yield return new WaitForSeconds(0.2f);
        Debug.Log("ReenableColliders enabling");
        foreach (var c in cols) c.enabled = true;
    }
}
