using System;
using UnityEngine;

public class InventoryManager : MonoBehaviour, IInventoryManager
{
    public IInventory Inventory { get; private set; }
    public Action OnInventoryLoaded { get; set; }

    [SerializeField] InventorySO inventorySO;

    private void Start()
    {
        InitializeInventory();
    }

    private void InitializeInventory()
    {
        if (inventorySO == null)
        {
            return;
        }

        Inventory = new Inventory(inventorySO.InventoryItems.Length);

        LoadInitialInventory();

        OnInventoryLoaded?.Invoke();
    }

    private void LoadInitialInventory()
    {
        foreach (var item in inventorySO.InventoryItems)
        {
            if (item != null)
            {
                Inventory.AddItem(item);
            }
        }
    }
}