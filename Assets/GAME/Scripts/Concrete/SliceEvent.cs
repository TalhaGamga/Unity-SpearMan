using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SliceEvent : IReactiveEvent
{
    private readonly SliceData _data;

    public SliceEvent(SliceData data)
    {
        _data = data;
    }

    public void Consume(
        IReadOnlyList<TargetContext> contexts,
        Action<IReadOnlyList<TargetContext>> completed)
    {
        ConsumeStructural(
            contexts,
            (context, callback) =>
            {
                if (context.TryGet<ISliceable>(out var sliceable))
                    sliceable.Slice(_data, callback);
                else
                    callback(default);
            },
            completed
        );
    }

    private static void ConsumeStructural(
        IReadOnlyList<TargetContext> contexts,
        Action<TargetContext, Action<StructuralResult>> consume,
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

            consume(context, result =>
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