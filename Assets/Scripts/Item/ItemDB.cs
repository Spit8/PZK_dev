using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "NewItemDatabase", menuName = "PZK/System/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [SerializeField] private List<ItemData> allItems = new List<ItemData>();

    /// <summary>
    /// Récupère les données d'un item via son ID unique.
    /// </summary>
    public ItemData GetItemById(int id)
    {
        // Utilise Linq pour trouver l'item avec l'ID correspondant
        ItemData item = allItems.FirstOrDefault(i => i.itemId == id);

        if (item == null)
        {
            Debug.LogError($"[ItemDatabase] ID {id} non trouvé dans la base de données !");
        }

        return item;
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