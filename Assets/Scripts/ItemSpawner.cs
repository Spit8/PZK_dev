using UnityEngine;
using Mirror;
using System.Collections;

public class ItemSpawner : NetworkBehaviour
{
    public static ItemSpawner Instance { get; private set; }

    public GameObject[] lootPrefabs;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ItemSpawner] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void OnStartServer()
    {
        SpawnInitialLoot();
    }

    [Server]
    void SpawnInitialLoot()
    {
        if (lootPrefabs == null || lootPrefabs.Length == 0)
        {
            Debug.LogWarning("[ItemSpawner] lootPrefabs est vide � aucun loot initial spawn�.");
            return;
        }

        for (int i = 0; i < 5; i++)
        {
            int index = Random.Range(0, lootPrefabs.Length);
            Vector3 position = new Vector3(Random.Range(-10f, 10f), 1f, Random.Range(-10f, 10f));
            SpawnLoot(index, position);
        }
    }

    [Server]
    public GameObject SpawnLoot(int index, Vector3 position)
    {
        if (index < 0 || index >= lootPrefabs.Length || lootPrefabs[index] == null)
        {
            Debug.LogWarning($"[ItemSpawner] Index {index} invalide ou prefab null.");
            return null;
        }

        GameObject prefab = lootPrefabs[index];
        GameObject obj = Instantiate(prefab, position, Quaternion.identity);
        NetworkServer.Spawn(obj);
        StartCoroutine(ForceNonKinematic(obj));
        return obj;
    }

    IEnumerator ForceNonKinematic(GameObject obj)
    {
        yield return null; // attend une frame
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = false;
    }
}
