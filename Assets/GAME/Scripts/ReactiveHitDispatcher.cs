using UnityEngine;
using R3;
public sealed class ReactiveHitDispatcher : MonoBehaviour
{
    public void Apply(IDamageEventSource source, GameObject target)
    {
        if (source == null || target == null)
            return;

        var ctx = new TargetContext(target);

        source.Stream()
            .Subscribe(evt => evt.Consume(ctx));
    }
}