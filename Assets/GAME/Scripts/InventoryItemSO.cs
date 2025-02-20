using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/InventoryItemSO")]
public class InventoryItemSO : ScriptableObject, IInventoryItem
{
    public string ItemName => itemName;

    public Sprite ItemIcon => itemIcon;

    [SerializeField] private string itemName;
    [SerializeField] private Sprite itemIcon;
}