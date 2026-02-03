using UnityEngine;
using R3;
public sealed class ReactiveDamageDispatcher : MonoBehaviour
{
    public void Apply(IDamageEventSource src, GameObject target)
    {
        var ctx = new TargetContext(target);
        src.Stream()
            .Subscribe(evt => evt.Consume(ctx));
    }
}