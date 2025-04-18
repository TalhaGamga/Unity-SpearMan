using UnityEngine;

public sealed class TargetContext
{
    public readonly IDamageable Damageable;
    public readonly IKnockbackable Knockbackable;

    public TargetContext(GameObject target)
    {
        Damageable = target.GetComponent<IDamageable>();
        Knockbackable = target.GetComponent<IKnockbackable>();
    }
}