using R3;
using UnityEngine;

public sealed class BulletDamageEventSource : IDamageEventSource
{
    private readonly float _damage;
    private readonly Vector3 _force;
    private readonly float _strength;
    private readonly string _fx;
    private readonly Vector3 _origin;
    private readonly Vector3 _direction;

    public BulletDamageEventSource(float damage, float strength, Vector3 origin, Vector3 direction, string fx)
    {
        _damage = damage;
        _strength = strength;
        _direction = direction;
        _origin = origin;
        _fx = fx;
        _force = direction * strength;
    }

    public Observable<IReactiveEvent> Stream(GameObject target)
    {
        return Observable.Return<IReactiveEvent>(new DamageEvent(_damage))
            .Concat(Observable.Return<IReactiveEvent>(new KnockbackEvent(_direction, _strength))
            .Concat(Observable.Return<IReactiveEvent>(new BreakEvent())));
    }
}