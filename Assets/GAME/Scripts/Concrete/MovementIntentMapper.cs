using UnityEngine;

public class MovementIntentMapper : IIntentMapper
{
    // Set this to match your RbMover's _maxJumpStage (or make it configurable)
    private readonly int _maxJumpStage = 2;

    public ActionIntent? MapInputToIntent(InputSnapshot inputSnapshot, CharacterSnapshot snapshot)
    {
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Run, out var runInput);
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Jump, out var jumpInput);

        // 1. If attacking, and attack is cancelable, and RUN is held, allow run-cancel
        if (snapshot.IsAttacking && snapshot.Combat.IsCancelable && runInput.IsHeld)
        {
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
            };
        }

        if (snapshot.IsAttacking && runInput.IsHeld)
        {
            Debug.Log("Canceled");
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = runInput.Direction,
                    ActionType = MovementType.Idle
                }
            };
        }

        // 0. Handle Falling
        if (snapshot.Movement.State == MovementType.Fall)
        {
            return new ActionIntent
            {
                Movement = new MovementAction { ActionType = MovementType.Fall, Direction = runInput.Direction }
            };
        }

        // 2. If attacking, and jump was just pressed, allow jump-cancel (eventful, and only if jump available)
        if (snapshot.IsAttacking && jumpInput.WasPresseedThisFrame && snapshot.Movement.JumpStage < _maxJumpStage)
        {
            return new ActionIntent
            {
                Movement = new MovementAction { ActionType = MovementType.Jump }
            };
        }

        // 3. Normal jump (eventful, only if jump available)
        if (jumpInput.WasPresseedThisFrame && snapshot.Movement.JumpStage < _maxJumpStage)
        {
            return new ActionIntent
            {
                Movement = new MovementAction { ActionType = MovementType.Jump }
            };
        }

        // 4. If run is held, move (stateful)
        if (runInput.IsHeld && snapshot.Combat.CurrentState == CombatType.Idle)
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = runInput.Direction,
                    ActionType = MovementType.Run
                }
            };
        }

        // 5. Idle fallback (no run/jump held)
        if (!runInput.IsHeld && !jumpInput.IsHeld)
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = runInput.Direction,
                    ActionType = MovementType.Idle
                }
            };
        }

        return null;
    }
}
