using UnityEngine;

public sealed class TargetContext
{
    public readonly IDamageable Damageable;
    public readonly IImpactable Impactable;
    public readonly IDestructible Destructible;
    public readonly ISliceable Sliceable;
    public readonly IHitReactable HitReactable;

    public TargetContext(GameObject target)
    {
        Damageable = target.GetComponent<IDamageable>();
        Impactable = target.GetComponent<IImpactable>();
        Destructible = target.GetComponent<IDestructible>();
        Sliceable = target.GetComponent<ISliceable>();
        HitReactable = target.GetComponent<IHitReactable>();
    }
}