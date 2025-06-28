using UnityEngine;

public class MovementIntentMapper : IIntentMapper
{
    public ActionIntent? MapInputToIntent(InputSnapshot inputSnapshot, CharacterSnapshot snapshot)
    {
        // Access relevant input states
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Run, out var runInput);
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Jump, out var jumpInput);

        // 1. If attacking, and attack is cancelable, and RUN is held, allow run-cancel
        if (snapshot.IsAttacking && snapshot.Combat.IsCancelable && runInput.IsHeld)
        {
            return new ActionIntent()
            {
                Movement = new MovementAction
                {
                    Direction = runInput.Direction,
                    ActionType = MovementType.Run
                },
                Animator = new AnimatorAction
                {
                    ActionType = AnimationType.Run,
                    UseRootMotion = true
                },
                Combat = new CombatAction
                {
                    ActionType = CombatType.Cancel
                }
            };
        }

        // 2. If attacking, and jump is held, allow jump-cancel
        if (snapshot.IsAttacking && jumpInput.IsHeld)
        {
            return new ActionIntent()
            {
                Movement = new MovementAction { ActionType = MovementType.Jump },
                Animator = new AnimatorAction
                {
                    ActionType = AnimationType.Jump,
                    UseRootMotion = false
                }
            };
        }

        // 3. If run is held, move
        if (runInput.IsHeld)
        {
            return new ActionIntent()
            {
                Movement = new MovementAction
                {
                    Direction = runInput.Direction,
                    ActionType = MovementType.Run
                },
                Animator = new AnimatorAction
                {
                    ActionType = AnimationType.Run,
                    UseRootMotion = true
                }
            };
        }

        // 4. If jump is held, jump
        if (jumpInput.IsHeld)
        {
            return new ActionIntent()
            {
                Movement = new MovementAction { ActionType = MovementType.Jump },
                Animator = new AnimatorAction
                {
                    ActionType = AnimationType.Jump,
                    UseRootMotion = false
                }
            };
        }

        // 5. If no run/jump input held, go idle
        if ((runInput.IsHeld == false) && (jumpInput.IsHeld == false))
        {
            return new ActionIntent()
            {
                Movement = new MovementAction
                {
                    Direction = Vector2.zero,
                    ActionType = MovementType.Run
                },
                Animator = new AnimatorAction
                {
                    ActionType = AnimationType.Run,
                    UseRootMotion = true // or false as needed
                }
            };
        }


        return null;
    }
}