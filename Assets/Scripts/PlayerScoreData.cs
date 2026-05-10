using System;

/// <summary>
/// Données de score d'un joueur, synchronisées via SyncList dans GameSessionManager.
/// Suit le pattern de ItemSlot : struct sérialisable, champs simples pour la sérialisation Mirror.
/// </summary>
[Serializable]
public struct PlayerScoreData
{
    public uint netId;
    public string playerName;
    public int kills;
    public int deaths;
    public int score;
    public bool isReady;

    public PlayerScoreData(uint playerNetId, string name)
    {
        netId = playerNetId;
        playerName = name;
        kills = 0;
        deaths = 0;
        score = 0;
        isReady = false;
    }

    public float KDRatio
    {
        get
        {
            if (deaths <= 0) return (float)kills;
            return (float)kills / (float)deaths;
        }
    }
}
