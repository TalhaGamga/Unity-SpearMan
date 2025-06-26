using R3;
using UnityEngine;

public class Sword : MonoBehaviour, IWeapon
{
    [SerializeField] private WeaponHitboxSensor _hitbox;

    public ICombat CreateCombat(ICombatManager combatManager, BehaviorSubject<CombatSnapshot> snapshotStream)
    {
        var logic = new SwordCombat(this, snapshotStream);
        _hitbox.OnHit += logic.OnWeaponCollision;

        return logic;
    }
}