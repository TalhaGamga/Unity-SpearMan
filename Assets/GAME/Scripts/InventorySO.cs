using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/InventorySO")]
public class InventorySO : ScriptableObject
{
    public InventoryItemSO[] InventoryItems;
} 