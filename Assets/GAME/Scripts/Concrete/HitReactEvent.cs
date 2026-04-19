public sealed class HitReactEvent : IReactiveEvent
{
    private readonly HitReactionData _data;

    public HitReactEvent(HitReactionData data)
    {
        _data = data;
    }

    public void Consume(TargetContext ctx)
    {
        ctx.HitReactable?.React(_data);
    }
}