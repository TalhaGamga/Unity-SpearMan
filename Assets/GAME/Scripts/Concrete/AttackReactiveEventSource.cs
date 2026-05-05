using R3;
using UnityEngine;

public sealed class AttackReactiveEventSource : IDamageEventSource
{
    private readonly AttackDefinition _attack;
    private readonly Vector3 _direction;
    private readonly Vector3 _hitPoint;

    public AttackReactiveEventSource(
        AttackDefinition attack,
        Vector3 direction,
        Vector3 hitPoint)
    {
        _attack = attack;
        _direction = direction;
        _hitPoint = hitPoint;
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
            var impact = _attack.Impact;

            impact.Direction = _direction;
            impact.Point = _hitPoint;

            stream = stream.Concat(
                Observable.Return<IReactiveEvent>(
                    new ImpactEvent(impact)
                )
            );
        }

        // Reaction (state machine)
        if (_attack.HasReaction)
        {
            var reaction = _attack.Reaction;

            // Convert 3D direction ? 2D for your reaction system
            reaction.Direction = new Vector2(_direction.x, _direction.z);

            stream = stream.Concat(
                Observable.Return<IReactiveEvent>(
                    new HitReactionEvent(reaction)
                )
            );
        }

        // Break
        if (_attack.Breaks)
        {
            stream = stream.Concat(
                Observable.Return<IReactiveEvent>(
                    new BreakEvent()
                )
            );
        }

        return stream;
    }
}