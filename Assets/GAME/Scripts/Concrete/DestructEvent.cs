using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DestructEvent : IReactiveEvent
{
    private readonly DestructData _data;

    public DestructEvent(DestructData data)
    {
        _data = data;
    }

    public void Consume(
        IReadOnlyList<TargetContext> contexts,
        Action<IReadOnlyList<TargetContext>> completed)
    {
        if (contexts.Count == 0)
        {
            completed?.Invoke(contexts);
            return;
        }

        var outputs = new List<TargetContext>();
        int remaining = contexts.Count;

        foreach (TargetContext context in contexts)
        {
            if (context == null || context.Target == null)
            {
                CompleteOne();
                continue;
            }

            if (!context.TryGet<IDestructible>(out var destructible))
            {
                outputs.Add(context);
                CompleteOne();
                continue;
            }

            destructible.Destruct(_data, result =>
            {
                bool addedResult = false;
                if (result.Succeeded && result.Targets != null)
                {
                    foreach (GameObject target in result.Targets)
                    {
                        if (target == null)
                            continue;

                        outputs.Add(new TargetContext(target, true));
                        addedResult = true;
                    }
                }

                if (!addedResult)
                    outputs.Add(context);

                CompleteOne();
            });
        }

        void CompleteOne()
        {
            remaining--;
            if (remaining == 0)
                completed?.Invoke(outputs);
        }
    }
}