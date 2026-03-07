using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "PZK/ItemData")]
public class ItemData : ScriptableObject
{
    [Header("Identité")]
    public int itemId;
    public string itemName;
    [TextArea] public string description;

    [Header("Visuels")]
    public Sprite icon;
    public GameObject worldPrefab; // Modèle 3D au sol
    public GameObject handPrefab;  // Modèle 3D quand tenu en main

    [Header("Logistique")]
    public float weight;
    public bool isStackable;
    public int maxStack = 1;
}