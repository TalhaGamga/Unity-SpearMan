using System;
using System.Collections.Generic;

public sealed class ImpactEvent : IReactiveEvent
{
    private readonly ImpactData _data;

    public ImpactEvent(ImpactData data)
    {
        _data = data;
    }

    public void Consume(
        IReadOnlyList<TargetContext> contexts,
        Action<IReadOnlyList<TargetContext>> completed)
    {
        foreach (TargetContext context in contexts)
        {
            if (context != null &&
                context.TryGet<IImpactable>(out var impactable))
            {
                impactable.ApplyImpact(_data);
            }
        }

        completed?.Invoke(contexts);
    }
}