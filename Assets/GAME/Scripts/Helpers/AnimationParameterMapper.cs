using System.Collections.Generic;
using UnityEngine;

public static class AnimationParameterMapper
{
    public static IEnumerable<AnimatorParamUpdate> MapMovement(MovementSnapshot movement)
    {
        yield return new AnimatorParamUpdate
        {
            ParamName = "MoveState",
            ParamType = AnimatorControllerParameterType.Int,
            Value = (int)movement.State
        };

        yield return new AnimatorParamUpdate
        {
            ParamName = "MoveSpeed",
            ParamType = AnimatorControllerParameterType.Float,
            Value = movement.Speed
        };
    }

    public static IEnumerable<AnimatorParamUpdate> MapCombat(CombatSnapshot combat)
    {
        // Always update the combat state (int parameter)
        yield return new AnimatorParamUpdate
        {
            ParamName = "CombatState",
            ParamType = AnimatorControllerParameterType.Int,
            Value = (int)combat.State
        };

        // Attack animation trigger (fire)
        if (combat.TriggerAttack)
            yield return AnimatorParamUpdate.Trigger("Attack");

        // Attack animation trigger (reset, e.g., cancel or to allow rapid retrigger)
        if (combat.ResetAttackTrigger)
            yield return AnimatorParamUpdate.Trigger("Attack", reset: true);

        // Parry trigger
        if (combat.TriggerParry)
            yield return AnimatorParamUpdate.Trigger("Parry");

        if (combat.ResetParryTrigger)
            yield return AnimatorParamUpdate.Trigger("Parry", reset: true);

        // Extend for other triggers/interrupts as needed (e.g., "Combo", "Special", etc.)
    }

}
