using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Mirror;

/// <summary>
/// UI de la session de jeu, entièrement pilotée par événements (aucun polling Update()).
///
/// Fonctionnement :
/// - S'abonne aux événements statiques de GameSessionManager (OnGameStateChangedEvent, etc.)
/// - Les SyncVar hooks de GameSessionManager déclenchent ces événements
/// - Le timer utilise une coroutine locale cadencée à 10 Hz (pas de trafic réseau)
/// - Les late-joiners sont gérés via l'initialisation dans Start()
///
/// Setup dans la scène :
/// - Attacher à un GameObject enfant du Canvas de scène
/// - Configurer les références Text et les panels dans l'Inspector
/// - Le scoreboardPanel doit contenir un VerticalLayoutGroup sur scoreboardContent
/// </summary>
public class GameSessionUI : MonoBehaviour
{
    [Header("État de la partie")]
    [SerializeField]
    [Tooltip("Texte affichant l'état actuel (Lobby, Warmup, En cours, Fin)")]
    private Text stateText;

    [SerializeField]
    [Tooltip("Texte affichant le countdown du timer")]
    private Text timerText;

    [Header("Scoreboard")]
    [SerializeField]
    [Tooltip("Panel racine du scoreboard (activé/désactivé via Tab)")]
    private GameObject scoreboardPanel;

    [SerializeField]
    [Tooltip("Transform parent contenant les entrées du scoreboard (avec VerticalLayoutGroup)")]
    private Transform scoreboardContent;

    [SerializeField]
    [Tooltip("Prefab d'une entrée du scoreboard. Doit contenir un composant Text.")]
    private GameObject scoreEntryPrefab;

    [Header("Contrôles")]
    [SerializeField]
    [Tooltip("Bouton pour lancer la partie (visible uniquement en Lobby pour le host)")]
    private GameObject startGameButton;

    [Header("Messages centraux")]
    [SerializeField]
    [Tooltip("Texte pour les annonces centrales (Game Start, Game Over, etc.)")]
    private Text announcementText;

    [SerializeField]
    [Tooltip("Durée d'affichage des annonces centrales (secondes)")]
    private float announcementDuration = 3.0f;

    // ─────────────────────────────────────────────────────────────────────────
    // Internals
    // ─────────────────────────────────────────────────────────────────────────

    private Coroutine timerDisplayCoroutine;
    private Coroutine announcementCoroutine;
    private WaitForSeconds timerWait;
    private WaitForSeconds announcementWait;

    private List<GameObject> scoreEntryPool;
    private bool scoreboardVisible;

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        timerWait = new WaitForSeconds(0.1f);
        announcementWait = new WaitForSeconds(announcementDuration);
        scoreEntryPool = new List<GameObject>(16);
        scoreboardVisible = false;
    }

    private void OnEnable()
    {
        GameSessionManager.OnGameStateChangedEvent += HandleGameStateChanged;
        GameSessionManager.OnTimerSyncedEvent += HandleTimerSynced;
        GameSessionManager.OnScoreboardChangedEvent += HandleScoreboardChanged;
        GameSessionManager.OnReadyStateChangedEvent += HandleReadyStateChanged;
    }

    private void OnDisable()
    {
        GameSessionManager.OnGameStateChangedEvent -= HandleGameStateChanged;
        GameSessionManager.OnTimerSyncedEvent -= HandleTimerSynced;
        GameSessionManager.OnScoreboardChangedEvent -= HandleScoreboardChanged;
        GameSessionManager.OnReadyStateChangedEvent -= HandleReadyStateChanged;
    }

    private void Start()
    {
        if (scoreboardPanel != null)
        {
            scoreboardPanel.SetActive(false);
        }

        if (announcementText != null)
        {
            announcementText.gameObject.SetActive(false);
        }

        InitializeFromCurrentState();
    }

    /// <summary>
    /// Gère les late-joiners : lit l'état actuel de GameSessionManager
    /// au lieu de dépendre uniquement des hooks SyncVar (qui ne se déclenchent
    /// pas lors de la synchronisation initiale Mirror).
    /// </summary>
    private void InitializeFromCurrentState()
    {
        GameSessionManager manager = GameSessionManager.Instance;
        if (manager == null)
        {
            RefreshStateDisplay(GameState.Lobby);
            RefreshTimerDisplay(0.0f);
            RefreshStartButton(GameState.Lobby);
            return;
        }

        RefreshStateDisplay(manager.CurrentState);
        RefreshStartButton(manager.CurrentState);

        if (manager.IsTimerRunning)
        {
            StartTimerDisplayCoroutine();
        }
        else
        {
            RefreshTimerDisplay(0.0f);
        }

        RefreshScoreboard();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Event Handlers — Pilotés par GameSessionManager
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleGameStateChanged(GameState oldState, GameState newState)
    {
        RefreshStateDisplay(newState);
        RefreshStartButton(newState);

        switch (newState)
        {
            case GameState.Active:
                ShowAnnouncement("GO !");
                break;
            case GameState.PostGame:
                ShowAnnouncement("Fin de partie");
                break;
            case GameState.Warmup:
                ShowAnnouncement("Préparation...");
                break;
        }
    }

    private void HandleTimerSynced()
    {
        GameSessionManager manager = GameSessionManager.Instance;
        if (manager == null)
        {
            return;
        }

        if (manager.IsTimerRunning)
        {
            StartTimerDisplayCoroutine();
        }
        else
        {
            StopTimerDisplayCoroutine();
            RefreshTimerDisplay(0.0f);
        }
    }

    private void HandleScoreboardChanged()
    {
        if (scoreboardVisible)
        {
            RefreshScoreboard();
        }
    }

    private void HandleReadyStateChanged()
    {
        if (scoreboardVisible)
        {
            RefreshScoreboard();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // State Display
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshStateDisplay(GameState state)
    {
        if (stateText == null)
        {
            return;
        }

        stateText.text = GameSessionManager.GetStateLabel(state);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Timer Display — Coroutine locale cadencée à 10 Hz
    // ─────────────────────────────────────────────────────────────────────────

    private void StartTimerDisplayCoroutine()
    {
        if (timerDisplayCoroutine != null)
        {
            return;
        }

        timerDisplayCoroutine = StartCoroutine(TimerDisplayLoop());
    }

    private void StopTimerDisplayCoroutine()
    {
        if (timerDisplayCoroutine != null)
        {
            StopCoroutine(timerDisplayCoroutine);
            timerDisplayCoroutine = null;
        }
    }

    private IEnumerator TimerDisplayLoop()
    {
        while (true)
        {
            GameSessionManager manager = GameSessionManager.Instance;
            if (manager == null || !manager.IsTimerRunning)
            {
                break;
            }

            float remaining = manager.RemainingTime;
            RefreshTimerDisplay(remaining);

            if (remaining <= 0.0f)
            {
                break;
            }

            yield return timerWait;
        }

        RefreshTimerDisplay(0.0f);
        timerDisplayCoroutine = null;
    }

    private void RefreshTimerDisplay(float remainingSeconds)
    {
        if (timerText == null)
        {
            return;
        }

        if (remainingSeconds <= 0.0f)
        {
            timerText.text = "--:--";
            return;
        }

        timerText.text = GameSessionManager.FormatTime(remainingSeconds);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Start Button
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshStartButton(GameState state)
    {
        if (startGameButton == null)
        {
            return;
        }

        bool showButton = state == GameState.Lobby && NetworkServer.active;
        startGameButton.SetActive(showButton);
    }

    /// <summary>
    /// Appelé par le bouton "Start Game" dans l'UI (onClick).
    /// Sécurisé : vérifie que le serveur est actif et que le manager existe.
    /// </summary>
    public void OnStartGameButtonClicked()
    {
        if (!NetworkServer.active)
        {
            return;
        }

        GameSessionManager manager = GameSessionManager.Instance;
        if (manager == null)
        {
            return;
        }

        manager.ServerStartWarmup();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scoreboard
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Toggle du scoreboard via input externe (Tab).
    /// Appelable depuis un PlayerInputHandler ou directement.
    /// </summary>
    public void ToggleScoreboard()
    {
        scoreboardVisible = !scoreboardVisible;

        if (scoreboardPanel != null)
        {
            scoreboardPanel.SetActive(scoreboardVisible);
        }

        if (scoreboardVisible)
        {
            RefreshScoreboard();
        }
    }

    private void RefreshScoreboard()
    {
        GameSessionManager manager = GameSessionManager.Instance;
        if (manager == null || scoreboardContent == null)
        {
            return;
        }

        int playerCount = manager.playerScores.Count;

        EnsurePoolSize(playerCount);

        for (int i = 0; i < scoreEntryPool.Count; i++)
        {
            if (i < playerCount)
            {
                PlayerScoreData data = manager.playerScores[i];
                scoreEntryPool[i].SetActive(true);

                Text entryText = scoreEntryPool[i].GetComponent<Text>();
                if (entryText != null)
                {
                    GameSessionManager gsm = GameSessionManager.Instance;
                    bool isLobby = gsm != null && gsm.CurrentState == GameState.Lobby;
                    string readyTag = isLobby ? (data.isReady ? " [PRET]" : " [...]") : "";

                    entryText.text = data.playerName
                        + readyTag
                        + "  |  K: " + data.kills.ToString()
                        + "  D: " + data.deaths.ToString()
                        + "  Score: " + data.score.ToString();
                }
            }
            else
            {
                scoreEntryPool[i].SetActive(false);
            }
        }
    }

    private void EnsurePoolSize(int requiredCount)
    {
        while (scoreEntryPool.Count < requiredCount)
        {
            GameObject entry;

            if (scoreEntryPrefab != null)
            {
                entry = Instantiate(scoreEntryPrefab, scoreboardContent);
            }
            else
            {
                entry = new GameObject("ScoreEntry_" + scoreEntryPool.Count.ToString());
                entry.transform.SetParent(scoreboardContent, false);

                Text text = entry.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 16;
                text.color = Color.white;
                text.alignment = TextAnchor.MiddleLeft;

                RectTransform rectTransform = entry.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(400.0f, 30.0f);
            }

            scoreEntryPool.Add(entry);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Announcements
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowAnnouncement(string message)
    {
        if (announcementText == null)
        {
            return;
        }

        if (announcementCoroutine != null)
        {
            StopCoroutine(announcementCoroutine);
        }

        announcementCoroutine = StartCoroutine(AnnouncementSequence(message));
    }

    private IEnumerator AnnouncementSequence(string message)
    {
        announcementText.text = message;
        announcementText.gameObject.SetActive(true);

        yield return announcementWait;

        announcementText.gameObject.SetActive(false);
        announcementCoroutine = null;
    }
}
