using R3;
using UnityEngine;

public sealed class BulletDamageEventSource : IReactiveEventSource
{
    private readonly float _damage;
    private readonly float _strength;
    private readonly string _fx;
    private readonly Vector3 _origin;
    private readonly Vector3 _direction;
    private readonly PhysicsAxes _translationAxes;
    private readonly PhysicsAxes _rotationAxes;
    private readonly float _angularImpulse;

    public BulletDamageEventSource(
        float damage,
        float strength,
        Vector3 origin,
        Vector3 direction,
        string fx,
        PhysicsAxes translationAxes = PhysicsAxes.XYZ,
        PhysicsAxes rotationAxes = PhysicsAxes.XYZ,
        float angularImpulse = 0f)
    {
        _damage = damage;
        _strength = strength;
        _translationAxes = PhysicsAxesUtility.Sanitize(translationAxes);
        _rotationAxes = PhysicsAxesUtility.Sanitize(rotationAxes);
        _direction = PhysicsAxesUtility.Direction(
            direction,
            _translationAxes | _rotationAxes
        );
        _angularImpulse = Mathf.Max(0f, angularImpulse);
        _origin = origin;
        _fx = fx;
    }

    public Observable<IReactiveEvent> Stream()
    {
        var impact = new ImpactData(
            _direction,
            _strength,
            _origin,
            _translationAxes,
            _rotationAxes,
            _angularImpulse
        );
        var destruct = new DestructData(_direction, _origin);

        return Observable.Return<IReactiveEvent>(new DamageEvent(_damage))
            .Concat(Observable.Return<IReactiveEvent>(new ImpactEvent(impact)))
            .Concat(Observable.Return<IReactiveEvent>(new DestructEvent(destruct)))
            .Concat(Observable.Return<IReactiveEvent>(new PieceImpactEvent(impact)));
    }
}