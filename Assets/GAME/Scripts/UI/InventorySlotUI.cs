using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour, IInventorySlotUI
{
    [SerializeField] Image itemIcon;
    [SerializeField] private TextMeshProUGUI stackCountText;

    private IInventorySlot _inventorySlot;

    public void SetSlot(IInventorySlot inventorySlot)
    {
        Debug.Log("Settled");
        if (_inventorySlot != null)
        {
            _inventorySlot.OnSlotUpdated -= UpdateUI; // Unsubscribe from old slot
        }

        _inventorySlot = inventorySlot;
        _inventorySlot.OnSlotUpdated += UpdateUI; // Subscribe to new slot

        UpdateUI();
    }

    private void OnDisable()
    {
        if (_inventorySlot != null)
        {
            _inventorySlot.OnSlotUpdated -= UpdateUI;
        }
    }

    public void UpdateUI()
    {
        if (_inventorySlot.IsEmpty)
        {
            itemIcon.enabled = false;
            stackCountText.text = "";
        }

        else
        {
            itemIcon.sprite = _inventorySlot.Item.ItemIcon;
            itemIcon.enabled = true;
            stackCountText.text = _inventorySlot.StackCount > 1 ? _inventorySlot.StackCount.ToString() : "";
        }
    }

    public void OnSlotClicked()
    {
        _inventorySlot.UseItem();
    }

    public void ClearSlot()
    {
        _inventorySlot = null;
        itemIcon.sprite = null;
        itemIcon.enabled = false;
        stackCountText.text = "";
    }
}