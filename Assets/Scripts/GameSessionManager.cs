using Mirror;
using UnityEngine;
using System;

/// <summary>
/// État de la session de jeu, utilisé comme SyncVar pour synchroniser la machine à états.
/// byte sous-jacent pour minimiser la bande passante réseau.
/// </summary>
public enum GameState : byte
{
    Lobby = 0,
    Warmup = 1,
    Active = 2,
    PostGame = 3
}

/// <summary>
/// Gestionnaire central de la session de jeu — Machine à états uniquement.
/// Les responsabilités ont été décomposées en sous-composants sur le même GameObject :
/// - SessionTimerController : timer synchronisé
/// - SessionScoreboard : scoreboard réseau + kill tracking
/// - SessionReadySystem : système de prêt lobby
///
/// GameSessionManager reste le point d'entrée singleton et la façade publique.
/// Les événements C# statiques sont relayés par les sous-composants.
/// </summary>
public class GameSessionManager : NetworkBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────────────────────────────────────────

    public static GameSessionManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Configuration (Inspector)
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Durées des phases (secondes)")]
    [SerializeField] private float warmupDuration = 10.0f;
    [SerializeField] private float gameDuration = 300.0f;
    [SerializeField] private float postGameDuration = 15.0f;

    [Header("Joueurs")]
    [SerializeField] private int minPlayersToStart = 2;

    // ─────────────────────────────────────────────────────────────────────────
    // Synced State
    // ─────────────────────────────────────────────────────────────────────────

    [SyncVar(hook = nameof(OnGameStateChanged))]
    private GameState currentGameState = GameState.Lobby;

    // ─────────────────────────────────────────────────────────────────────────
    // Événements C# — Binding UI event-driven
    // ─────────────────────────────────────────────────────────────────────────

    public static event Action<GameState, GameState> OnGameStateChangedEvent;
    public static event Action OnTimerSyncedEvent;
    public static event Action OnScoreboardChangedEvent;
    public static event Action OnReadyStateChangedEvent;

    // ─────────────────────────────────────────────────────────────────────────
    // Sub-components
    // ─────────────────────────────────────────────────────────────────────────

    private SessionTimerController timerController;
    private SessionScoreboard scoreboard;
    private SessionReadySystem readySystem;

    // ─────────────────────────────────────────────────────────────────────────
    // Properties
    // ─────────────────────────────────────────────────────────────────────────

    public GameState CurrentState => currentGameState;
    public bool IsTimerRunning => timerController != null && timerController.IsTimerRunning;
    public float RemainingTime => timerController != null ? timerController.RemainingTime : 0f;
    public int PlayerCount => scoreboard != null ? scoreboard.PlayerCount : 0;
    public int MinPlayersToStart => minPlayersToStart;

    public SessionScoreboard Scoreboard => scoreboard;
    public SessionTimerController Timer => timerController;
    public SessionReadySystem ReadySystem => readySystem;

    /// <summary>Accès direct au scoreboard pour compatibilité (GameSessionUI, etc.).</summary>
    public SyncList<PlayerScoreData> playerScores => scoreboard.playerScores;

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameSessionManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        timerController = GetComponent<SessionTimerController>();
        scoreboard = GetComponent<SessionScoreboard>();
        readySystem = GetComponent<SessionReadySystem>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SyncVar Hooks
    // ─────────────────────────────────────────────────────────────────────────

    private void OnGameStateChanged(GameState oldState, GameState newState)
    {
        OnGameStateChangedEvent?.Invoke(oldState, newState);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Event Relays — Appelés par les sous-composants
    // ─────────────────────────────────────────────────────────────────────────

    public static void NotifyTimerSynced() => OnTimerSyncedEvent?.Invoke();
    public static void NotifyScoreboardChanged() => OnScoreboardChangedEvent?.Invoke();
    public static void NotifyReadyStateChanged() => OnReadyStateChangedEvent?.Invoke();

    // ─────────────────────────────────────────────────────────────────────────
    // Server — State Machine
    // ─────────────────────────────────────────────────────────────────────────

    [Server]
    public void ServerStartWarmup()
    {
        if (currentGameState != GameState.Lobby) return;

        if (scoreboard.PlayerCount < minPlayersToStart)
        {
            Debug.LogWarning($"[GameSessionManager] Pas assez de joueurs ({scoreboard.PlayerCount}/{minPlayersToStart})");
            return;
        }

        if (!readySystem.ServerAreAllPlayersReady())
        {
            Debug.LogWarning("[GameSessionManager] Tous les joueurs ne sont pas prêts.");
            return;
        }

        ServerTransitionTo(GameState.Warmup);
    }

    [Server]
    public void ServerForceReturnToLobby()
    {
        ServerTransitionTo(GameState.Lobby);
    }

    [Server]
    public void ServerTransitionTo(GameState newState)
    {
        GameState previousState = currentGameState;

        timerController.ServerStopTimer();
        currentGameState = newState;

        switch (newState)
        {
            case GameState.Lobby:
                readySystem.ServerResetAllReadyStates();
                break;

            case GameState.Warmup:
                timerController.ServerStartTimer(warmupDuration, () => ServerTransitionTo(GameState.Active));
                break;

            case GameState.Active:
                scoreboard.ServerResetAllScores();
                timerController.ServerStartTimer(gameDuration, () => ServerTransitionTo(GameState.PostGame));
                RpcNotifyGameStarted();
                break;

            case GameState.PostGame:
                timerController.ServerStartTimer(postGameDuration, () => ServerTransitionTo(GameState.Lobby));
                RpcNotifyGameEnded();
                break;
        }

        Debug.Log($"[GameSessionManager] {previousState} → {newState}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RPCs
    // ─────────────────────────────────────────────────────────────────────────

    [ClientRpc]
    private void RpcNotifyGameStarted()
    {
        Debug.Log("[GameSessionManager] La partie commence !");
    }

    [ClientRpc]
    private void RpcNotifyGameEnded()
    {
        Debug.Log("[GameSessionManager] La partie est terminée !");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Backward Compatibility Delegates
    // ─────────────────────────────────────────────────────────────────────────

    [Server]
    public void ServerRegisterPlayer(NetworkIdentity playerIdentity)
    {
        scoreboard.ServerRegisterPlayer(playerIdentity);
    }

    [Server]
    public void ServerUnregisterPlayer(uint netId)
    {
        scoreboard.ServerUnregisterPlayer(netId);
    }

    [Server]
    public void ServerRecordKill(uint killerNetId, uint victimNetId)
    {
        if (currentGameState != GameState.Active) return;
        scoreboard.ServerRecordKill(killerNetId, victimNetId);
    }

    [Server]
    public void ServerSetPlayerReady(uint playerNetId, bool ready)
    {
        readySystem.ServerSetPlayerReady(playerNetId, ready);
    }

    public bool TryGetPlayerScore(uint netId, out PlayerScoreData scoreData)
    {
        return scoreboard.TryGetPlayerScore(netId, out scoreData);
    }

    public int GetLeaderIndex()
    {
        return scoreboard.GetLeaderIndex();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Static Helpers
    // ─────────────────────────────────────────────────────────────────────────

    public static string GetStateLabel(GameState state)
    {
        switch (state)
        {
            case GameState.Lobby: return "Lobby";
            case GameState.Warmup: return "Warmup";
            case GameState.Active: return "En cours";
            case GameState.PostGame: return "Fin de partie";
            default: return "Inconnu";
        }
    }

    public static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int totalSeconds = Mathf.CeilToInt(seconds);
        int minutes = totalSeconds / 60;
        int secs = totalSeconds % 60;
        return minutes.ToString() + ":" + secs.ToString("D2");
    }
}
