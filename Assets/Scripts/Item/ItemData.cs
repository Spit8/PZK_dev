using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "PZK/ItemData")]
public class ItemData : ScriptableObject
{
    public int itemId;
    public string itemName;
    public float weight;
    public GameObject worldPrefab; // Le modèle 3D au sol
}