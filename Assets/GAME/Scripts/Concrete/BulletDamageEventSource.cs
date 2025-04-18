using R3;
using UnityEngine;

public sealed class BulletDamageEventSource : IDamageEventSource
{
    private readonly float _damage;
    private readonly Vector3 _force;
    private readonly string _fx;

    public BulletDamageEventSource(float damage, Vector3 force, string fx)
    {
        _damage = damage;
        _force = force;
        _fx = fx;
    }

    public Observable<IDamageEvent> Stream(GameObject target)
    {
        Debug.Log("Streaming");
        return Observable.Return<IDamageEvent>(new DamageEvent(_damage))
            .Concat(Observable.Return<IDamageEvent>(new KnockbackEvent(_force, 1)));
    }
}