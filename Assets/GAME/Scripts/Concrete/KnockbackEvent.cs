using UnityEngine;

public sealed class KnockbackEvent : IReactiveEvent
{
    private readonly Vector3 _direction;
    private readonly float _strength;
    public KnockbackEvent(Vector3 direction, float strength)
    {
        _direction = direction;
        _strength = strength;
    }
    public void Consume(TargetContext ctx)
    {
        Debug.Log("Consumed");
        ctx.Knockbackable?.ApplyForce(_direction * _strength);
    }
}