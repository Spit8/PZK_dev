using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using System;

/// <summary>
/// Système de combat de base, serveur-autoritaire.
/// Le client ne fait qu'envoyer une intention d'attaque via Command.
/// </summary>
public class WeaponSystem : NetworkBehaviour
{
    [Header("Combat")]
    [SerializeField]
    [Tooltip("Dégâts infligés par une attaque de base.")]
    private int baseDamage = 10;

    [SerializeField]
    [Tooltip("Rayon de la sphère de détection devant le joueur.")]
    private float attackRadius = 2.0f;

    [SerializeField]
    [Tooltip("Distance devant le joueur où la sphère est centrée.")]
    private float attackForwardOffset = 1.5f;

    [SerializeField]
    [Tooltip("Couches valides pour les cibles d'attaque (exclure le joueur local).")]
    private LayerMask attackMask;

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

    private void Awake()
    {
        if (playerAnimator == null)
        {
            playerAnimator = GetComponentInChildren<Animator>();
        }

        TryInitializeHumanoidHandPoseFix();
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

    private void Update()
    {
        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log("DEBUG: Clic physique détecté");
        }

        if (!isLocalPlayer)
        {
            return;
        }

        if (UnityEngine.InputSystem.Mouse.current == null)
        {
            return;
        }

        if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log("DEBUG: Clic Joueur Local -> Envoi Cmd");

            if (playerAnimator != null)
            {
                BeginHandPoseFixIfEnabled();
                playerAnimator.SetTrigger("AttackTrigger");
            }

            CmdAttack();
        }
    }

    /// <summary>
    /// Commande envoyée par le client local pour déclencher une attaque.
    /// La logique de dégâts (raycast et validation de distance) est exécutée exclusivement sur le serveur.
    /// </summary>
    [Command]
    private void CmdAttack()
    {
        float maxDistance = attackRadius;

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        Vector3 origin = mainCamera.transform.position + (mainCamera.transform.forward * attackForwardOffset);
        Vector3 direction = mainCamera.transform.forward;

        int maskValue = attackMask;
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycastLayer >= 0)
        {
            maskValue &= ~(1 << ignoreRaycastLayer);
        }

        int selfLayer = gameObject.layer;
        maskValue &= ~(1 << selfLayer);

        Debug.DrawLine(origin, origin + direction * maxDistance, Color.red, 2.0f);

        RaycastHit hitInfo;
        bool hit = Physics.Raycast(origin, direction, out hitInfo, maxDistance, maskValue, QueryTriggerInteraction.Ignore);
        if (!hit)
        {
            RpcPlayAttackAnimation();
            return;
        }

        PlayerHealth targetHealth = hitInfo.collider.GetComponentInParent<PlayerHealth>();
        if (targetHealth == null)
        {
            RpcPlayAttackAnimation();
            return;
        }

        if (targetHealth.gameObject == gameObject)
        {
            RpcPlayAttackAnimation();
            return;
        }

        targetHealth.TakeDamage(baseDamage);

        RpcPlayAttackAnimation();
    }


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
    }

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

        // Déclenche une fenêtre courte pendant laquelle on force les muscles des doigts après l'évaluation Animator.
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
            // Le correctif s'appuie sur les muscles humanoid. Si Remy n'est pas en Humanoid, on ne fait rien.
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

            // Heuristique robuste: cible les muscles liés aux doigts (stretch/curl + spread) pour les deux mains.
            // Cela permet de neutraliser des poses extrêmes importées de Mixamo (ex: doigt levé).
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
