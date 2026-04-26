using UnityEngine;

public sealed class TargetContext
{
    public readonly IDamageable Damageable;
    public readonly IImpactable Knockbackable;
    public readonly IDestructible Destructible;
    public readonly IHitReactable HitReactable;
    public TargetContext(GameObject target)
    {
        Damageable = target.GetComponent<IDamageable>();
        Knockbackable = target.GetComponent<IImpactable>();
        Destructible = target.GetComponent<IDestructible>();
        HitReactable = target.GetComponent<IHitReactable>();
    }
}