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

        // Extendible...
    }
}