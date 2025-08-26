public struct MovementAnimationFrame
{
    public string Action;
    public string EventKey;

    public MovementAnimationFrame(
        string action,
        string eventKey
        )
    {
        Action = action;
        EventKey = eventKey;
    }
}