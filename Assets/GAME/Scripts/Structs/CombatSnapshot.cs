public readonly struct CombatSnapshot
{
    public readonly CombatType State;
    public readonly bool IsCancelable;
    public readonly int ComboStep;
    public readonly bool IsAttacking;
    public readonly int Version;

    public CombatSnapshot(
        CombatType state,
        int version,
        bool isCancelable,
        int comboStep = 0,
        bool isAttacking = false
    )
    {
        State = state;
        Version = version;
        IsCancelable = isCancelable;
        ComboStep = comboStep;
        IsAttacking = isAttacking;
    }

    public static CombatSnapshot Default => new CombatSnapshot(
        CombatType.Idle, 1, false, 0, false
    );
}
