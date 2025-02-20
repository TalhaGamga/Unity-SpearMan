using UnityEngine;

public class InventoryUI : MonoBehaviour, IInventoryUI
{
    [SerializeField] private InventorySlotUI[] slotUIs;
    [SerializeField] private InventoryManager inventoryManager;

    private void OnEnable()
    {
        inventoryManager.OnInventoryLoaded += RefreshUI;
    }

    private void OnDisable()
    {
        inventoryManager.OnInventoryLoaded -= RefreshUI;
    }
    public void Initialize(IInventory inventory)
    {
        RefreshUI();
    }

    public void RefreshUI()
    {
        for (int i = 0; i < slotUIs.Length; i++)
        {
            if (i < inventoryManager.Inventory.Slots.Count)
            {
                slotUIs[i].SetSlot(inventoryManager.Inventory.Slots[i]);
            }
            else
            {
                slotUIs[i].ClearSlot();
            }
        }
    }
}