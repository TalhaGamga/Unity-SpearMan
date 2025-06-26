public readonly struct CombatSnapshot
{
    public readonly CombatType State;
    public readonly float Energy;
    public CombatSnapshot(CombatType state, float energy) { State = state; Energy = energy; }
    public static CombatSnapshot Default => new CombatSnapshot(CombatType.Idle, 0);
}