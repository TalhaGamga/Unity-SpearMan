public sealed class DestructEvent : IReactiveEvent
{
    private readonly DestructData _data;

    public DestructEvent(DestructData data)
    {
        _data = data;
    }

    public void Consume(TargetContext ctx)
    {
        ctx.Destructible?.Destruct(_data);
    }
}