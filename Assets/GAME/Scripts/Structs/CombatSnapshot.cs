public readonly struct CombatSnapshot
{
    public readonly CombatType CurrentState;         // E.g., Idle, InPrimaryAttack
    public readonly bool IsCancelable;        // For animation cancel
    public readonly int ComboStep;            // Which attack in the chain? (0=idle)
    public readonly bool TriggerAttack;       // Should Animator fire attack trigger this frame?
    public readonly bool ResetAttackTrigger;  // Animator resets trigger
    public readonly bool TriggerParry;        // Example: parry animation trigger
    public readonly bool ResetParryTrigger;   // Example: reset parry

    // You can extend with more triggers/flags as needed.

    public CombatSnapshot(
        CombatType state,
        bool isCancelable,
        int comboStep = 0,
        bool triggerAttack = false,
        bool resetAttackTrigger = false,
        bool triggerParry = false,
        bool resetParryTrigger = false
    )
    {
        CurrentState = state;
        IsCancelable = isCancelable;
        ComboStep = comboStep;
        TriggerAttack = triggerAttack;
        ResetAttackTrigger = resetAttackTrigger;
        TriggerParry = triggerParry;
        ResetParryTrigger = resetParryTrigger;
    }

    public static CombatSnapshot Default => new CombatSnapshot(
        CombatType.Idle, false, 0, false, true, false, false
    );

    public static CombatSnapshot Cancel => new CombatSnapshot(
        CombatType.Idle, true, 0, false, true, false, false
    );
}
