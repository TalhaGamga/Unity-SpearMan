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

        yield return new AnimatorParamUpdate
        {
            ParamName = CombatType.GroundedPrimaryAttack.ToString(),
            ParamType = AnimatorParamUpdateType.Bool,
            Value = snapshot.Combat.IsAttacking &&
                snapshot.Combat.State == CombatType.GroundedPrimaryAttack
        };

        yield return new AnimatorParamUpdate
        {
            ParamName = CombatType.Stab.ToString(),
            ParamType = AnimatorParamUpdateType.Bool,
            Value = snapshot.Combat.IsAttacking &&
                snapshot.Combat.State == CombatType.Stab
        };

        yield return new AnimatorParamUpdate
        {
            ParamName = "ComboStep",
            ParamType = AnimatorParamUpdateType.Int,
            Value = snapshot.Combat.ComboStep
        };

        yield return new AnimatorParamUpdate
        {
            ParamName = "Version",
            ParamType = AnimatorParamUpdateType.Int,
            Value = snapshot.Combat.Version
        };

        yield return new AnimatorParamUpdate
        {
            ParamName = snapshot.Reaction.State.ToString(),
            ParamType = AnimatorParamUpdateType.Trigger,
            Value = snapshot.Reaction.Force
        };
    }
}