using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/EquipableInventoryItemSO")]
public class EquipableInventoryItemSO : InventoryItemSO, IEquipableInventoryItem
{
    public GameObject WeaponPrefab => equipablePrefab;
    [SerializeField] private GameObject equipablePrefab; // Prefab reference

    public void Use(IEquipmentManager equipmentManager)
    {
        if (equipablePrefab == null)
        {
            Debug.LogError($"EquipablePrefab is missing in {ItemName} SO!");
            return;
        }

        GameObject instance = Object.Instantiate(equipablePrefab);

        IEquipable equipable = instance.GetComponent<IEquipable>();

        if (equipable != null)
        {
            equipable.Equip(equipmentManager);
            Debug.Log($"{ItemName} has been equipped.");
        }

        else
        {
            Debug.LogError($"EquipablePrefab {equipablePrefab.name} does not have an IEquipable component!");
        }
    }
}
