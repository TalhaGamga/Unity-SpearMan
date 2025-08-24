using System.Collections.Generic;

public static class AnimationParameterMapper
{
    public static IEnumerable<AnimatorParamUpdate> AnimatorMapper(CharacterSnapshot snapshot)
    {
        yield return new AnimatorParamUpdate
        {
            ParamName = "MoveState",
            ParamType = AnimatorParamUpdateType.Int,
            Value = (int)snapshot.Movement.State,
        };

        yield return new AnimatorParamUpdate
        {
            ParamName = "MoveSpeed",
            ParamType = AnimatorParamUpdateType.Float,
            Value = snapshot.Movement.Speed
        };

        // Jump Triggers
        if (snapshot.Movement.JumpStage == 1 && snapshot.Movement.State == MovementType.Jump)
            yield return AnimatorParamUpdate.Trigger("Jump");

        else if (snapshot.Movement.JumpStage == 2 && snapshot.Movement.State == MovementType.Jump)
            yield return AnimatorParamUpdate.Trigger("DoubleJump");

        // Land Trigger
        if (snapshot.Movement.State == MovementType.Land)
        {
            yield return AnimatorParamUpdate.Trigger("Land");

            // Optionally reset previous jump triggers
            yield return AnimatorParamUpdate.Trigger("Jump", reset: true);
            yield return AnimatorParamUpdate.Trigger("DoubleJump", reset: true);
        }

        // Dash Trigger
        if (snapshot.Movement.State == MovementType.Dash)
            yield return AnimatorParamUpdate.Trigger("Dash");

        // Root Motion Toggle
        yield return AnimatorParamUpdate.RootMotion(snapshot.Movement.State == MovementType.Move || snapshot.IsAttacking);

        // Combat State Triggers
        switch (snapshot.Combat.CurrentState)
        {
            case CombatType.PrimaryAttack:
                yield return AnimatorParamUpdate.Trigger("Attack");
                yield return new AnimatorParamUpdate
                {
                    ParamType = AnimatorParamUpdateType.Int,
                    ParamName = "ComboStep",
                    Value = snapshot.Combat.ComboStep
                };
                break;

            case CombatType.Parry:
                yield return AnimatorParamUpdate.Trigger("Parry");
                break;

            case CombatType.Idle:
                yield return AnimatorParamUpdate.Trigger("AttackCancel");
                break;
        }

        // Manual Trigger Resets (handled externally via .Trigger(..., reset: true))
        if (snapshot.Combat.ResetAttackTrigger)
            yield return AnimatorParamUpdate.Trigger("Attack", reset: true);
    }
}
