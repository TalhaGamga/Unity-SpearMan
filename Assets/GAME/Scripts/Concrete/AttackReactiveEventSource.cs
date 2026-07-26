using R3;
using UnityEngine;

public sealed class AttackReactiveEventSource : IReactiveEventSource
{
    private readonly AttackDefinition _attack;
    private readonly HitContext _hit;

    public AttackReactiveEventSource(
        AttackDefinition attack,
        HitContext hit)
    {
        _attack = attack;
        _hit = hit;
    }

    public Observable<IReactiveEvent> Stream()
    {
        var stream = Observable.Return<IReactiveEvent>(
            new DamageEvent(_attack.Damage)
        );

        ImpactData impact = default;
        if (_attack.HasImpact)
        {
            impact = BuildImpact();
            stream = stream.Concat(
                Observable.Return<IReactiveEvent>(new ImpactEvent(impact))
            );
        }

        if (_attack.IsDestructive)
        {
            var destruct = new DestructData(_hit.Direction, _hit.Point);
            stream = stream.Concat(
                Observable.Return<IReactiveEvent>(new DestructEvent(destruct))
            );
        }

        if (_attack.CanSlice && _hit.SlicePlaneNormal != Vector3.zero)
        {
            var slice = new SliceData(_hit.Point, _hit.SlicePlaneNormal);
            stream = stream.Concat(
                Observable.Return<IReactiveEvent>(new SliceEvent(slice))
            );
        }

        if (_attack.HasImpact &&
            (_attack.IsDestructive || _attack.CanSlice))
        {
            stream = stream.Concat(
                Observable.Return<IReactiveEvent>(new PieceImpactEvent(impact))
            );
        }

        // Reaction ordering is intentionally provisional.
        if (_attack.HasReaction)
        {
            var settings = _attack.Reaction;
            Vector3 direction = _hit.GetDirection(PhysicsAxes.YZ);
            var reaction = new HitReaction
            {
                Type = settings.Type,
                Direction = new Vector2(direction.z, direction.y),
                Force = _hit.GetSpeed(PhysicsAxes.YZ) * settings.ForceMultiplier,
                Duration = settings.Duration,
                LocksMovementInput = settings.LocksMovementInput,
                LocksCombatInput = settings.LocksCombatInput,
                AllowsAirDrift = settings.AllowsAirDrift
            };

            stream = stream.Concat(
                Observable.Return<IReactiveEvent>(new HitReactionEvent(reaction))
            );
        }

        return stream;
    }

    private ImpactData BuildImpact()
    {
        PhysicsResponseSettings motion = _attack.Impact.Motion;
        PhysicsAxes translationAxes = PhysicsAxesUtility.Sanitize(
            motion.TranslationAxes
        );
        PhysicsAxes rotationAxes = PhysicsAxesUtility.Sanitize(
            motion.RotationAxes
        );
        PhysicsAxes motionAxes = translationAxes | rotationAxes;

        return new ImpactData(
            _hit.GetDirection(motionAxes),
            _hit.GetSpeed(translationAxes) * motion.LinearMultiplier,
            _hit.Point,
            translationAxes,
            rotationAxes,
            _hit.GetSpeed(motionAxes) * motion.AngularMultiplier
        );
    }
}