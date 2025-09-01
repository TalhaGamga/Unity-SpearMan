public readonly struct CombatSnapshot
{
    public readonly CombatType State;        
    public readonly bool IsCancelable;        
    public readonly int ComboStep;            
    public readonly bool IsAttacking;

    public CombatSnapshot(
        CombatType state,
        bool isCancelable,
        int comboStep = 0,
        bool isAttacking = false
    )
    {
        State = state;
        IsCancelable = isCancelable;
        ComboStep = comboStep;
        IsAttacking = isAttacking;
    }

    public static CombatSnapshot Default => new CombatSnapshot(
        CombatType.Idle, false, 0, false
    );
}
