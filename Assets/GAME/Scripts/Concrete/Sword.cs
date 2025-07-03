using R3;
using UnityEngine;

public class Sword : MonoBehaviour, IWeapon
{
    [SerializeField] private WeaponHitboxSensor _hitbox;

    public ICombat CreateCombat(ICombatManager combatManager, BehaviorSubject<CombatSnapshot> snapshotStream, BehaviorSubject<CombatTransition> transitionStream)
    {
        var logic = new SwordCombat(this, snapshotStream,transitionStream);
        _hitbox.OnHit += logic.OnWeaponCollision;

        return logic;
    }
}