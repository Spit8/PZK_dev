using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewItemDatabase", menuName = "PZK/System/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [SerializeField] private List<ItemData> allItems = new List<ItemData>();

    private Dictionary<int, ItemData> itemLookup;

    private void OnEnable()
    {
        RebuildLookup();
    }

    private void RebuildLookup()
    {
        itemLookup = new Dictionary<int, ItemData>(allItems.Count);
        for (int i = 0; i < allItems.Count; i++)
        {
            ItemData data = allItems[i];
            if (data == null) continue;
            itemLookup[data.itemId] = data;
        }
    }

    /// <summary>
    /// Récupère les données d'un item via son ID unique. O(1) via Dictionary.
    /// </summary>
    public ItemData GetItemById(int id)
    {
        if (itemLookup == null)
        {
            RebuildLookup();
        }

        if (itemLookup.TryGetValue(id, out ItemData item))
        {
            return item;
        }

        Debug.LogError($"[ItemDatabase] ID {id} non trouvé dans la base de données !");
        return null;
    }

    /// <summary>
    /// Optionnel : Permet de rafraîchir la liste automatiquement (Editor seulement)
    /// </summary>
    [ContextMenu("Sync Database")]
    private void SyncDatabase()
    {
        // Logique pour trouver automatiquement tous les ItemData dans le projet via AssetDatabase
        // Utile pour éviter d'oublier de glisser un item manuellement
    }
}