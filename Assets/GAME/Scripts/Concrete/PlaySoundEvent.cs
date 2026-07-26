using System;
using System.Collections.Generic;

public sealed class PlaySoundEvent : IReactiveEvent
{
    public void Consume(
        IReadOnlyList<TargetContext> contexts,
        Action<IReadOnlyList<TargetContext>> completed)
    {
        completed?.Invoke(contexts);
    }
}