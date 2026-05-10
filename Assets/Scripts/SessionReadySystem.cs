using Mirror;
using UnityEngine;

/// <summary>
/// Gère le système de prêt des joueurs en lobby (extrait de GameSessionManager).
/// Lit et modifie les flags isReady dans SessionScoreboard.playerScores.
/// Déclenche un auto-start quand tous les joueurs sont prêts.
/// Doit être sur le même GameObject/NetworkIdentity que GameSessionManager.
/// </summary>
public class SessionReadySystem : NetworkBehaviour
{
    private SessionScoreboard scoreboard;

    private void Awake()
    {
        scoreboard = GetComponent<SessionScoreboard>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Server API
    // ─────────────────────────────────────────────────────────────────────────

    [Server]
    public void ServerSetPlayerReady(uint playerNetId, bool ready)
    {
        scoreboard.ServerSetPlayerReadyFlag(playerNetId, ready);
        GameSessionManager.NotifyReadyStateChanged();

        GameSessionManager gsm = GameSessionManager.Instance;
        if (gsm != null
            && gsm.CurrentState == GameState.Lobby
            && ServerAreAllPlayersReady()
            && scoreboard.PlayerCount >= gsm.MinPlayersToStart)
        {
            gsm.ServerTransitionTo(GameState.Warmup);
        }
    }

    [Server]
    public bool ServerAreAllPlayersReady()
    {
        if (scoreboard.PlayerCount == 0) return false;

        for (int i = 0; i < scoreboard.playerScores.Count; i++)
        {
            if (!scoreboard.playerScores[i].isReady) return false;
        }
        return true;
    }

    [Server]
    public void ServerResetAllReadyStates()
    {
        scoreboard.ServerResetAllReadyFlags();
    }
}
