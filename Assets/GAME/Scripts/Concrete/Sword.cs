using Combat;
using UnityEngine;

public class Sword : MonoBehaviour, IWeapon
{
    [SerializeField] private WeaponHitboxSensor _hitbox;
    [SerializeField] private SwordCombatMachine _swordCombatMachine;
    public ICombat CreateCombat(ICombatManager combatManager)
    {
        //var logic = new SwordCombatMachine(this);
        var logic = _swordCombatMachine;
        _hitbox.OnHit += logic.OnWeaponCollision;

        return logic;
    }
}