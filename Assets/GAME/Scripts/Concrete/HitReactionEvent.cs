using System;
using System.Collections.Generic;

public sealed class HitReactionEvent : IReactiveEvent
{
    private readonly HitReaction _reaction;

    public HitReactionEvent(HitReaction reaction)
    {
        _reaction = reaction;
    }

    public void Consume(
        IReadOnlyList<TargetContext> contexts,
        Action<IReadOnlyList<TargetContext>> completed)
    {
        foreach (TargetContext context in contexts)
        {
            if (context != null &&
                context.TryGet<IHitReactable>(out var hitReactable))
            {
                hitReactable.HandleReaction(_reaction);
            }
        }

        completed?.Invoke(contexts);
    }
}