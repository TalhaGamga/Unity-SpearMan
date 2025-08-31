using Combat;
using R3;
using UnityEngine;

public class Sword : MonoBehaviour, IWeapon
{
    [SerializeField] private WeaponHitboxSensor _hitbox;

    public ICombat CreateCombat(ICombatManager combatManager)
    {
        var logic = new SwordCombat(this);
        _hitbox.OnHit += logic.OnWeaponCollision;

        return logic;
    }
}