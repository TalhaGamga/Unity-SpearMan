public readonly struct MovementSnapshot
{
    public readonly MovementType State;
    public readonly MovementComboType ComboType;
    public readonly float Speed;
    public readonly int JumpRight;
    public readonly bool IsGrounded;

    /// <summary>
    /// Whether the jump currently being spent left the air rather than the
    /// ground. Only meaningful while <see cref="State"/> is Jump.
    /// </summary>
    /// <remarks>
    /// Published rather than left to be re-derived from <see cref="JumpRight"/>
    /// because the count alone cannot answer it: how many jumps make a chain
    /// is a designer field, so a consumer comparing the count against two is
    /// really asserting that MaxJumpCount is two. The mover already computes
    /// this when it decides which launch velocity to use; this is the same
    /// answer, published instead of discarded.
    /// </remarks>
    public readonly bool IsAirJump;

    public MovementSnapshot(
        MovementType state,
        MovementComboType comboType,
        float speed,
        int jumpStage,
        bool isGrounded,
        bool isAirJump = false)
    {
        State = state;
        Speed = speed;
        JumpRight = jumpStage;
        IsGrounded = isGrounded;
        ComboType = comboType;
        IsAirJump = isAirJump;
    }

    public static MovementSnapshot Default => new MovementSnapshot(
        MovementType.Idle, MovementComboType.None, 0, 0, true);
}
