using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/StackableInventoryItemSO")]
public class StackableInventoryItemSO : InventoryItemSO, IStackableItem
{
    [SerializeField] private int maxStackSize; // Maximum number of items that can be stacked
    private int currentStack = 1; // Default stack count

    public int CurrentStack => currentStack;

    public int MaxStackSize { get { return maxStackSize; } set { } }

    public void RemoveFromStack(int amount)
    {
        currentStack = Mathf.Max(currentStack - amount, 0);
    }

    public bool CanStackWith(IStackableItem otherItem)
    {
        return otherItem is StackableInventoryItemSO otherStackable &&
               otherStackable.ItemName == this.ItemName; // Ensure same item type
    }
}
 