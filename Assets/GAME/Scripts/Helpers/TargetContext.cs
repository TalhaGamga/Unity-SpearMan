using UnityEngine;

public sealed class TargetContext
{
    public readonly IDamageable Damageable;
    public readonly IKnockbackable Knockbackable;
    public readonly IDestructible Destructible;

    public TargetContext(GameObject target)
    {
        Damageable = target.GetComponent<IDamageable>();
        Knockbackable = target.GetComponent<IKnockbackable>();
        Destructible = target.GetComponent<IDestructible>();
    }
}