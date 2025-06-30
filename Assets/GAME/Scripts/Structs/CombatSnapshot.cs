public readonly struct CombatSnapshot
{
    public readonly CombatType State;
    public readonly float Energy;
    public readonly bool TriggerAttack;
    public readonly bool ResetAttackTrigger;
    public readonly bool TriggerParry;
    public readonly bool ResetParryTrigger;
    public readonly bool IsCancelable;

    public CombatSnapshot(
        CombatType state,
        float energy,
        bool isCancelable,
        bool triggerAttack = false,
        bool triggerParry = false,
        bool resetAttackTrigger = false,
        bool resetParryTrigger = false
        )
    {
        State = state;
        Energy = energy;
        IsCancelable = isCancelable;
        TriggerAttack = triggerAttack;
        TriggerParry = triggerParry;
        ResetAttackTrigger = resetAttackTrigger;
        ResetParryTrigger = resetParryTrigger;
    }

    public static CombatSnapshot Default => new CombatSnapshot(CombatType.Idle, 0, false);
}
