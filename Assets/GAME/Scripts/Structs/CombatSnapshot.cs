public readonly struct CombatSnapshot
{
    public readonly CombatType State;
    public readonly int ComboStep;
    public readonly bool IsAttacking;

    /// <summary>
    /// The actions allowed to take control away from the current attack.
    /// Keeping this in the snapshot lets movement ask a semantic question
    /// without knowing which animation event opened the window.
    /// </summary>
    public readonly CombatCancelOptions CancelOptions;

    /// <summary>
    /// Compatibility summary for consumers that only care whether any exit
    /// exists. New gameplay code should use <see cref="CanCancelInto"/>.
    /// </summary>
    public bool IsCancelable => CancelOptions != CombatCancelOptions.None;
    /// <summary>
    /// Which attack the animator should be playing. Only meaningful while
    /// <see cref="IsAttacking"/> is true.
    /// </summary>
    public readonly AttackId Attack;

    /// <summary>
    /// Who owns horizontal motion while this combat state runs, taken from the
    /// active <see cref="AttackDefinition"/>. Published here so the movement
    /// intent can hand it to the mover without combat and movement having to
    /// know about each other.
    /// </summary>
    public readonly LocomotionSource Locomotion;

    public CombatSnapshot(
        CombatType state,
        AttackId attack,
        bool isCancelable,
        int comboStep = 0,
        bool isAttacking = false,
        LocomotionSource locomotion = LocomotionSource.Simulated
    ) : this(
        state,
        attack,
        isCancelable
            ? CombatCancelOptions.All
            : CombatCancelOptions.None,
        comboStep,
        isAttacking,
        locomotion)
    {
    }

    public CombatSnapshot(
        CombatType state,
        AttackId attack,
        CombatCancelOptions cancelOptions,
        int comboStep = 0,
        bool isAttacking = false,
        LocomotionSource locomotion = LocomotionSource.Simulated
    )
    {
        State = state;
        Attack = attack;
        CancelOptions = cancelOptions;
        ComboStep = comboStep;
        IsAttacking = isAttacking;
        Locomotion = locomotion;
    }

    public bool CanCancelInto(CombatCancelOptions option)
    {
        return !IsAttacking || (CancelOptions & option) != 0;
    }

    public static CombatSnapshot Default => new CombatSnapshot(
        CombatType.Idle, AttackId.None, false, 0, false
    );
}
