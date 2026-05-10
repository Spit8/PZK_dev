using UnityEngine;

/// <summary>
/// ScriptableObject définissant les stats d'une arme (Phase 3.6).
/// Référencé par ItemData.weaponData pour les items équipables en combat.
/// WeaponSystem lit ces données depuis le slot actif du joueur.
/// </summary>
[CreateAssetMenu(fileName = "NewWeapon", menuName = "PZK/WeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("Dégâts")]
    [Tooltip("Dégâts infligés par une attaque")]
    public int damage = 10;

    [Header("Portée")]
    [Tooltip("Portée maximale de l'attaque (mètres)")]
    public float range = 2.0f;

    [Header("Vitesse")]
    [Tooltip("Délai minimum entre deux attaques (secondes)")]
    public float cooldown = 0.5f;

    [Header("Type")]
    public WeaponAttackType attackType = WeaponAttackType.Melee;

    [Header("Animation")]
    [Tooltip("Nom du trigger Animator pour l'attaque. Vide = 'AttackTrigger' par défaut.")]
    public string animationTrigger = "AttackTrigger";
}

public enum WeaponAttackType : byte
{
    Melee = 0,
    Ranged = 1
}
