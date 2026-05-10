using Mirror;
using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Scoreboard réseau synchronisé (extrait de GameSessionManager).
/// Gère l'enregistrement des joueurs, le tracking kills/deaths et le scoring.
/// SyncList envoie uniquement les deltas pour optimiser la bande passante.
/// Doit être sur le même GameObject/NetworkIdentity que GameSessionManager.
/// </summary>
public class SessionScoreboard : NetworkBehaviour
{
    [Header("Scoring")]
    [SerializeField] private int killScoreValue = 100;
    [SerializeField] private int deathScorePenalty = 25;

    public readonly SyncList<PlayerScoreData> playerScores = new SyncList<PlayerScoreData>();

    private Dictionary<uint, int> scoreIndexByNetId;

    /// <summary>Déclenché quand un kill est broadcast (killerName, victimName).</summary>
    public static event Action<string, string> OnKillEvent;

    public int PlayerCount => playerScores.Count;

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        scoreIndexByNetId = new Dictionary<uint, int>(16);
    }

    private void OnDestroy()
    {
        playerScores.Callback -= OnPlayerScoresListChanged;
    }

    public override void OnStartClient()
    {
        playerScores.Callback += OnPlayerScoresListChanged;

        for (int i = 0; i < playerScores.Count; i++)
        {
            RebuildClientScoreIndex(playerScores[i].netId, i);
        }

        GameSessionManager.NotifyScoreboardChanged();
    }

    private void OnPlayerScoresListChanged(
        SyncList<PlayerScoreData>.Operation operation,
        int index,
        PlayerScoreData oldItem,
        PlayerScoreData newItem)
    {
        GameSessionManager.NotifyScoreboardChanged();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Server — Player Registration
    // ─────────────────────────────────────────────────────────────────────────

    [Server]
    public void ServerRegisterPlayer(NetworkIdentity playerIdentity)
    {
        if (playerIdentity == null) return;

        uint netId = playerIdentity.netId;
        if (scoreIndexByNetId.ContainsKey(netId)) return;

        string displayName = "Player_" + netId.ToString();
        PlayerScoreData data = new PlayerScoreData(netId, displayName);
        playerScores.Add(data);
        scoreIndexByNetId[netId] = playerScores.Count - 1;
    }

    [Server]
    public void ServerUnregisterPlayer(uint netId)
    {
        if (!scoreIndexByNetId.ContainsKey(netId)) return;

        int index = scoreIndexByNetId[netId];
        playerScores.RemoveAt(index);
        scoreIndexByNetId.Remove(netId);
        ServerRebuildScoreIndex();
    }

    [Server]
    private void ServerRebuildScoreIndex()
    {
        scoreIndexByNetId.Clear();
        for (int i = 0; i < playerScores.Count; i++)
        {
            scoreIndexByNetId[playerScores[i].netId] = i;
        }
    }

    private void RebuildClientScoreIndex(uint netId, int index)
    {
        if (scoreIndexByNetId == null)
        {
            scoreIndexByNetId = new Dictionary<uint, int>(16);
        }
        scoreIndexByNetId[netId] = index;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Server — Kill/Death Tracking
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enregistre un kill/death. La vérification du GameState est faite par l'appelant.
    /// </summary>
    [Server]
    public void ServerRecordKill(uint killerNetId, uint victimNetId)
    {
        string killerName = "";
        string victimName = "";

        if (killerNetId != 0 && killerNetId != victimNetId)
        {
            int killerIndex;
            if (scoreIndexByNetId.TryGetValue(killerNetId, out killerIndex))
            {
                PlayerScoreData killerData = playerScores[killerIndex];
                killerName = killerData.playerName;
                killerData.kills += 1;
                killerData.score += killScoreValue;
                playerScores[killerIndex] = killerData;
            }
        }

        int victimIndex;
        if (scoreIndexByNetId.TryGetValue(victimNetId, out victimIndex))
        {
            PlayerScoreData victimData = playerScores[victimIndex];
            victimName = victimData.playerName;
            victimData.deaths += 1;
            victimData.score -= deathScorePenalty;
            if (victimData.score < 0) victimData.score = 0;
            playerScores[victimIndex] = victimData;
        }

        if (!string.IsNullOrEmpty(killerName) && !string.IsNullOrEmpty(victimName))
        {
            RpcBroadcastKill(killerName, victimName);
        }
    }

    [Server]
    public void ServerResetAllScores()
    {
        for (int i = 0; i < playerScores.Count; i++)
        {
            PlayerScoreData data = playerScores[i];
            data.kills = 0;
            data.deaths = 0;
            data.score = 0;
            playerScores[i] = data;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Server — Ready State (délégué par SessionReadySystem)
    // ─────────────────────────────────────────────────────────────────────────

    [Server]
    public void ServerSetPlayerReadyFlag(uint playerNetId, bool ready)
    {
        int index;
        if (!scoreIndexByNetId.TryGetValue(playerNetId, out index)) return;

        PlayerScoreData data = playerScores[index];
        if (data.isReady == ready) return;

        data.isReady = ready;
        playerScores[index] = data;
    }

    [Server]
    public void ServerResetAllReadyFlags()
    {
        for (int i = 0; i < playerScores.Count; i++)
        {
            PlayerScoreData data = playerScores[i];
            if (data.isReady)
            {
                data.isReady = false;
                playerScores[i] = data;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RPCs
    // ─────────────────────────────────────────────────────────────────────────

    [ClientRpc]
    private void RpcBroadcastKill(string killerName, string victimName)
    {
        OnKillEvent?.Invoke(killerName, victimName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public Queries
    // ─────────────────────────────────────────────────────────────────────────

    public bool TryGetPlayerScore(uint netId, out PlayerScoreData scoreData)
    {
        for (int i = 0; i < playerScores.Count; i++)
        {
            if (playerScores[i].netId == netId)
            {
                scoreData = playerScores[i];
                return true;
            }
        }
        scoreData = default;
        return false;
    }

    public int GetLeaderIndex()
    {
        if (playerScores.Count == 0) return -1;

        int bestIndex = 0;
        int bestScore = playerScores[0].score;

        for (int i = 1; i < playerScores.Count; i++)
        {
            if (playerScores[i].score > bestScore)
            {
                bestScore = playerScores[i].score;
                bestIndex = i;
            }
        }
        return bestIndex;
    }
}
