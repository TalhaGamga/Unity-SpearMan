using System.Collections.Generic;
using Combat;
using Movement;
using UnityEngine;

public class Sword : MonoBehaviour, IWeapon
{
    private const string LaunchAttackKey = "Sword_Light_2";
    private const int LaunchArcSegments = 16;

    [SerializeField] private WeaponHitboxSensor _hitbox;
    [SerializeField] private SwordCombatMachine _swordCombatMachine;
    [SerializeField] private AttackDatabase _attackDatabase;
    [SerializeField] private ReactiveEventDispatcher _dispatcher;

    private Transform _owner;
    private Transform _forwardSource;
    private Vector3 _lastPlanarForward = Vector3.forward;

    public ICombat CreateCombat(ICombatManager combatManager)
    {
        _owner = (combatManager as Component)?.transform;
        MovementManager movementManager = _owner != null
            ? _owner.GetComponent<MovementManager>()
            : null;
        _forwardSource = movementManager != null
            ? movementManager.CharacterOrientator
            : _owner;
        updatePlanarForward();

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

        foreach (PlanarWeaponHit planarHit in hits)
        {
            Collider hit = planarHit.Collider;

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
            Vector3 hitPoint = planarHit.Point;
            Vector3 targetCenter = hit.attachedRigidbody != null
                ? hit.attachedRigidbody.worldCenterOfMass
                : hit.bounds.center;
            Vector3 attackOrigin = _owner != null
                ? _owner.position
                : _hitbox.Position;
            Vector3 fallbackDirection = targetCenter - attackOrigin;
            var hitContext = new HitContext(
                velocity,
                hitPoint,
                fallbackDirection,
                _hitbox.BladeDirection,
                getPlanarForward()
            );

            var source = new AttackReactiveEventSource(
                attack,
                hitContext
            );

            _dispatcher.Apply(source, target);
        }
    }

    private Vector3 getPlanarForward()
    {
        updatePlanarForward();
        return _lastPlanarForward;
    }

    private void updatePlanarForward()
    {
        if (_forwardSource == null)
            return;

        float forwardZ = _forwardSource.forward.z;
        if (Mathf.Abs(forwardZ) > 0.1f)
        {
            _lastPlanarForward = Vector3.forward *
                Mathf.Sign(forwardZ);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_hitbox == null ||
            _attackDatabase == null ||
            !_attackDatabase.TryGet(
                LaunchAttackKey,
                out AttackDefinition launchAttack) ||
            launchAttack == null)
        {
            return;
        }

        PhysicsResponseSettings motion = launchAttack.Impact.Motion;
        if (!motion.ClampToForwardArc)
            return;

        resolveGizmoForwardSource();

        Vector3 origin = _hitbox.Position;
        Vector3 forward = getPlanarForward();
        float minimumAngle = Mathf.Clamp(
            Mathf.Min(
                motion.MinimumForwardAngle,
                motion.MaximumForwardAngle
            ),
            0f,
            90f
        );
        float maximumAngle = Mathf.Clamp(
            Mathf.Max(
                motion.MinimumForwardAngle,
                motion.MaximumForwardAngle
            ),
            minimumAngle,
            90f
        );
        float rayLength = Mathf.Clamp(
            motion.LinearForce * 0.15f,
            1f,
            4f
        );

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(origin, forward * rayLength * 0.55f);

        Vector3 previousPoint = origin + getLaunchArcDirection(
            forward,
            minimumAngle
        ) * rayLength;

        Gizmos.color = new Color(1f, 0.55f, 0f, 1f);
        Gizmos.DrawLine(origin, previousPoint);

        for (int i = 1; i <= LaunchArcSegments; i++)
        {
            float interpolation = i / (float)LaunchArcSegments;
            float angle = Mathf.Lerp(
                minimumAngle,
                maximumAngle,
                interpolation
            );
            Vector3 point = origin + getLaunchArcDirection(
                forward,
                angle
            ) * rayLength;
            Gizmos.DrawLine(previousPoint, point);
            previousPoint = point;
        }

        Gizmos.DrawLine(origin, previousPoint);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            origin + getLaunchArcDirection(
                forward,
                minimumAngle
            ) * rayLength,
            $"Launch min {minimumAngle:0}°"
        );
        UnityEditor.Handles.Label(
            origin + getLaunchArcDirection(
                forward,
                maximumAngle
            ) * rayLength,
            $"Launch max {maximumAngle:0}°"
        );
#endif
    }

    private void resolveGizmoForwardSource()
    {
        if (_forwardSource != null)
            return;

        MovementManager movementManager =
            GetComponentInParent<MovementManager>();
        _owner = movementManager != null
            ? movementManager.transform
            : transform.root;
        _forwardSource = movementManager != null
            ? movementManager.CharacterOrientator
            : _owner;
        updatePlanarForward();
    }

    private static Vector3 getLaunchArcDirection(
        Vector3 forward,
        float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return forward * Mathf.Cos(radians) +
            Vector3.up * Mathf.Sin(radians);
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
