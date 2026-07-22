using System.Collections.Generic;
using Combat;
using UnityEngine;

public class Sword : MonoBehaviour, IWeapon
{
    [SerializeField] private WeaponHitboxSensor _hitbox;
    [SerializeField] private SwordCombatMachine _swordCombatMachine;
    [SerializeField] private AttackDatabase _attackDatabase;
    [SerializeField] private ReactiveEventDispatcher _dispatcher;

    public ICombat CreateCombat(ICombatManager combatManager)
    {
        var logic = _swordCombatMachine;
        logic.SetSwordView(this);
        return logic;
    }

    public bool TryGetAttackDefinition(string key, out AttackDefinition attack)
    {
        attack = null;

        if (_attackDatabase == null)
            return false;

        return _attackDatabase.TryGet(key, out attack);
    }

    public void ProcessHitWindow(AttackDefinition attack, HashSet<GameObject> hitTargets)
    {
        if (attack == null || _hitbox == null || _dispatcher == null)
            return;

        var hits = _hitbox.ScanHits();

        foreach (var hit in hits)
        {
            GameObject target = hit.attachedRigidbody != null
                ? hit.attachedRigidbody.gameObject
                : hit.gameObject;

            if (target == null)
                continue;

            if (hitTargets.Contains(target))
                continue;

            hitTargets.Add(target);

            Vector3 direction = (target.transform.position - transform.position).normalized;
            Vector3 hitPoint = hit.ClosestPoint(transform.position);

            var source = new AttackReactiveEventSource(
                attack,
                direction,
                hitPoint
            );

            _dispatcher.Apply(source, target);
        }
    }
}