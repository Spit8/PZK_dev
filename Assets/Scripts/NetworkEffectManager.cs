using Mirror;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Identifiant des types d'effets visuels réseau.
/// byte sous-jacent pour minimiser la bande passante dans les RPCs.
/// </summary>
public enum NetworkEffectType : byte
{
    BloodSplatter = 0,
    MuzzleFlash = 1,
    HitSpark = 2,
    RespawnFlash = 3,
    DeathEffect = 4
}

/// <summary>
/// Identifiant des sons réseau.
/// byte sous-jacent pour minimiser la bande passante dans les RPCs.
/// </summary>
public enum NetworkSoundType : byte
{
    HitFlesh = 0,
    HitMetal = 1,
    WeaponSwing = 2,
    Pickup = 3,
    Drop = 4,
    Death = 5,
    Respawn = 6
}

/// <summary>
/// Gestionnaire centralisé des effets visuels et audio réseau (Phase 3.1).
/// Singleton NetworkBehaviour attaché à un objet de scène avec NetworkIdentity.
///
/// Architecture :
/// - Le serveur appelle ServerSpawnEffect / ServerPlaySound
/// - Un [ClientRpc] distribue l'ordre à tous les clients connectés
/// - Les effets sont instanciés depuis un NetworkObjectPool local (zéro NetworkServer.Spawn)
/// - Un check headless empêche l'exécution sur un serveur dédié sans GPU
/// - Les WaitForSeconds sont mis en cache par durée pour éviter les allocations GC
/// - Les AudioSources 3D sont poolées pour éviter les allocations de PlayClipAtPoint
///
/// Bande passante : Un seul RPC (byte + Vector3 + Quaternion ≈ 30 bytes)
/// au lieu de NetworkServer.Spawn d'un prefab complet (200+ bytes + sync continue).
///
/// Setup :
/// - Attacher à un GameObject de scène avec NetworkIdentity
/// - Configurer les EffectDefinitions et SoundDefinitions dans l'Inspector
/// - Les prefabs VFX doivent avoir un ParticleSystem à la racine
/// </summary>
public class NetworkEffectManager : NetworkBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────────────────────────────────────────

    public static NetworkEffectManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Definitions (Inspector)
    // ─────────────────────────────────────────────────────────────────────────

    [System.Serializable]
    public struct EffectDefinition
    {
        public NetworkEffectType Type;
        public GameObject Prefab;
        [Tooltip("Nombre d'instances pré-allouées dans le pool")]
        public int PoolSize;
        [Tooltip("Taille max du pool (0 = illimité)")]
        public int MaxPoolSize;
        [Tooltip("Durée de vie de l'effet avant retour au pool (secondes)")]
        public float Lifetime;
    }

    [System.Serializable]
    public struct SoundDefinition
    {
        public NetworkSoundType Type;
        public AudioClip Clip;
        [Range(0f, 1f)]
        public float Volume;
        [Tooltip("Distance max d'audition (mètres)")]
        public float MaxDistance;
    }

    [Header("Définitions VFX")]
    [SerializeField]
    private EffectDefinition[] effectDefinitions;

    [Header("Définitions Audio")]
    [SerializeField]
    private SoundDefinition[] soundDefinitions;

    [Header("Audio Pool")]
    [SerializeField]
    [Tooltip("Nombre d'AudioSources 3D pré-allouées")]
    private int audioPoolSize = 8;

    // ─────────────────────────────────────────────────────────────────────────
    // Internals
    // ─────────────────────────────────────────────────────────────────────────

    private Dictionary<NetworkEffectType, NetworkObjectPool> effectPools;
    private Dictionary<NetworkEffectType, float> effectLifetimes;
    private Dictionary<NetworkSoundType, SoundDefinition> soundLookup;
    private Dictionary<float, WaitForSeconds> waitCache;

    private Queue<AudioSource> audioSourcePool;
    private bool isHeadless;

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NetworkEffectManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        isHeadless = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
        waitCache = new Dictionary<float, WaitForSeconds>(16);

        if (!isHeadless)
        {
            InitializeEffectPools();
            InitializeSoundLookup();
            InitializeAudioPool();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Initialization (Client-only, skip sur serveur headless)
    // ─────────────────────────────────────────────────────────────────────────

    private void InitializeEffectPools()
    {
        effectPools = new Dictionary<NetworkEffectType, NetworkObjectPool>();
        effectLifetimes = new Dictionary<NetworkEffectType, float>();

        if (effectDefinitions == null)
        {
            return;
        }

        for (int i = 0; i < effectDefinitions.Length; i++)
        {
            EffectDefinition def = effectDefinitions[i];
            if (def.Prefab == null)
            {
                continue;
            }

            GameObject poolHost = new GameObject("Pool_" + def.Type.ToString());
            poolHost.transform.SetParent(transform);

            NetworkObjectPool pool = poolHost.AddComponent<NetworkObjectPool>();
            pool.Warmup(def.Prefab, def.PoolSize, def.MaxPoolSize);

            effectPools[def.Type] = pool;
            effectLifetimes[def.Type] = def.Lifetime;
        }
    }

    private void InitializeSoundLookup()
    {
        soundLookup = new Dictionary<NetworkSoundType, SoundDefinition>();

        if (soundDefinitions == null)
        {
            return;
        }

        for (int i = 0; i < soundDefinitions.Length; i++)
        {
            SoundDefinition def = soundDefinitions[i];
            soundLookup[def.Type] = def;
        }
    }

    private void InitializeAudioPool()
    {
        audioSourcePool = new Queue<AudioSource>(audioPoolSize);

        for (int i = 0; i < audioPoolSize; i++)
        {
            GameObject audioObj = new GameObject("PooledAudio_" + i.ToString());
            audioObj.transform.SetParent(transform);

            AudioSource source = audioObj.AddComponent<AudioSource>();
            source.spatialBlend = 1.0f;
            source.playOnAwake = false;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.0f;
            source.maxDistance = 30.0f;
            audioObj.SetActive(false);

            audioSourcePool.Enqueue(source);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Server — API publique (appelé par les systèmes de jeu)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Déclenche un effet visuel sur tous les clients via RPC.
    /// </summary>
    [Server]
    public void ServerSpawnEffect(NetworkEffectType effectType, Vector3 position, Quaternion rotation)
    {
        RpcSpawnEffect(effectType, position, rotation);
    }

    /// <summary>
    /// Joue un son 3D sur tous les clients via RPC.
    /// </summary>
    [Server]
    public void ServerPlaySound(NetworkSoundType soundType, Vector3 position)
    {
        RpcPlaySound(soundType, position);
    }

    /// <summary>
    /// Déclenche simultanément un effet visuel et un son.
    /// Optimisation : un seul RPC au lieu de deux.
    /// </summary>
    [Server]
    public void ServerSpawnEffectWithSound(
        NetworkEffectType effectType,
        NetworkSoundType soundType,
        Vector3 position,
        Quaternion rotation)
    {
        RpcSpawnEffectWithSound(effectType, soundType, position, rotation);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Client — API locale pour les RPCs existants
    // Permet à un [ClientRpc] (ex: PlayerHealth.RpcOnDamage) de déclencher
    // un effet sans passer par un RPC supplémentaire du NetworkEffectManager.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Déclenche un effet visuel localement sur ce client.
    /// Appelable depuis n'importe quel [ClientRpc] pour éviter un double-RPC.
    /// </summary>
    [Client]
    public void PlayEffectLocally(NetworkEffectType effectType, Vector3 position, Quaternion rotation)
    {
        if (isHeadless)
        {
            return;
        }

        SpawnEffectOnClient(effectType, position, rotation);
    }

    /// <summary>
    /// Joue un son localement sur ce client.
    /// Appelable depuis n'importe quel [ClientRpc] pour éviter un double-RPC.
    /// </summary>
    [Client]
    public void PlaySoundLocally(NetworkSoundType soundType, Vector3 position)
    {
        if (isHeadless)
        {
            return;
        }

        PlaySoundOnClient(soundType, position);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RPCs — Exécution client uniquement
    // Sur serveur dédié, les [ClientRpc] sont sérialisés et envoyés
    // mais jamais exécutés localement. Le check isHeadless couvre le mode Host.
    // ─────────────────────────────────────────────────────────────────────────

    [ClientRpc]
    private void RpcSpawnEffect(NetworkEffectType effectType, Vector3 position, Quaternion rotation)
    {
        if (isHeadless)
        {
            return;
        }

        SpawnEffectOnClient(effectType, position, rotation);
    }

    [ClientRpc]
    private void RpcPlaySound(NetworkSoundType soundType, Vector3 position)
    {
        if (isHeadless)
        {
            return;
        }

        PlaySoundOnClient(soundType, position);
    }

    [ClientRpc]
    private void RpcSpawnEffectWithSound(
        NetworkEffectType effectType,
        NetworkSoundType soundType,
        Vector3 position,
        Quaternion rotation)
    {
        if (isHeadless)
        {
            return;
        }

        SpawnEffectOnClient(effectType, position, rotation);
        PlaySoundOnClient(soundType, position);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Client — Implémentation effet local (poolé)
    // ─────────────────────────────────────────────────────────────────────────

    private void SpawnEffectOnClient(NetworkEffectType effectType, Vector3 position, Quaternion rotation)
    {
        if (effectPools == null)
        {
            return;
        }

        NetworkObjectPool pool;
        if (!effectPools.TryGetValue(effectType, out pool))
        {
            return;
        }

        float lifetime;
        if (!effectLifetimes.TryGetValue(effectType, out lifetime))
        {
            lifetime = 2.0f;
        }

        GameObject effectObj = pool.Get(position, rotation);
        if (effectObj == null)
        {
            return;
        }

        ParticleSystem particles = effectObj.GetComponent<ParticleSystem>();
        if (particles != null)
        {
            particles.Clear();
            particles.Play();
        }

        StartCoroutine(ReturnEffectToPool(pool, effectObj, lifetime));
    }

    private void PlaySoundOnClient(NetworkSoundType soundType, Vector3 position)
    {
        if (soundLookup == null)
        {
            return;
        }

        SoundDefinition def;
        if (!soundLookup.TryGetValue(soundType, out def))
        {
            return;
        }

        if (def.Clip == null)
        {
            return;
        }

        AudioSource source = GetPooledAudioSource();
        if (source == null)
        {
            AudioSource.PlayClipAtPoint(def.Clip, position, def.Volume);
            return;
        }

        source.gameObject.SetActive(true);
        source.transform.position = position;
        source.clip = def.Clip;
        source.volume = def.Volume;
        source.maxDistance = def.MaxDistance;
        source.Play();

        StartCoroutine(ReturnAudioToPool(source, def.Clip.length + 0.1f));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pool management
    // ─────────────────────────────────────────────────────────────────────────

    private AudioSource GetPooledAudioSource()
    {
        if (audioSourcePool == null || audioSourcePool.Count == 0)
        {
            return null;
        }

        AudioSource source = audioSourcePool.Dequeue();
        return source;
    }

    private IEnumerator ReturnEffectToPool(NetworkObjectPool pool, GameObject obj, float delay)
    {
        yield return GetCachedWait(delay);

        if (obj == null)
        {
            yield break;
        }

        ParticleSystem particles = obj.GetComponent<ParticleSystem>();
        if (particles != null)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        pool.Return(obj);
    }

    private IEnumerator ReturnAudioToPool(AudioSource source, float delay)
    {
        yield return GetCachedWait(delay);

        if (source == null)
        {
            yield break;
        }

        source.Stop();
        source.clip = null;
        source.gameObject.SetActive(false);

        audioSourcePool.Enqueue(source);
    }

    /// <summary>
    /// Cache les WaitForSeconds par durée arrondie au dixième.
    /// Évite les allocations GC répétées dans les coroutines de retour au pool.
    /// </summary>
    private WaitForSeconds GetCachedWait(float seconds)
    {
        float key = Mathf.Round(seconds * 10.0f) / 10.0f;

        WaitForSeconds wait;
        if (!waitCache.TryGetValue(key, out wait))
        {
            wait = new WaitForSeconds(key);
            waitCache[key] = wait;
        }

        return wait;
    }
}
