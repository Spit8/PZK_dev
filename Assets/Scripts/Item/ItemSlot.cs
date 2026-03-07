using System;

/// <summary>
/// Représente un slot d'inventaire synchronisé entre le serveur et les clients.
/// Contient l'ID de l'objet et la quantité actuelle.
/// </summary>
[Serializable]
public struct ItemSlot
{
    public int itemId; // ID référant à ItemData (0 ou -1 si vide)
    public int amount; // Quantité dans le slot

    public ItemSlot(int id, int count)
    {
        itemId = id;
        amount = count;
    }

    /// <summary>Vérifie si le slot est vide.</summary>
    public bool IsEmpty => itemId <= 0 || amount <= 0;
}
