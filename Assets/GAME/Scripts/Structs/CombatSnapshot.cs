public readonly struct CombatSnapshot
{
    public readonly CombatType State;
    public readonly bool IsCancelable;
    public readonly int ComboStep;
    public readonly bool IsAttacking;
    public readonly int Version;

    /// <summary>
    /// Who owns horizontal motion while this combat state runs, taken from the
    /// active <see cref="AttackDefinition"/>. Published here so the movement
    /// intent can hand it to the mover without combat and movement having to
    /// know about each other.
    /// </summary>
    public readonly LocomotionSource Locomotion;

    public CombatSnapshot(
        CombatType state,
        int version,
        bool isCancelable,
        int comboStep = 0,
        bool isAttacking = false,
        LocomotionSource locomotion = LocomotionSource.Simulated
    )
    {
        State = state;
        Version = version;
        IsCancelable = isCancelable;
        ComboStep = comboStep;
        IsAttacking = isAttacking;
        Locomotion = locomotion;
    }

    public static CombatSnapshot Default => new CombatSnapshot(
        CombatType.Idle, 1, false, 0, false
    );
}
