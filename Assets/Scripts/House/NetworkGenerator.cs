using Mirror;
using UnityEngine;

/// <summary>
/// Wrapper réseau autour de Generator.cs (Phase 2.4).
/// Synchronise la génération procédurale de la map via une seed partagée.
///
/// Architecture :
/// - Le serveur génère un int seed aléatoire au démarrage
/// - La seed est stockée dans un [SyncVar] pour les late-joiners
/// - Le serveur et les clients appellent Generator.Generate(seed) localement
/// - Résultat : même seed → même séquence Random → même map sur toutes les machines
/// - Zéro trafic réseau continu (un seul int envoyé, pas de NetworkServer.Spawn par pièce)
///
/// Régénération :
/// - S'abonne à GameSessionManager.OnGameStateChangedEvent
/// - Si regenerateEachRound est activé, régénère pendant le Warmup
/// - Peut aussi être appelé manuellement via ServerRegenerate()
///
/// Setup :
/// - Attacher au même GameObject que Generator
/// - Ajouter un NetworkIdentity sur ce GameObject
/// - NetworkGenerator désactive automatiquement l'auto-génération de Generator
/// </summary>
[RequireComponent(typeof(Generator))]
[RequireComponent(typeof(NetworkIdentity))]
public class NetworkGenerator : NetworkBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────────────────────────────────────────

    public static NetworkGenerator Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Configuration
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Régénération")]
    [SerializeField]
    [Tooltip("Si activé, la map est régénérée à chaque début de round (transition Warmup)")]
    private bool regenerateEachRound = false;

    // ─────────────────────────────────────────────────────────────────────────
    // Synced State
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seed utilisée pour la génération courante.
    /// SyncVar assure que les late-joiners reçoivent la valeur automatiquement.
    /// </summary>
    [SyncVar]
    private int currentSeed;

    /// <summary>
    /// Indique si la map a été générée. Les late-joiners vérifient ce flag
    /// dans OnStartClient pour savoir s'ils doivent générer.
    /// </summary>
    [SyncVar]
    private bool isGenerated;

    // ─────────────────────────────────────────────────────────────────────────
    // Internals
    // ─────────────────────────────────────────────────────────────────────────

    private Generator generator;

    // ─────────────────────────────────────────────────────────────────────────
    // Properties
    // ─────────────────────────────────────────────────────────────────────────

    public int CurrentSeed => currentSeed;
    public bool IsGenerated => isGenerated;

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NetworkGenerator] Duplicate instance destroyed.");
            Destroy(this);
            return;
        }

        Instance = this;

        generator = GetComponent<Generator>();
        if (generator != null)
        {
            generator.networkControlled = true;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        GameSessionManager.OnGameStateChangedEvent -= OnGameStateChanged;
    }

    private void OnEnable()
    {
        GameSessionManager.OnGameStateChangedEvent += OnGameStateChanged;
    }

    private void OnDisable()
    {
        GameSessionManager.OnGameStateChangedEvent -= OnGameStateChanged;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Server — Initialization
    // ─────────────────────────────────────────────────────────────────────────

    public override void OnStartServer()
    {
        if (generator == null || !generator.HasRequiredPrefabs())
        {
            Debug.Log("[NetworkGenerator] Génération désactivée (prefabs non assignés ou Generator absent).");
            return;
        }

        currentSeed = GenerateNewSeed();
        ExecuteGeneration(currentSeed);
        isGenerated = true;

        Debug.Log($"[NetworkGenerator] Serveur: map générée avec seed {currentSeed}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Client — Late-join handling
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Appelé quand un client rejoint. Les SyncVars (currentSeed, isGenerated)
    /// sont déjà synchronisées à ce point pour les objets de scène.
    /// En mode Host, isServer est true → on skip (déjà généré côté serveur).
    /// </summary>
    public override void OnStartClient()
    {
        if (isServer)
        {
            return;
        }

        if (isGenerated)
        {
            ExecuteGeneration(currentSeed);
            Debug.Log($"[NetworkGenerator] Client: map générée avec seed {currentSeed}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Server — Regeneration
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Régénère la map avec une nouvelle seed.
    /// Appelable par GameSessionManager ou un système admin.
    /// </summary>
    [Server]
    public void ServerRegenerate()
    {
        currentSeed = GenerateNewSeed();
        isGenerated = false;

        ExecuteGeneration(currentSeed);
        isGenerated = true;

        RpcRegenerate(currentSeed);

        Debug.Log($"[NetworkGenerator] Serveur: map régénérée avec seed {currentSeed}");
    }

    /// <summary>
    /// Régénère la map avec une seed spécifique (utile pour le debug/replay).
    /// </summary>
    [Server]
    public void ServerRegenerateWithSeed(int seed)
    {
        currentSeed = seed;
        isGenerated = false;

        ExecuteGeneration(currentSeed);
        isGenerated = true;

        RpcRegenerate(currentSeed);

        Debug.Log($"[NetworkGenerator] Serveur: map régénérée avec seed imposée {currentSeed}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RPCs
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Envoyé à tous les clients connectés pour régénérer la map mid-session.
    /// Les late-joiners utilisent OnStartClient + SyncVar à la place.
    /// </summary>
    [ClientRpc]
    private void RpcRegenerate(int seed)
    {
        if (isServer)
        {
            return;
        }

        ExecuteGeneration(seed);
        Debug.Log($"[NetworkGenerator] Client: map régénérée avec seed {seed}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GameSessionManager Integration
    // ─────────────────────────────────────────────────────────────────────────

    private void OnGameStateChanged(GameState oldState, GameState newState)
    {
        if (!isServer)
        {
            return;
        }

        if (!regenerateEachRound)
        {
            return;
        }

        if (newState == GameState.Warmup)
        {
            ServerRegenerate();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Internal
    // ─────────────────────────────────────────────────────────────────────────

    private void ExecuteGeneration(int seed)
    {
        if (generator == null)
        {
            generator = GetComponent<Generator>();
        }

        if (generator == null)
        {
            Debug.LogError("[NetworkGenerator] Generator component introuvable !");
            return;
        }

        generator.Generate(seed);
    }

    private int GenerateNewSeed()
    {
        return Random.Range(int.MinValue, int.MaxValue);
    }
}
