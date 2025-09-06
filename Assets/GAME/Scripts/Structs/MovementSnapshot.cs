public readonly struct MovementSnapshot
{
    public readonly MovementType State;
    public readonly MovementComboType ComboType;
    public readonly float Speed;
    public readonly int JumpRight;
    public readonly bool IsGrounded;
    
    public MovementSnapshot(MovementType state,MovementComboType comboType, float speed, int jumpStage, bool isGrounded)
    {
        State = state;
        Speed = speed;
        JumpRight = jumpStage;
        IsGrounded = isGrounded;
        ComboType = comboType;
    }

    public static MovementSnapshot Default => new MovementSnapshot(MovementType.Idle,MovementComboType.None, 0, 0, true);
}
