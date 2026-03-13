using Mirror;
using UnityEngine;

/// <summary>
/// Gestion simple de la santé d'un joueur côté réseau.
/// Le serveur est seul autoritaire sur les modifications de santé.
/// </summary>
public class PlayerHealth : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnHealthChanged))]
    public int currentHealth;

    [SyncVar]
    public int maxHealth = 100;

    /// <summary>
    /// Applique des dégâts côté serveur uniquement.
    /// </summary>
    /// <param name="amount">Quantité de dégâts à infliger.</param>
    [Server]
    public void TakeDamage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        int newHealth = currentHealth - amount;
        if (newHealth < 0)
        {
            newHealth = 0;
        }

        currentHealth = newHealth;

        if (currentHealth <= 0)
        {
            RpcOnDeath();
        }
    }

    /// <summary>
    /// Hook SyncVar déclenché à chaque changement de santé.
    /// </summary>
    /// <param name="oldHealth">Ancienne valeur de santé.</param>
    /// <param name="newHealth">Nouvelle valeur de santé.</param>
    /// <summary>
    /// Hook SyncVar déclenché à chaque changement de santé.
    /// Met à jour l'UI si un PlayerHealthUI est présent dans les enfants.
    /// </summary>
    /// <param name="oldHealth">Ancienne valeur de santé.</param>
    /// <param name="newHealth">Nouvelle valeur de santé.</param>
    private void OnHealthChanged(int oldHealth, int newHealth)
    {
        PlayerHealthUI healthUI = GetComponentInChildren<PlayerHealthUI>();
        if (healthUI != null)
        {
            healthUI.UpdateHealthUI(newHealth, maxHealth);
        }
        else
        {
            Debug.Log($"[PlayerHealth] Santé changée : {oldHealth} -> {newHealth}");
        }
    }

    /// <summary>
    /// RPC client déclenché lorsque la santé atteint zéro.
    /// Utilisé pour jouer les effets visuels et audio de mort côté client.
    /// </summary>
    [ClientRpc]
    private void RpcOnDeath()
    {
        Debug.Log("[PlayerHealth] RpcOnDeath déclenché (jouer effets de mort ici).");
        // TODO PZK: désactiver le contrôle joueur, jouer animation, VFX, etc.
    }
}
