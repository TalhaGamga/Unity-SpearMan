public sealed class SliceEvent : IReactiveEvent
{
    private readonly SliceData _data;

    public SliceEvent(SliceData data)
    {
        _data = data;
    }

    public void Consume(TargetContext ctx)
    {
        ctx.Sliceable?.Slice(_data);
    }
}
