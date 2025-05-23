public class BreakEvent : IReactiveEvent
{
    public void Consume(TargetContext ctx)
    {
        ctx.Destructible?.Break();
    }
}