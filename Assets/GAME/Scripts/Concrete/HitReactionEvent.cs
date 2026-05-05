public sealed class HitReactionEvent : IReactiveEvent
{
    private readonly HitReaction _reaction;

    public HitReactionEvent(HitReaction reaction)
    {
        _reaction = reaction;
    }

    public void Consume(TargetContext ctx)
    {
        ctx.HitReactable?.HandleReaction(_reaction);
    }
}