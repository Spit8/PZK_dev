using UnityEngine;

/// <summary>
/// Composant optionnel pour le filtrage spatial réseau (Phase 3.1).
/// À placer sur les NetworkIdentity non-essentielles (PickupItem, décor, etc.)
/// pour limiter leur synchronisation aux clients proches.
///
/// Utilisé par PZKInterestManagement pour déterminer la distance de visibilité.
/// Sans ce composant, l'objet est considéré comme "toujours visible" par tous les clients.
///
/// Optimisation bande passante :
/// - Un PickupItem avec VisibilityRange = 30m ne sera synchronisé qu'aux joueurs
///   dans un rayon de 30m, réduisant le trafic pour les maps larges avec beaucoup d'objets.
/// - Les joueurs et objets de scène critiques (GameSessionManager, NetworkGenerator)
///   sont toujours visibles indépendamment de ce composant.
/// </summary>
public class NetworkSyncDistance : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Distance (mètres) au-delà de laquelle l'objet n'est plus synchronisé aux clients distants")]
    private float visibilityRange = 30.0f;

    public float VisibilityRange => visibilityRange;
}
