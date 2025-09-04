public readonly struct MovementSnapshot
{
    public readonly MovementType State;
    public readonly float Speed;
    public readonly int JumpRight;
    public readonly bool IsGrounded;
    
    public MovementSnapshot(MovementType state, float speed, int jumpStage, bool isGrounded)
    {
        State = state;
        Speed = speed;
        JumpRight = jumpStage;
        IsGrounded = isGrounded;
    }

    public static MovementSnapshot Default => new MovementSnapshot(MovementType.Idle, 0, 0, true);
}
