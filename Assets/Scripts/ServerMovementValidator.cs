using Mirror;
using UnityEngine;

/// <summary>
/// Valide côté serveur la vitesse de déplacement d'un joueur pour détecter les speedhacks.
/// Doit être attaché à un objet réseau représentant un joueur.
/// </summary>
public class ServerMovementValidator : NetworkBehaviour
{
    [SerializeField]
    [Tooltip("Vitesse maximale autorisée en m/s (runSpeed * 1.1f typiquement)")]
    private float maxAllowedSpeed = 7.0f;

    /// <summary>
    /// Dernière position connue côté serveur pour ce joueur.
    /// </summary>
    private Vector3 lastPosition;

    /// <summary>
    /// Initialisation côté serveur : on capture la position de départ pour éviter un pic artificiel au premier tick.
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        lastPosition = transform.position;
    }

    /// <summary>
    /// Validation de la distance parcourue à chaque tick physique côté serveur uniquement.
    /// </summary>
    [Server]
    private void FixedUpdate()
    {
        Vector3 currentPosition = transform.position;

        float distance = Vector3.Distance(currentPosition, lastPosition);
        float maxDistance = maxAllowedSpeed * Time.fixedDeltaTime;

        if (distance > maxDistance)
        {
            Debug.LogWarning($"[ServerMovementValidator] Suspicion de speedhack sur netId={netId} (distance={distance:F3}, maxDistance={maxDistance:F3})");
        }

        lastPosition = currentPosition;
    }
}
