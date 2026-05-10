using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pool d'objets générique pour éviter les spikes GC causés par
/// Instantiate/Destroy répétés (VFX, projectiles, audio one-shots).
///
/// Architecture :
/// - Warmup() pré-alloue les objets à l'initialisation
/// - Get() retourne un objet inactif, activé et positionné
/// - Return() désactive l'objet et le recycle
/// - Les objets retournés sont reparentés sous le transform du pool
/// - Si le pool est épuisé, un nouvel objet est créé (jusqu'à MaxSize)
///
/// Thread Safety : Non thread-safe. Appeler uniquement depuis le main thread Unity.
/// </summary>
public class NetworkObjectPool : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Prefab à instancier dans le pool")]
    private GameObject prefab;

    [SerializeField]
    [Tooltip("Nombre d'instances pré-allouées")]
    private int initialSize = 10;

    [SerializeField]
    [Tooltip("Taille maximale du pool (0 = illimité)")]
    private int maxSize = 50;

    private Queue<GameObject> available;
    private HashSet<GameObject> inUse;

    public int AvailableCount => available != null ? available.Count : 0;
    public int InUseCount => inUse != null ? inUse.Count : 0;
    public int TotalCount => AvailableCount + InUseCount;

    private void Awake()
    {
        available = new Queue<GameObject>(initialSize);
        inUse = new HashSet<GameObject>();
    }

    /// <summary>
    /// Pré-alloue les objets. Appeler une fois à l'initialisation.
    /// </summary>
    public void Warmup()
    {
        if (prefab == null)
        {
            return;
        }

        int count = initialSize - available.Count;
        for (int i = 0; i < count; i++)
        {
            if (maxSize > 0 && TotalCount >= maxSize)
            {
                break;
            }

            GameObject obj = CreateNewObject();
            available.Enqueue(obj);
        }
    }

    /// <summary>
    /// Initialise le pool programmatiquement avec un prefab et des paramètres spécifiques.
    /// Utilisé quand le pool est créé par code (pas par Inspector).
    /// </summary>
    public void Warmup(GameObject overridePrefab, int count, int max)
    {
        prefab = overridePrefab;
        initialSize = count;
        maxSize = max;

        if (available == null)
        {
            available = new Queue<GameObject>(count);
        }

        if (inUse == null)
        {
            inUse = new HashSet<GameObject>();
        }

        for (int i = 0; i < count; i++)
        {
            GameObject obj = CreateNewObject();
            available.Enqueue(obj);
        }
    }

    /// <summary>
    /// Retourne un objet poolé à la position et rotation données, déjà activé.
    /// Crée un nouvel objet si le pool est vide (jusqu'à MaxSize).
    /// Retourne null si MaxSize est atteint et aucun objet n'est disponible.
    /// </summary>
    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject obj = null;

        while (available.Count > 0)
        {
            obj = available.Dequeue();
            if (obj != null)
            {
                break;
            }
            obj = null;
        }

        if (obj == null)
        {
            if (maxSize > 0 && inUse.Count >= maxSize)
            {
                return null;
            }
            obj = CreateNewObject();
        }

        obj.transform.SetParent(null);
        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);

        inUse.Add(obj);
        return obj;
    }

    /// <summary>
    /// Retourne un objet au pool. Le désactive et le reparente sous le pool.
    /// </summary>
    public void Return(GameObject obj)
    {
        if (obj == null)
        {
            return;
        }

        inUse.Remove(obj);
        obj.SetActive(false);
        obj.transform.SetParent(transform);

        available.Enqueue(obj);
    }

    /// <summary>
    /// Détruit tous les objets du pool (disponibles et en cours d'utilisation).
    /// </summary>
    public void Clear()
    {
        if (available != null)
        {
            while (available.Count > 0)
            {
                GameObject obj = available.Dequeue();
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
        }

        if (inUse != null)
        {
            foreach (GameObject obj in inUse)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            inUse.Clear();
        }
    }

    private void OnDestroy()
    {
        Clear();
    }

    private GameObject CreateNewObject()
    {
        GameObject obj = Instantiate(prefab, transform);
        obj.SetActive(false);
        return obj;
    }
}
