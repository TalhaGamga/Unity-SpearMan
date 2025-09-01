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
            ParamName = snapshot.Combat.State.ToString(),
            ParamType = AnimatorParamUpdateType.Bool,
            Value = snapshot.Combat.IsAttacking
        };

        yield return new AnimatorParamUpdate
        {
            ParamName = "ComboStep",
            ParamType = AnimatorParamUpdateType.Int,
            Value = snapshot.Combat.ComboStep
        };
    }
}