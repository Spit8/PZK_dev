using Mirror;
using UnityEngine;

/// <summary>
/// Système de santé serveur-autoritaire avec gestion de mort et respawn.
/// Le serveur est seul responsable des modifications de santé.
/// Les clients reçoivent des RPCs pour le feedback visuel et directionnel.
/// </summary>
public class PlayerHealth : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnHealthChanged))]
    public int currentHealth;

    [SyncVar]
    public int maxHealth = 100;

    [SyncVar(hook = nameof(OnIsDeadChanged))]
    private bool isDead;

    [SyncVar]
    private uint lastAttackerNetId;

    /// <summary>Indique si le joueur est actuellement mort (accessible par tous les clients).</summary>
    public bool IsDead => isDead;

    private PlayerHealthUI cachedHealthUI;

    public override void OnStartServer()
    {
        currentHealth = maxHealth;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Server — Damage & Healing
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Applique des dégâts sans source identifiée (environnement, chute, zone, etc.).
    /// </summary>
    [Server]
    public void TakeDamage(int amount)
    {
        TakeDamage(amount, null);
    }

    /// <summary>
    /// Applique des dégâts avec identification de l'attaquant pour le kill tracking.
    /// Déclenche le feedback directionnel vers la victime et les effets globaux.
    /// </summary>
    [Server]
    public void TakeDamage(int amount, NetworkIdentity attacker)
    {
        if (amount <= 0 || isDead)
        {
            return;
        }

        if (attacker != null)
        {
            lastAttackerNetId = attacker.netId;
        }

        int newHealth = currentHealth - amount;
        if (newHealth < 0)
        {
            newHealth = 0;
        }
        currentHealth = newHealth;

        if (attacker != null && connectionToClient != null)
        {
            Vector3 hitDirection = (attacker.transform.position - transform.position).normalized;
            TargetOnDamage(connectionToClient, amount, hitDirection);
        }

        RpcOnDamage(transform.position + Vector3.up);

        if (currentHealth <= 0)
        {
            ServerInitiateDeath();
        }
    }

    /// <summary>
    /// Restaure des points de vie côté serveur. Ignoré sur un joueur mort.
    /// </summary>
    [Server]
    public void Heal(int amount)
    {
        if (amount <= 0 || isDead)
        {
            return;
        }

        int newHealth = currentHealth + amount;
        if (newHealth > maxHealth)
        {
            newHealth = maxHealth;
        }
        currentHealth = newHealth;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Server — Death & Respawn
    // ─────────────────────────────────────────────────────────────────────────

    [Server]
    private void ServerInitiateDeath()
    {
        isDead = true;
        RpcOnDeath(lastAttackerNetId);

        GameSessionManager session = GameSessionManager.Instance;
        if (session != null)
        {
            session.ServerRecordKill(lastAttackerNetId, netId);
        }

        PZKNetworkManager manager = NetworkManager.singleton as PZKNetworkManager;
        if (manager != null && connectionToClient != null)
        {
            TargetOnDeathInfo(connectionToClient, manager.RespawnDelay);
            manager.RequestRespawn(connectionToClient);
        }
    }

    [TargetRpc]
    private void TargetOnDeathInfo(NetworkConnectionToClient target, float respawnDelay)
    {
        PlayerDamageFeedback feedback = GetComponent<PlayerDamageFeedback>();
        if (feedback != null)
        {
            feedback.ShowDeathOverlay(respawnDelay);
        }
    }

    /// <summary>
    /// Appelé par PZKNetworkManager après le délai de respawn.
    /// Téléporte le joueur, réinitialise la santé et l'état de mort.
    /// </summary>
    [Server]
    public void ServerRespawn(Vector3 spawnPosition)
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            transform.position = spawnPosition;
            cc.enabled = true;
        }
        else
        {
            transform.position = spawnPosition;
        }

        isDead = false;
        currentHealth = maxHealth;
        lastAttackerNetId = 0;

        RpcOnRespawn();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SyncVar Hooks
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Hook SyncVar déclenché à chaque changement de santé.
    /// Met à jour l'UI si un PlayerHealthUI est présent dans les enfants.
    /// </summary>
    private void OnHealthChanged(int oldHealth, int newHealth)
    {
        if (cachedHealthUI == null)
        {
            cachedHealthUI = GetComponentInChildren<PlayerHealthUI>();
        }

        if (cachedHealthUI != null)
        {
            cachedHealthUI.UpdateHealthUI(newHealth, maxHealth);
        }
        else
        {
            Debug.Log($"[PlayerHealth] Santé changée : {oldHealth} -> {newHealth}");
        }
    }

    /// <summary>
    /// Hook SyncVar pour l'état de mort.
    /// Désactive/réactive les composants de contrôle du joueur local.
    /// </summary>
    private void OnIsDeadChanged(bool wasDead, bool isNowDead)
    {
        if (isLocalPlayer)
        {
            SetLocalPlayerControlsEnabled(!isNowDead);

            if (!isNowDead && wasDead)
            {
                PlayerDamageFeedback feedback = GetComponent<PlayerDamageFeedback>();
                if (feedback != null)
                {
                    feedback.HideDeathOverlay();
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RPCs — Client Feedback
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Feedback directionnel envoyé uniquement à la victime.
    /// Utilisé pour les indicateurs de dégâts directionnels et le screen shake.
    /// </summary>
    [TargetRpc]
    private void TargetOnDamage(NetworkConnectionToClient target, int amount, Vector3 hitDirection)
    {
        PlayerDamageFeedback feedback = GetComponent<PlayerDamageFeedback>();
        if (feedback != null)
        {
            feedback.OnDamageReceived(amount, hitDirection);
        }
    }

    /// <summary>
    /// Effet visuel global au point d'impact, visible par tous les clients.
    /// Utilise le NetworkEffectManager pour spawner un VFX poolé + son.
    /// </summary>
    [ClientRpc]
    private void RpcOnDamage(Vector3 hitPoint)
    {
        NetworkEffectManager effects = NetworkEffectManager.Instance;
        if (effects != null)
        {
            effects.PlayEffectLocally(NetworkEffectType.BloodSplatter, hitPoint, Quaternion.identity);
            effects.PlaySoundLocally(NetworkSoundType.HitFlesh, hitPoint);
        }
    }

    /// <summary>
    /// Notification de mort envoyée à tous les clients.
    /// Déclenche les effets visuels de mort et fournit le netId du tueur pour le kill feed.
    /// </summary>
    [ClientRpc]
    private void RpcOnDeath(uint killerNetId)
    {
        NetworkEffectManager effects = NetworkEffectManager.Instance;
        if (effects != null)
        {
            effects.PlayEffectLocally(NetworkEffectType.DeathEffect, transform.position, Quaternion.identity);
            effects.PlaySoundLocally(NetworkSoundType.Death, transform.position);
        }

        Debug.Log($"[PlayerHealth] Joueur mort. Tueur netId: {killerNetId}");
    }

    /// <summary>
    /// Notification de respawn envoyée à tous les clients.
    /// Réinitialise l'état visuel du joueur.
    /// </summary>
    [ClientRpc]
    private void RpcOnRespawn()
    {
        NetworkEffectManager effects = NetworkEffectManager.Instance;
        if (effects != null)
        {
            effects.PlayEffectLocally(NetworkEffectType.RespawnFlash, transform.position + Vector3.up, Quaternion.identity);
            effects.PlaySoundLocally(NetworkSoundType.Respawn, transform.position);
        }

        Debug.Log("[PlayerHealth] Joueur respawné.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Utility
    // ─────────────────────────────────────────────────────────────────────────

    private void SetLocalPlayerControlsEnabled(bool controlsEnabled)
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        WeaponSystem weapon = GetComponent<WeaponSystem>();
        PlayerHighlightObject highlight = GetComponent<PlayerHighlightObject>();

        if (movement != null)
        {
            movement.enabled = controlsEnabled;
        }

        if (weapon != null)
        {
            weapon.enabled = controlsEnabled;
        }

        if (highlight != null)
        {
            highlight.enabled = controlsEnabled;
        }
    }
}
