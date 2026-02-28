using UnityEngine;
using Mirror;
using System.Collections;

public class itemSpawner : NetworkBehaviour
{
    public static itemSpawner Instance;

    public GameObject[] lootPrefabs;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnStartServer()
    {
        SpawnInitialLoot();
    }

    [Server]
    void SpawnInitialLoot()
    {

        // Spawn aléatoire
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
