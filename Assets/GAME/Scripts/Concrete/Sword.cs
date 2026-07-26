using System.Collections.Generic;
using Combat;
using UnityEngine;

public class Sword : MonoBehaviour, IWeapon
{
    [SerializeField] private WeaponHitboxSensor _hitbox;
    [SerializeField] private SwordCombatMachine _swordCombatMachine;
    [SerializeField] private AttackDatabase _attackDatabase;
    [SerializeField] private ReactiveEventDispatcher _dispatcher;

    private Transform _owner;

    public ICombat CreateCombat(ICombatManager combatManager)
    {
        _owner = (combatManager as Component)?.transform;

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
            if (IsOwnerCollider(hit))
                continue;

            GameObject target = hit.attachedRigidbody != null
                ? hit.attachedRigidbody.gameObject
                : hit.gameObject;

            if (target == null)
                continue;

            if (hitTargets.Contains(target))
                continue;

            hitTargets.Add(target);

            Vector3 velocity = _hitbox.Velocity;
            Vector3 hitPoint = hit.ClosestPoint(_hitbox.Position);
            Vector3 fallbackDirection = target.transform.position - _hitbox.Position;
            var hitContext = new HitContext(
                velocity,
                hitPoint,
                fallbackDirection,
                _hitbox.BladeDirection
            );

            var source = new AttackReactiveEventSource(
                attack,
                hitContext
            );

            _dispatcher.Apply(source, target);
        }
    }

    private bool IsOwnerCollider(Collider hit)
    {
        if (_owner == null || hit == null)
            return false;

        if (hit.transform == _owner || hit.transform.IsChildOf(_owner))
            return true;

        Transform rigidbodyTransform = hit.attachedRigidbody?.transform;
        return rigidbodyTransform != null &&
               (rigidbodyTransform == _owner || rigidbodyTransform.IsChildOf(_owner));
    }
}
