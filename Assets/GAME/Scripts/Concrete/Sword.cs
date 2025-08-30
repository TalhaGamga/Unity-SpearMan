using Combat;
using R3;
using UnityEngine;

public class Sword : MonoBehaviour, IWeapon
{
    [SerializeField] private WeaponHitboxSensor _hitbox;

    public ICombat CreateCombat(ICombatManager combatManager, Subject<CombatSnapshot> snapshotStream, Subject<CombatTransition> transitionStream)
    {
        var logic = new SwordCombatMachine(this, snapshotStream, transitionStream);
        _hitbox.OnHit += logic.OnWeaponCollision;

        return logic;
    }
}