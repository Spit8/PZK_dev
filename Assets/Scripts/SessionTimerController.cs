using Mirror;
using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Gère le timer synchronisé de la session (extrait de GameSessionManager).
/// Stocke l'heure de fin réseau pour éviter le trafic par-frame.
/// Les clients calculent le temps restant localement via NetworkTime.time.
/// Doit être sur le même GameObject/NetworkIdentity que GameSessionManager.
/// </summary>
public class SessionTimerController : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnTimerEndTimeChanged))]
    private double timerEndTime;

    [SyncVar(hook = nameof(OnTimerRunningChanged))]
    private bool timerRunning;

    private Coroutine activeTimerCoroutine;
    private Action pendingOnComplete;

    public bool IsTimerRunning => timerRunning;

    public float RemainingTime
    {
        get
        {
            if (!timerRunning) return 0f;
            double remaining = timerEndTime - NetworkTime.time;
            return remaining < 0.0 ? 0f : (float)remaining;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SyncVar Hooks → relay vers GameSessionManager events
    // ─────────────────────────────────────────────────────────────────────────

    private void OnTimerEndTimeChanged(double oldEndTime, double newEndTime)
    {
        GameSessionManager.NotifyTimerSynced();
    }

    private void OnTimerRunningChanged(bool wasRunning, bool isNowRunning)
    {
        GameSessionManager.NotifyTimerSynced();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Server API
    // ─────────────────────────────────────────────────────────────────────────

    [Server]
    public void ServerStartTimer(float duration, Action onComplete)
    {
        ServerStopTimer();
        pendingOnComplete = onComplete;
        timerEndTime = NetworkTime.time + (double)duration;
        timerRunning = true;
        activeTimerCoroutine = StartCoroutine(ServerRunTimer(duration));
    }

    [Server]
    public void ServerStopTimer()
    {
        if (activeTimerCoroutine != null)
        {
            StopCoroutine(activeTimerCoroutine);
            activeTimerCoroutine = null;
        }
        timerRunning = false;
        pendingOnComplete = null;
    }

    [Server]
    private IEnumerator ServerRunTimer(float duration)
    {
        yield return new WaitForSeconds(duration);

        timerRunning = false;
        activeTimerCoroutine = null;

        Action callback = pendingOnComplete;
        pendingOnComplete = null;
        callback?.Invoke();
    }
}
