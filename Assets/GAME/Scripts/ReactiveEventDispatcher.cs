using UnityEngine;
using R3;
public sealed class ReactiveEventDispatcher : MonoBehaviour
{
    public void Apply(IReactiveEventSource source, GameObject target)
    {
        if (source == null || target == null)
            return;

        var ctx = new TargetContext(target);

        source.Stream()
            .Subscribe(evt => evt.Consume(ctx));
    }
}