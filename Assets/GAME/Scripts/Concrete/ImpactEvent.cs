public sealed class ImpactEvent : IReactiveEvent
{
    private readonly ImpactData _data;

    public ImpactEvent(ImpactData data)
    {
        _data = data;
    }

    public void Consume(TargetContext ctx)
    {
        ctx.Impactable?.ApplyImpact(_data);
    }
}