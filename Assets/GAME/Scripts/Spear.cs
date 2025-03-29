using UnityEngine;

public class Spear : MonoBehaviour, ISpear
{
    private ICombat<ISpear> combat;
    [SerializeField] private Vector3 snapPosition;
    [SerializeField] private Quaternion snapRotation;
    [SerializeField] private Vector3 snapScale;

    public Transform WeaponTransform => throw new System.NotImplementedException();

    public void Equip(IEquipmentManager equipmentManager)
    {
        Debug.Log("Spear Equip");
        Transform hand = equipmentManager.Hand;
        transform.SetParent(hand);
        transform.SetLocalPositionAndRotation(snapPosition, snapRotation);
        transform.localScale = snapScale;
    }

    public void SetCombat(ICombatManager CombatManager)
    {
        Debug.Log("Setting Combat");
        combat = new SpearCombat(this);
        CombatManager.SetCombat(combat);
    }

    public void Unequip(IEquipmentManager equipmentManager)
    {
        if (equipmentManager is MonoBehaviour eqManager)
        {
            if (eqManager.TryGetComponent<ICombatManager>(out ICombatManager combatManager))
            {
                combatManager.UnsettleCombat(combat);
                combat = null;
            }
        }
    }

    public void Tick()
    {
        Debug.Log("Spear Tick");
    }

    public void FixedTick()
    {
    }
}