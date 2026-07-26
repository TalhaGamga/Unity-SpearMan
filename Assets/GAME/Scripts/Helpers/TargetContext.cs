using UnityEngine;

public sealed class TargetContext
{
    public GameObject Target { get; }
    public bool IsStructuralResult { get; }

    public TargetContext(
        GameObject target,
        bool isStructuralResult = false)
    {
        Target = target;
        IsStructuralResult = isStructuralResult;
    }

    public bool TryGet<TCapability>(out TCapability capability)
        where TCapability : class
    {
        capability = Target != null
            ? Target.GetComponent<TCapability>()
            : null;

        return capability != null;
    }
}