using System;
using System.Collections.Generic;

public sealed class PierceEvent : IReactiveEvent
{
    private readonly PierceData _data;

    public PierceEvent(PierceData data)
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
                context.TryGet<IPierceable>(out var pierceable))
            {
                pierceable.ApplyPierce(_data);
            }
        }

        completed?.Invoke(contexts);
    }
}
