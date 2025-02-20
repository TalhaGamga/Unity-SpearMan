using System;
using UnityEngine;

public class InventorySlot : IInventorySlot
{
    public IInventoryItem Item { get; private set; }

    public int StackCount { get; private set; }
    public int MaxStackSize { get; private set; }
    public bool IsEmpty => Item == null;

    public Action OnSlotUpdated { get; set; }

    public InventorySlot()
    {
        StackCount = 0;
    }

    public void SetItem(IInventoryItem item, int count = 1)
    {
        if (Item == null)
        {
            Item = item;
            if (item is IStackableItem stackableItem)
            {
                MaxStackSize = stackableItem.MaxStackSize;
                StackCount = Math.Min(count, MaxStackSize);
                OnSlotUpdated?.Invoke();
            }
        }

        else
        {
            throw new InvalidOperationException("Slot already has an item. Use AddToStack instead.");
        }
    }

    public void ClearSlot()
    {
        Item = null;
        StackCount = 0;
        MaxStackSize = 0;
        OnSlotUpdated?.Invoke();
    }

    public void UseItem()
    {
        if (Item != null)
        {
            Debug.Log("Used");
            EventBus.RaiseItemUsed(new ItemUsedEvent(Item));

            //if (StackCount > 1)
            //{
            //    RemoveFromStack(1);
            //}
            //else
            //{
            //    ClearSlot();
            //}

            OnSlotUpdated?.Invoke();
        }
    }

    public void AddToStack(int count = 1)
    {
        if (Item != null)
        {
            int newStackCount = Math.Min(StackCount + count, MaxStackSize);
            int addedAmount = newStackCount - StackCount;
            StackCount = newStackCount;

            if (addedAmount > 0)
            {
                OnSlotUpdated?.Invoke();
            }
        }

        else
        {
            throw new InvalidOperationException("Cannot add to stack when the slot is empty");
        }
    }

    public void RemoveFromStack(int count = 1)
    {
        if (Item != null && StackCount > 0)
        {
            StackCount = Math.Max(StackCount - count, 0);

            if (StackCount == 0)
            {
                ClearSlot();
            }

            OnSlotUpdated?.Invoke();
        }

        else
        {
            throw new InvalidOperationException("Cannot remove from an empty slot");
        }
    }
}