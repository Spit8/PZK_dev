using Mirror;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI locale pour afficher la santé d'un joueur.
/// Doit être placée sur un GameObject possédant un Slider.
/// </summary>
public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField]
    private Slider healthSlider;

    [SerializeField]
    private PlayerHealth targetHealth;

    private void Start()
    {
        if (targetHealth == null)
        {
            targetHealth = GetComponentInParent<PlayerHealth>();
        }

        if (targetHealth != null && healthSlider != null)
        {
            int initialCurrent = targetHealth.currentHealth;
            int initialMax = targetHealth.maxHealth;
            UpdateHealthUI(initialCurrent, initialMax);
        }
    }

    public void UpdateHealthUI(int current, int max)
    {
        if (healthSlider == null)
        {
            return;
        }

        if (max <= 0)
        {
            healthSlider.value = 0.0f;
            return;
        }

        float ratio = (float)current / (float)max;
        healthSlider.value = ratio;
    }
}
