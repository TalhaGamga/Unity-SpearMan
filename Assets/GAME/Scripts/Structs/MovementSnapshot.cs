public readonly struct MovementSnapshot
{
    public readonly MovementType State; public readonly float Speed;
    public MovementSnapshot(MovementType state, float speed) { State = state; Speed = speed; }
    public static MovementSnapshot Default => new MovementSnapshot(MovementType.Idle, 0);
}
