public readonly struct MovementSnapshot
{
    public readonly MovementType State;
    public readonly float Speed;
    public readonly int JumpRight;
    public MovementSnapshot(MovementType state, float speed, int jumpStage)
    {
        State = state;
        Speed = speed;
        JumpRight = jumpStage;
    }
    public static MovementSnapshot Default => new MovementSnapshot(MovementType.Idle, 0,0);
}
