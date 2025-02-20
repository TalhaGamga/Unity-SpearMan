using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Inventory : IInventory
{
    public int Capacity { get; private set; }
    public IReadOnlyList<IInventorySlot> Slots => _slots.AsReadOnly();

    private List<IInventorySlot> _slots;

    public Inventory(int capacity)
    {
        Capacity = capacity;
        _slots = new List<IInventorySlot>();

        for (int i = 0; i < capacity; i++)
        {
            _slots.Add(new InventorySlot());
        }
    }

    public bool AddItem(IInventoryItem item, int amount = 1)
    {
        foreach (var slot in _slots)
        {
            if (slot.Item != null && slot.Item.ItemName == item.ItemName
                && slot.StackCount < slot.MaxStackSize)
            {
                int spaceLeft = slot.MaxStackSize - slot.StackCount;
                int amountToAdd = Mathf.Min(spaceLeft, amount);

                slot.AddToStack(amountToAdd);
                amount -= amountToAdd;

                if (amount <= 0)
                {
                    return true;
                }
            }
        }

        foreach (var slot in _slots)
        {
            if (slot.IsEmpty)
            {
                slot.SetItem(item, amount);
                return true;
            }
        }

        Debug.LogWarning("Inventory is full!");
        return false;
    }

    public bool RemoveItem(IInventoryItem item, int amount = 1)
    {
        foreach (var slot in _slots)
        {
            if (slot.Item != null && slot.Item.ItemName == item.ItemName)
            {
                if (slot.StackCount >= amount)
                {
                    slot.RemoveFromStack(amount);
                    return true;
                }
                else
                {
                    amount -= slot.StackCount;
                    slot.ClearSlot();
                }
            }
        }

        Debug.LogWarning("Item not found in sufficient quantity!");
        return false;
    }

    public bool HasItem(IInventoryItem item)
    {
        return _slots.Any(slot => slot.Item == item);
    }

    public IInventoryItem GetItem(string itemName)
    {
        return _slots.FirstOrDefault(slot => !slot.IsEmpty && slot.Item.ItemName == itemName)?.Item;
    }

    public void ClearInventory()
    {
        foreach (var slot in _slots)
        {
            slot.ClearSlot();
        }

        Debug.Log("Inventory cleared");
    }
}