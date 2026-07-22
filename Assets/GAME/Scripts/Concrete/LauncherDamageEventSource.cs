using R3;
using UnityEngine;

public sealed class LauncherDamageEventSource : IReactiveEventSource
{
    private readonly float _damage;
    private readonly Vector3 _direction;
    private readonly float _force;

    public LauncherDamageEventSource(float damage, Vector3 direction, float force)
    {
        _damage = damage;
        _direction = direction;
        _force = force;
    }

    public Observable<IReactiveEvent> Stream()
    {
        //var reaction = new HitReactionData
        //{
        //    Type = HitReactionType.Launch,
        //    Direction = _direction,
        //    Force = _force,
        //    Lift = 1.5f,
        //    CausesUngrounded = true,
        //    CanChainFromAir = false
        //};

        //return Observable.Return<IReactiveEvent>(new DamageEvent(_damage))
        //    .Concat(Observable.Return<IReactiveEvent>(new HitReactEvent(reaction)));

        return null;
    }
}