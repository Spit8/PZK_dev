using Mirror;
using UnityEngine;

/// <summary>
/// Valide côté serveur la vitesse de déplacement d'un joueur pour détecter les speedhacks.
/// Applique un rubber-banding (correction de position) après un nombre configurable de violations.
/// Doit être attaché au prefab joueur à côté du CharacterController.
/// </summary>
public class ServerMovementValidator : NetworkBehaviour
{
    [Header("Speed Validation")]
    [SerializeField]
    [Tooltip("Vitesse maximale autorisée en m/s (runSpeed * 1.1f typiquement)")]
    private float maxAllowedSpeed = 7.0f;

    [SerializeField]
    [Tooltip("Distance horizontale absolue (mètres) par tick au-delà de laquelle un téléport est détecté")]
    private float teleportThreshold = 10.0f;

    [Header("Enforcement")]
    [SerializeField]
    [Tooltip("Nombre de violations avant correction de position (rubber-banding)")]
    private int violationsBeforeCorrection = 3;

    [SerializeField]
    [Tooltip("Intervalle (secondes) entre chaque décroissance naturelle du compteur de violations")]
    private float violationDecayInterval = 5.0f;

    private Vector3 lastPosition;
    private Vector3 lastValidPosition;
    private int violationCount;
    private float lastViolationDecayTime;
    private CharacterController cachedCharacterController;

    private void Awake()
    {
        cachedCharacterController = GetComponent<CharacterController>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        lastPosition = transform.position;
        lastValidPosition = transform.position;
        lastViolationDecayTime = Time.time;
    }

    [ServerCallback]
    private void FixedUpdate()
    {
        if (violationCount > 0 && Time.time > lastViolationDecayTime + violationDecayInterval)
        {
            violationCount--;
            lastViolationDecayTime = Time.time;
        }

        Vector3 currentPosition = transform.position;

        float deltaX = currentPosition.x - lastPosition.x;
        float deltaZ = currentPosition.z - lastPosition.z;
        float horizontalDistance = Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);

        float maxDistance = maxAllowedSpeed * Time.fixedDeltaTime;

        bool isViolation = false;

        if (horizontalDistance > teleportThreshold)
        {
            Debug.LogWarning($"[ServerMovementValidator] Teleport detected on netId={netId} (distance={horizontalDistance:F3}m)");
            violationCount = violationsBeforeCorrection;
            isViolation = true;
        }
        else if (horizontalDistance > maxDistance)
        {
            violationCount++;
            Debug.LogWarning($"[ServerMovementValidator] Speed violation #{violationCount} on netId={netId} (distance={horizontalDistance:F3}, max={maxDistance:F3})");
            isViolation = true;
        }

        if (!isViolation)
        {
            lastValidPosition = currentPosition;
        }

        if (violationCount >= violationsBeforeCorrection)
        {
            ForcePositionCorrection(lastValidPosition);
            violationCount = 0;
            lastViolationDecayTime = Time.time;
        }

        lastPosition = transform.position;
    }

    [Server]
    private void ForcePositionCorrection(Vector3 correctedPosition)
    {
        if (cachedCharacterController != null)
        {
            cachedCharacterController.enabled = false;
            transform.position = correctedPosition;
            cachedCharacterController.enabled = true;
        }
        else
        {
            transform.position = correctedPosition;
        }

        NetworkConnectionToClient ownerConnection = connectionToClient;
        if (ownerConnection != null)
        {
            TargetCorrectPosition(ownerConnection, correctedPosition);
        }
    }

    /// <summary>
    /// Envoie une correction de position au client propriétaire (rubber-banding).
    /// Le CharacterController est temporairement désactivé pour permettre le repositionnement.
    /// </summary>
    [TargetRpc]
    private void TargetCorrectPosition(NetworkConnectionToClient target, Vector3 correctedPosition)
    {
        if (cachedCharacterController != null)
        {
            cachedCharacterController.enabled = false;
            transform.position = correctedPosition;
            cachedCharacterController.enabled = true;
        }
        else
        {
            transform.position = correctedPosition;
        }
    }
}
