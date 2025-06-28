public readonly struct CombatSnapshot
{
    public readonly CombatType State;
    public readonly float Energy;
    public readonly bool IsCancelable;

    public CombatSnapshot(CombatType state, float energy, bool isCancelable)
    {
        State = state;
        Energy = energy;
        IsCancelable = isCancelable;
    }

    public static CombatSnapshot Default => new CombatSnapshot(CombatType.Idle, 0, false);
}