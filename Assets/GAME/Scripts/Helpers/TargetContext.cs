using UnityEngine;

public sealed class TargetContext
{
    public readonly IDamageable Damageable;
    public readonly IImpactable Impactable;
    public readonly IDestructible Destructible;
    public readonly IHitReactable HitReactable;
    public TargetContext(GameObject target)
    {
        Damageable = target.GetComponent<IDamageable>();
        Impactable = target.GetComponent<IImpactable>();
        Destructible = target.GetComponent<IDestructible>();
        HitReactable = target.GetComponent<IHitReactable>();
    }
}