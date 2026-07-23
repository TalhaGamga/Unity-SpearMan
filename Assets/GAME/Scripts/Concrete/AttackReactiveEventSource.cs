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
        // Always start with damage
        var stream = Observable.Return<IReactiveEvent>(
            new DamageEvent(_attack.Damage)
        );

        // Impact (physics)
        if (_attack.HasImpact)
        {
            var impact = new ImpactData
            {
                Direction = _hit.Direction,
                Force = _hit.Speed * _attack.Impact.ForceMultiplier,
                Point = _hit.Point
            };

            stream = stream.Concat(
                Observable.Return<IReactiveEvent>(
                    new ImpactEvent(impact)
                )
            );
        }

        // Reaction (state machine)
        if (_attack.HasReaction)
        {
            var settings = _attack.Reaction;
            var reaction = new HitReaction
            {
                Type = settings.Type,
                Direction = new Vector2(_hit.Direction.x, _hit.Direction.z),
                Force = _hit.Speed * settings.ForceMultiplier,
                Duration = settings.Duration,
                LocksMovementInput = settings.LocksMovementInput,
                LocksCombatInput = settings.LocksCombatInput,
                AllowsAirDrift = settings.AllowsAirDrift
            };

            stream = stream.Concat(
                Observable.Return<IReactiveEvent>(
                    new HitReactionEvent(reaction)
                )
            );
        }

        // Destruct
        if (_attack.IsDestructive)
        {
            var destruct = new DestructData
            {
                Direction = _hit.Direction,
                Force = _hit.Speed * _attack.Destruct.ForceMultiplier,
                Point = _hit.Point
            };

            stream = stream.Concat(
                Observable.Return<IReactiveEvent>(
                    new DestructEvent(destruct)
                )
            );
        }

        // Slice (geometry)
        if (_attack.CanSlice && _hit.SlicePlaneNormal != Vector3.zero)
        {
            var slice = new SliceData(
                _hit.Point,
                _hit.SlicePlaneNormal,
                _hit.Speed * _attack.Slice.ForceMultiplier
            );

            stream = stream.Concat(
                Observable.Return<IReactiveEvent>(
                    new SliceEvent(slice)
                )
            );
        }

        return stream;
    }
}