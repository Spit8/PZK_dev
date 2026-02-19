using UnityEngine;

public class ItemTest : MonoBehaviour
{
    // On glisse notre base de données ici dans l'inspecteur pour le test
    public ItemDatabase database;

    void Start()
    {
        // Test pour la Hache (ID 101)
        TestItem(0);

        // Test d'une erreur (ID inexistant)
        TestItem(999);
    }

    void TestItem(int id)
    {
        ItemData data = database.GetItemById(id);
        if (data != null)
        {
            Debug.Log($"<color=green>[SUCCESS]</color> ID {id} trouvé : {data.itemName}, Poids: {data.weight}kg");
        }
        else
        {
            Debug.LogWarning($"<color=red>[ERROR]</color> ID {id} introuvable dans la base !");
        }
    }
}