using Mirror;
using UnityEngine;
using System.Collections;

/// <summary>
/// NetworkManager custom du projet PZK.
/// Gère le cycle de vie des joueurs : connexion, déconnexion (nettoyage items),
/// et respawn server-authoritative après la mort.
/// </summary>
public class PZKNetworkManager : NetworkManager
{
    [Header("Respawn")]
    [SerializeField]
    [Tooltip("Délai en secondes avant le respawn d'un joueur mort.")]
    private float respawnDelay = 5.0f;

    [SerializeField]
    [Tooltip("Points de spawn supplémentaires. Les NetworkStartPosition de Mirror sont utilisés en priorité.")]
    private Transform[] spawnPoints;

    public float RespawnDelay => respawnDelay;

    public override void OnStartServer()
    {
        base.OnStartServer();
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient connection)
    {
        base.OnServerAddPlayer(connection);

        if (connection.identity != null)
        {
            GameSessionManager session = GameSessionManager.Instance;
            if (session != null)
            {
                session.ServerRegisterPlayer(connection.identity);
            }
        }
    }

    public override void OnServerDisconnect(NetworkConnectionToClient connection)
    {
        if (connection != null && connection.identity != null)
        {
            uint disconnectedNetId = connection.identity.netId;

            GameSessionManager session = GameSessionManager.Instance;
            if (session != null)
            {
                session.ServerUnregisterPlayer(disconnectedNetId);
            }

            ReleaseItemsHeldBy(disconnectedNetId);
        }

        base.OnServerDisconnect(connection);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Respawn
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Démarre le processus de respawn pour un joueur mort.
    /// Appelé par PlayerHealth.ServerInitiateDeath() côté serveur.
    /// </summary>
    [Server]
    public void RequestRespawn(NetworkConnectionToClient connection)
    {
        if (connection == null)
        {
            return;
        }

        StartCoroutine(RespawnAfterDelay(connection));
    }

    [Server]
    private IEnumerator RespawnAfterDelay(NetworkConnectionToClient connection)
    {
        yield return new WaitForSeconds(respawnDelay);

        if (connection == null || connection.identity == null)
        {
            yield break;
        }

        PlayerHealth health = connection.identity.GetComponent<PlayerHealth>();
        if (health != null)
        {
            Vector3 spawnPosition = GetSpawnPosition();
            health.ServerRespawn(spawnPosition);
        }
    }

    /// <summary>
    /// Retourne une position de spawn aléatoire.
    /// Priorité : NetworkStartPosition de Mirror > spawnPoints Inspector > fallback.
    /// </summary>
    public Vector3 GetSpawnPosition()
    {
        if (startPositions.Count > 0)
        {
            int index = UnityEngine.Random.Range(0, startPositions.Count);
            if (startPositions[index] != null)
            {
                return startPositions[index].position;
            }
        }

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int index = UnityEngine.Random.Range(0, spawnPoints.Length);
            if (spawnPoints[index] != null)
            {
                return spawnPoints[index].position;
            }
        }

        return new Vector3(0f, 2f, 0f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cleanup
    // ─────────────────────────────────────────────────────────────────────────

    private void ReleaseItemsHeldBy(uint holderNetId)
    {
        if (!NetworkServer.active)
        {
            return;
        }

        PickupItem[] pickupItems = FindObjectsByType<PickupItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < pickupItems.Length; i++)
        {
            PickupItem pickupItem = pickupItems[i];
            if (pickupItem != null)
            {
                pickupItem.ServerForceReleaseIfHeldBy(holderNetId);
            }
        }
    }
}
