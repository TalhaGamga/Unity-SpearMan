using System.Collections.Generic;
using UnityEngine;

public class EquipmentManager : MonoBehaviour, IEquipmentManager
{
    private List<IEquipable> equipables;
    private IEquipable equipped;

    public Transform Hand { get { return hand; } private set { } }

    [SerializeField] Transform hand;

    private void OnEnable()
    {
        EventBus.OnItemUsed += onItemUsed;
    }

    private void OnDisable()
    {
        EventBus.OnItemUsed += onItemUsed;
    }

    public void Equip(IEquipable equipable)
    {
        Debug.Log("Equipping:" + equipable.ToString());

        if ((equipped != null && equipped.Equals(equipable)))
        {
            return;
        }

        equipable?.Equip(this);
        equipped = equipable;
    }

    public void Unequip(IEquipable equipable)
    {
        if ((equipped == null) || !(equipped != null && equipped.Equals(equipable)))
        {
            return;
        }

        equipable?.Unequip(this);
        equipped = null;
    }

    public void Pickup()
    {
    }

    private void Update()
    {
        //if (Input.GetKeyDown(KeyCode.Q))
        //{
        //    Equip(defaultWeapon);
        //}

        //if (Input.GetKeyDown(KeyCode.R))
        //{
        //    Unequip(equipped);
        //}
    }

    private void onItemUsed(ItemUsedEvent itemEvent)
    {
        if (itemEvent.Item is IEquipableInventoryItem inventoryItem)
        {
            inventoryItem.Use(this);
        }
    }
}