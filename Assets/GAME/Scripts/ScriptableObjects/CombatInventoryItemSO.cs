using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/EquipableInventoryItemSO")]
public class CombatInventoryItemSO : InventoryItemSO, ICombatInventoryItem
{
    public GameObject WeaponPrefab => weaponPrefab;
    [SerializeField] private GameObject weaponPrefab; // Prefab reference
    private GameObject weaponObj;

    public void Use(IEquipmentManager equipmentManager)
    {
        if (weaponPrefab == null)
        {
            Debug.LogError($"Equipable-CombatPrefab is missing in {ItemName} SO!");
            return;
        }

        if (weaponObj == null)
        {
            weaponObj = Object.Instantiate(weaponPrefab);
        }

        IEquipable equipable = weaponObj.GetComponent<IEquipable>();

        if (equipable != null)
        {
            equipable.Equip(equipmentManager);
            Debug.Log($"{ItemName} has been equipped.");
        }

        else
        {
            Debug.LogError($"EquipablePrefab {weaponPrefab.name} does not have an IEquipable component!");
        }
    }

    public void Use(ICombatManager combatManager)
    {
        //if (weaponPrefab == null)
        //{
        //    Debug.LogError($"CombatPrefab is missing in {ItemName} SO!");
        //    return;
        //}

        //if (weaponObj == null)
        //{
        //    weaponObj = Object.Instantiate(weaponPrefab);
        //}

        //IWeapon weapon = weaponObj.GetComponent<IWeapon>();

        //if (weapon != null)
        //{
        //    weapon.SetCombat(combatManager); // This must not be done here.
        //    Debug.Log($"{ItemName} has set the Combat.");
        //}

        //else
        //{
        //    Debug.LogError($"WeaponPrefab {weaponPrefab.name} does not have an IWeapon component!");
        //}
    }
}
