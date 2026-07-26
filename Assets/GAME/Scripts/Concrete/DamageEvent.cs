using System;
using System.Collections.Generic;

public sealed class DamageEvent : IReactiveEvent
{
    private readonly float _damage;

    public DamageEvent(float damage)
    {
        _damage = damage;
    }

    public void Consume(
        IReadOnlyList<TargetContext> contexts,
        Action<IReadOnlyList<TargetContext>> completed)
    {
        foreach (TargetContext context in contexts)
        {
            if (context != null &&
                context.TryGet<IDamageable>(out var damageable))
            {
                damageable.ReceiveDamage(_damage);
            }
        }

        completed?.Invoke(contexts);
    }
}