using System.Linq;
using UnityEngine;

public class MovementIntentMapper : IIntentMapper
{
    public ActionIntent? MapInputToIntent(InputSnapshot inputSnapshot, CharacterSnapshot snapshot)
    {
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Run, out var runInput);
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Jump, out var jumpInput);

        // Animator param updates—always included, even for idle
        var animatorUpdates = AnimationParameterMapper.MapMovement(snapshot.Movement);

        // 1. If attacking, and attack is cancelable, and RUN is held, allow run-cancel
        if (snapshot.IsAttacking && snapshot.Combat.IsCancelable && runInput.IsHeld)
        {
            var fullAnimatorUpdates = animatorUpdates.Concat(new[] { AnimatorParamUpdate.Trigger("AttackCancel") });

            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = runInput.Direction,
                    ActionType = MovementType.Run
                },
                Combat = new CombatAction
                {
                    ActionType = CombatType.Cancel
                },
                AnimatorUpdates = fullAnimatorUpdates
            };
        }

        // 2. If attacking, and jump was just pressed, allow jump-cancel (eventful)
        if (snapshot.IsAttacking && jumpInput.WasPresseedThisFrame)
        {
            return new ActionIntent
            { 
                Movement = new MovementAction { ActionType = MovementType.Jump },
                AnimatorUpdates = animatorUpdates
            };
        }

        // 3. Normal jump (eventful)
        if (jumpInput.WasPresseedThisFrame)
        {
            return new ActionIntent
            {
                Movement = new MovementAction { ActionType = MovementType.Jump },
                AnimatorUpdates = animatorUpdates
            };
        }

        // 4. If run is held, move (stateful)
        if (runInput.IsHeld)
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = runInput.Direction,
                    ActionType = MovementType.Run
                },
                AnimatorUpdates = animatorUpdates
            };
        }

        // 5. Idle fallback (no run/jump held)
        if (!runInput.IsHeld && !jumpInput.IsHeld)
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = Vector2.zero,
                    ActionType = MovementType.Run
                },
                AnimatorUpdates = animatorUpdates
            };
        }

        // No actionable intent
        return null;
    }
}
