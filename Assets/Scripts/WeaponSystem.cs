using Mirror;
using UnityEngine;
using System;

/// <summary>
/// Système de combat de base, serveur-autoritaire.
/// Le client envoie une intention d'attaque avec direction de visée via Command.
/// Le serveur valide et exécute le raycast depuis la position réelle du joueur.
/// </summary>
public class WeaponSystem : NetworkBehaviour
{
    [Header("Combat — Valeurs par défaut (écrasées par WeaponData si présent)")]
    [SerializeField] private int baseDamage = 10;
    [SerializeField] private float attackRadius = 2.0f;
    [SerializeField] private LayerMask attackMask;
    [SerializeField] private float eyeHeight = 1.65f;
    [SerializeField] private float attackCooldown = 0.5f;

    [SerializeField]
    [Tooltip("Animator du joueur pour les animations d'attaque.")]
    private Animator playerAnimator;

    [Header("Mixamo / Hands Pose Fix")]
    [SerializeField]
    [Tooltip("Active un correctif runtime des doigts pendant l'attaque pour éviter des poses indésirables (Mixamo).")]
    private bool enableHandPoseFix = true;

    [SerializeField]
    [Tooltip("Durée (secondes) pendant laquelle on force une pose de main neutre/poing après le trigger d'attaque.")]
    private float handPoseFixDuration = 0.25f;

    [SerializeField]
    [Tooltip("Valeur cible pour les muscles de doigts. 0 = neutre, 1 = poing fermé (selon rig humanoid).")]
    private float fingerMuscleTarget = 0.75f;

    private HumanPoseHandler humanPoseHandler;
    private HumanPose humanPose;
    private int[] fingerMuscleIndices;
    private float handPoseFixEndTime;
    private float lastAttackTime;
    private PlayerCameraController cachedCameraController;
    private PlayerInputHandler inputHandler;
    private PlayerInventory cachedInventory;
    private ItemDatabase cachedItemDatabase;

    private void Awake()
    {
        if (playerAnimator == null)
        {
            playerAnimator = GetComponentInChildren<Animator>();
        }

        TryInitializeHumanoidHandPoseFix();
    }

    private void Start()
    {
        cachedCameraController = GetComponent<PlayerCameraController>();
        cachedInventory = GetComponent<PlayerInventory>();
        if (cachedInventory != null)
        {
            cachedItemDatabase = cachedInventory.itemDatabase;
        }

        if (isLocalPlayer)
        {
            inputHandler = GetComponent<PlayerInputHandler>();
            SubscribeInput();
        }
    }

    private void OnEnable()
    {
        if (inputHandler != null)
        {
            SubscribeInput();
        }
    }

    private void OnDisable()
    {
        UnsubscribeInput();
    }

    private void OnDestroy()
    {
        UnsubscribeInput();
    }

    private void SubscribeInput()
    {
        if (inputHandler == null) return;
        inputHandler.OnAttackPressed -= HandleAttackInput;
        inputHandler.OnAttackPressed += HandleAttackInput;
    }

    private void UnsubscribeInput()
    {
        if (inputHandler == null) return;
        inputHandler.OnAttackPressed -= HandleAttackInput;
    }

    private void LateUpdate()
    {
        if (!enableHandPoseFix)
        {
            return;
        }

        if (humanPoseHandler == null || fingerMuscleIndices == null)
        {
            return;
        }

        if (Time.time > handPoseFixEndTime)
        {
            return;
        }

        ApplyFingerMusclesOverride();
    }

    private void HandleAttackInput()
    {
        if (playerAnimator != null)
        {
            BeginHandPoseFixIfEnabled();
            string trigger = ResolveLocalAnimTrigger();
            playerAnimator.SetTrigger(trigger);
        }

        Vector3 aimDirection = ComputeAimDirection();
        CmdAttack(aimDirection);
    }

    private string ResolveLocalAnimTrigger()
    {
        if (cachedInventory == null || cachedItemDatabase == null) return "AttackTrigger";
        if (cachedInventory.activeSlotIndex < 0 || cachedInventory.activeSlotIndex >= cachedInventory.inventorySlots.Count) return "AttackTrigger";

        ItemSlot slot = cachedInventory.inventorySlots[cachedInventory.activeSlotIndex];
        if (slot.IsEmpty) return "AttackTrigger";

        ItemData itemData = cachedItemDatabase.GetItemById(slot.itemId);
        if (itemData == null || itemData.weaponData == null) return "AttackTrigger";

        return string.IsNullOrEmpty(itemData.weaponData.animationTrigger) ? "AttackTrigger" : itemData.weaponData.animationTrigger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Client — Aim Direction (TPS/FPS unifié)
    // ─────────────────────────────────────────────────────────────────────────

    private Vector3 ComputeAimDirection()
    {
        if (cachedCameraController == null || cachedCameraController.playerCamera == null)
        {
            return transform.forward;
        }

        return cachedCameraController.playerCamera.transform.forward;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Server — Attack Validation
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Commande envoyée par le client local pour déclencher une attaque.
    /// Le serveur valide la direction, applique un cooldown, et exécute le raycast
    /// depuis la position réelle du joueur (pas la caméra du client).
    /// </summary>
    [Command]
    private void CmdAttack(Vector3 aimDirection)
    {
        ResolveWeaponStats(out int damage, out float range, out float cooldown, out string animTrigger);

        if (Time.time < lastAttackTime + cooldown)
        {
            return;
        }
        lastAttackTime = Time.time;

        if (aimDirection.sqrMagnitude < 0.1f)
        {
            return;
        }

        Vector3 normalizedDirection = aimDirection.normalized;

        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        float maxDistance = range;

        int maskValue = attackMask;
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycastLayer >= 0)
        {
            maskValue &= ~(1 << ignoreRaycastLayer);
        }

        int selfLayer = gameObject.layer;
        maskValue &= ~(1 << selfLayer);

        Debug.DrawLine(origin, origin + normalizedDirection * maxDistance, Color.red, 2.0f);

        RaycastHit hitInfo;
        bool hit = Physics.Raycast(origin, normalizedDirection, out hitInfo, maxDistance, maskValue, QueryTriggerInteraction.Ignore);

        RpcPlayAttackAnimation();

        if (!hit)
        {
            return;
        }

        PlayerHealth targetHealth = hitInfo.collider.GetComponentInParent<PlayerHealth>();
        if (targetHealth == null)
        {
            return;
        }

        if (targetHealth.gameObject == gameObject)
        {
            return;
        }

        targetHealth.TakeDamage(damage, netIdentity);

        if (connectionToClient != null)
        {
            TargetHitConfirmed(connectionToClient);
        }
    }

    /// <summary>
    /// Résout les stats d'arme depuis le WeaponData du slot actif,
    /// ou utilise les valeurs par défaut si aucune arme n'est équipée.
    /// </summary>
    private void ResolveWeaponStats(out int damage, out float range, out float cooldown, out string animTrigger)
    {
        damage = baseDamage;
        range = attackRadius;
        cooldown = attackCooldown;
        animTrigger = "AttackTrigger";

        if (cachedInventory == null || cachedItemDatabase == null) return;
        if (cachedInventory.activeSlotIndex < 0 || cachedInventory.activeSlotIndex >= cachedInventory.inventorySlots.Count) return;

        ItemSlot slot = cachedInventory.inventorySlots[cachedInventory.activeSlotIndex];
        if (slot.IsEmpty) return;

        ItemData itemData = cachedItemDatabase.GetItemById(slot.itemId);
        if (itemData == null || itemData.weaponData == null) return;

        WeaponData wpn = itemData.weaponData;
        damage = wpn.damage;
        range = wpn.range;
        cooldown = wpn.cooldown;
        if (!string.IsNullOrEmpty(wpn.animationTrigger))
        {
            animTrigger = wpn.animationTrigger;
        }
    }

    [TargetRpc]
    private void TargetHitConfirmed(NetworkConnectionToClient target)
    {
        PlayerDamageFeedback feedback = GetComponent<PlayerDamageFeedback>();
        if (feedback != null)
        {
            feedback.ShowHitMarker();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RPCs
    // ─────────────────────────────────────────────────────────────────────────

    [ClientRpc]
    private void RpcPlayAttackAnimation()
    {
        if (isLocalPlayer)
        {
            return;
        }

        if (playerAnimator == null)
        {
            return;
        }

        BeginHandPoseFixIfEnabled();
        playerAnimator.SetTrigger("AttackTrigger");

        NetworkEffectManager effects = NetworkEffectManager.Instance;
        if (effects != null)
        {
            effects.PlaySoundLocally(NetworkSoundType.WeaponSwing, transform.position);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Mixamo Hand Pose Fix
    // ─────────────────────────────────────────────────────────────────────────

    private void BeginHandPoseFixIfEnabled()
    {
        if (!enableHandPoseFix)
        {
            return;
        }

        if (handPoseFixDuration <= 0.0f)
        {
            return;
        }

        handPoseFixEndTime = Time.time + handPoseFixDuration;
    }

    private void TryInitializeHumanoidHandPoseFix()
    {
        humanPoseHandler = null;
        fingerMuscleIndices = null;
        handPoseFixEndTime = 0.0f;

        if (!enableHandPoseFix)
        {
            return;
        }

        if (playerAnimator == null)
        {
            return;
        }

        if (!playerAnimator.isHuman)
        {
            return;
        }

        Avatar avatar = playerAnimator.avatar;
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            return;
        }

        Transform root = playerAnimator.transform;
        if (root == null)
        {
            return;
        }

        humanPoseHandler = new HumanPoseHandler(avatar, root);
        humanPose = new HumanPose();

        fingerMuscleIndices = BuildFingerMuscleIndexList();
    }

    private int[] BuildFingerMuscleIndexList()
    {
        int muscleCount = HumanTrait.MuscleCount;
        int[] tmp = new int[muscleCount];
        int count = 0;

        for (int i = 0; i < muscleCount; i++)
        {
            string muscleName = HumanTrait.MuscleName[i];
            if (string.IsNullOrEmpty(muscleName))
            {
                continue;
            }

            bool isFinger =
                muscleName.IndexOf("Thumb", StringComparison.OrdinalIgnoreCase) >= 0 ||
                muscleName.IndexOf("Index", StringComparison.OrdinalIgnoreCase) >= 0 ||
                muscleName.IndexOf("Middle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                muscleName.IndexOf("Ring", StringComparison.OrdinalIgnoreCase) >= 0 ||
                muscleName.IndexOf("Little", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isFinger)
            {
                continue;
            }

            tmp[count] = i;
            count++;
        }

        int[] result = new int[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = tmp[i];
        }

        return result;
    }

    private void ApplyFingerMusclesOverride()
    {
        if (humanPoseHandler == null)
        {
            return;
        }

        humanPoseHandler.GetHumanPose(ref humanPose);

        if (humanPose.muscles == null || fingerMuscleIndices == null)
        {
            return;
        }

        float target = Mathf.Clamp01(fingerMuscleTarget);
        for (int i = 0; i < fingerMuscleIndices.Length; i++)
        {
            int idx = fingerMuscleIndices[i];
            if (idx < 0 || idx >= humanPose.muscles.Length)
            {
                continue;
            }

            humanPose.muscles[idx] = target;
        }

        humanPoseHandler.SetHumanPose(ref humanPose);
    }
}
