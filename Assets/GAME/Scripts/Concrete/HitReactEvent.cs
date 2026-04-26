public sealed class HitReactEvent : IReactiveEvent
{
    private readonly HitReaction _data;

    public HitReactEvent(HitReaction data)
    {
        _data = data;
    }

    public void Consume(TargetContext ctx)
    {
        ctx.HitReactable?.HandleReaction(_data);
    }
}