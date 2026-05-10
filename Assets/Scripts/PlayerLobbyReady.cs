using Mirror;
using UnityEngine;

/// <summary>
/// Permet au joueur de basculer son état "Prêt" dans le lobby (Phase 3.1).
/// Le serveur met à jour le GameSessionManager via la SyncList playerScores.
///
/// Architecture :
/// - Attaché au prefab joueur (requiert authority pour les Commands)
/// - Seul le joueur local peut envoyer CmdToggleReady / CmdSetReady
/// - Le serveur valide et met à jour le GameSessionManager
/// - Quand tous les joueurs sont prêts ET le minimum atteint → démarrage automatique
/// - L'état "prêt" est visible par tous via SyncList (scoreboard)
///
/// Setup :
/// - Attacher au player prefab à côté de PlayerHealth, PlayerMovement, etc.
/// - L'UI appelle ToggleReady() ou SetReady(bool) sur le joueur local
/// </summary>
public class PlayerLobbyReady : NetworkBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Public API — Appelable par l'UI ou les raccourcis clavier
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Bascule l'état prêt du joueur local.
    /// </summary>
    public void ToggleReady()
    {
        if (!isLocalPlayer)
        {
            return;
        }

        CmdToggleReady();
    }

    /// <summary>
    /// Force l'état prêt à une valeur spécifique.
    /// </summary>
    public void SetReady(bool ready)
    {
        if (!isLocalPlayer)
        {
            return;
        }

        CmdSetReady(ready);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Commands — Client → Serveur
    // ─────────────────────────────────────────────────────────────────────────

    [Command]
    private void CmdToggleReady()
    {
        GameSessionManager session = GameSessionManager.Instance;
        if (session == null)
        {
            return;
        }

        PlayerScoreData scoreData;
        if (session.TryGetPlayerScore(netId, out scoreData))
        {
            session.ServerSetPlayerReady(netId, !scoreData.isReady);
        }
    }

    [Command]
    private void CmdSetReady(bool ready)
    {
        GameSessionManager session = GameSessionManager.Instance;
        if (session == null)
        {
            return;
        }

        session.ServerSetPlayerReady(netId, ready);
    }
}
