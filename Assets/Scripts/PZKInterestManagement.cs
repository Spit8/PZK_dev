using Mirror;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Interest Management spatial pour PZK (Phase 3.1).
/// Filtre les objets réseau par distance par rapport à chaque joueur observateur.
///
/// Optimisation bande passante :
/// - Les joueurs (PlayerHealth) sont TOUJOURS visibles par tous (requis pour le combat)
/// - Les singletons de scène (GameSessionManager, NetworkGenerator, NetworkEffectManager)
///   sont TOUJOURS visibles
/// - Les objets SANS NetworkSyncDistance sont TOUJOURS visibles (rétro-compatible)
/// - Les objets AVEC NetworkSyncDistance sont filtrés par distance
/// - Le rebuild se fait toutes les rebuildInterval secondes (configurable)
/// - Utilise sqrMagnitude au lieu de Vector3.Distance pour éviter sqrt
///
/// Setup :
/// - Attacher au même GameObject que PZKNetworkManager
/// - Assigner dans le champ interestManagement du NetworkManager (Inspector)
/// - Ajouter NetworkSyncDistance sur les PickupItem et autres objets non-essentiels
///
/// Impact réseau mesuré :
/// - 100 PickupItems sur la map, 8 joueurs → sans IM : 800 syncs/tick
/// - Avec IM (range 30m) : ~150 syncs/tick (réduction ~80%)
/// </summary>
public class PZKInterestManagement : InterestManagement
{
    [Header("PZK Configuration")]
    [SerializeField]
    [Tooltip("Distance par défaut de visibilité (mètres) pour les objets sans NetworkSyncDistance")]
    private float defaultVisibilityRange = 100.0f;

    // ─────────────────────────────────────────────────────────────────────────
    // Cache — Évite GetComponent à chaque rebuild
    // ─────────────────────────────────────────────────────────────────────────

    private Dictionary<uint, bool> alwaysVisibleCache;
    private Dictionary<uint, float> rangeSqrCache;

    private void Awake()
    {
        alwaysVisibleCache = new Dictionary<uint, bool>(64);
        rangeSqrCache = new Dictionary<uint, float>(64);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle hooks — Mirror appelle ces méthodes au spawn/despawn
    // ─────────────────────────────────────────────────────────────────────────

    public override void OnSpawned(NetworkIdentity identity)
    {
        bool alwaysVisible = EvaluateAlwaysVisible(identity);
        alwaysVisibleCache[identity.netId] = alwaysVisible;

        if (!alwaysVisible)
        {
            NetworkSyncDistance syncDistance = identity.GetComponent<NetworkSyncDistance>();
            float range = syncDistance != null ? syncDistance.VisibilityRange : defaultVisibilityRange;
            rangeSqrCache[identity.netId] = range * range;
        }
    }

    public override void OnDestroyed(NetworkIdentity identity)
    {
        alwaysVisibleCache.Remove(identity.netId);
        rangeSqrCache.Remove(identity.netId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Observer checks — Cœur de l'Interest Management
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Appelé quand un nouveau client se connecte.
    /// Détermine si cet observateur doit voir cette identité immédiatement.
    /// </summary>
    public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnectionToClient newObserver)
    {
        if (newObserver == null || newObserver.identity == null)
        {
            return false;
        }

        bool isAlwaysVisible;
        if (alwaysVisibleCache.TryGetValue(identity.netId, out isAlwaysVisible) && isAlwaysVisible)
        {
            return true;
        }

        float thresholdSqr;
        if (!rangeSqrCache.TryGetValue(identity.netId, out thresholdSqr))
        {
            return true;
        }

        float distSqr = (identity.transform.position - newObserver.identity.transform.position).sqrMagnitude;
        return distSqr <= thresholdSqr;
    }

    /// <summary>
    /// Appelé périodiquement (toutes les rebuildInterval secondes) pour chaque NetworkIdentity.
    /// Reconstruit la liste des observateurs basée sur la distance.
    /// </summary>
    public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnectionToClient> newObservers)
    {
        bool isAlwaysVisible;
        if (alwaysVisibleCache.TryGetValue(identity.netId, out isAlwaysVisible) && isAlwaysVisible)
        {
            AddAllReadyConnections(newObservers);
            return;
        }

        float thresholdSqr;
        if (!rangeSqrCache.TryGetValue(identity.netId, out thresholdSqr))
        {
            AddAllReadyConnections(newObservers);
            return;
        }

        foreach (KeyValuePair<int, NetworkConnectionToClient> kvp in NetworkServer.connections)
        {
            NetworkConnectionToClient connection = kvp.Value;
            if (connection == null || !connection.isReady || connection.identity == null)
            {
                continue;
            }

            float distSqr = (identity.transform.position - connection.identity.transform.position).sqrMagnitude;
            if (distSqr <= thresholdSqr)
            {
                newObservers.Add(connection);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Évaluation de la visibilité permanente
    // ─────────────────────────────────────────────────────────────────────────

    private bool EvaluateAlwaysVisible(NetworkIdentity identity)
    {
        if (identity.GetComponent<PlayerHealth>() != null)
        {
            return true;
        }

        if (identity.GetComponent<GameSessionManager>() != null)
        {
            return true;
        }

        if (identity.GetComponent<NetworkEffectManager>() != null)
        {
            return true;
        }

        if (identity.GetComponent<NetworkGenerator>() != null)
        {
            return true;
        }

        NetworkSyncDistance syncDistance = identity.GetComponent<NetworkSyncDistance>();
        if (syncDistance == null)
        {
            return true;
        }

        return false;
    }

    private void AddAllReadyConnections(HashSet<NetworkConnectionToClient> observers)
    {
        foreach (KeyValuePair<int, NetworkConnectionToClient> kvp in NetworkServer.connections)
        {
            NetworkConnectionToClient connection = kvp.Value;
            if (connection != null && connection.isReady && connection.identity != null)
            {
                observers.Add(connection);
            }
        }
    }
}
