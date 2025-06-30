using System.Collections.Generic;
using UnityEngine;

public static class AnimationParameterMapper
{
    public static IEnumerable<AnimatorParamUpdate> MapMovement(InputSnapshot input, CharacterSnapshot snapshot)
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

        // Example: Jump trigger (fire only on new jump input)
        if (input.CurrentInputs.TryGetValue(PlayerAction.Jump, out var jumpInput) && jumpInput.WasPresseedThisFrame)
        {
            yield return AnimatorParamUpdate.Trigger("Jump");
        }

        // Example: Dash trigger (add your logic as needed)
        if (input.CurrentInputs.TryGetValue(PlayerAction.Dash, out var dashInput) && dashInput.WasPresseedThisFrame)
        {
            yield return AnimatorParamUpdate.Trigger("Dash");
        }

        if (snapshot.Movement.State == MovementType.Run)
            yield return AnimatorParamUpdate.RootMotion(true);
        else
            yield return AnimatorParamUpdate.RootMotion(false);

        // Example: Land trigger (if you want to fire a land animation)
        // if (snapshot.Movement.StateChangedTo(MovementType.Land)) ...
        //     yield return AnimatorParamUpdate.Trigger("Land");
    }


    public static IEnumerable<AnimatorParamUpdate> MapCombat(InputSnapshot input, CharacterSnapshot snapshot)
    {
        // Primary attack
        if (input.CurrentInputs.TryGetValue(PlayerAction.PrimaryAttack, out var attackInput) && attackInput.WasPresseedThisFrame)
        {
            yield return AnimatorParamUpdate.Trigger("Attack");
        }

        // Parry (if you have it)
        if (input.CurrentInputs.TryGetValue(PlayerAction.Parry, out var parryInput) && parryInput.WasPresseedThisFrame)
        {
            yield return AnimatorParamUpdate.Trigger("Parry");
        }

        // Attack cancel (e.g., if running while in cancel window)
        if (input.CurrentInputs.TryGetValue(PlayerAction.Run, out var runInput)
            && runInput.IsHeld
            && snapshot.IsAttacking
            && snapshot.Combat.IsCancelable)
        {
            yield return AnimatorParamUpdate.Trigger("AttackCancel");
        }

        // Add similar logic for special, combo, etc.

        // (If you want to handle trigger resets, add conditions here based on input/context)
    }


}
