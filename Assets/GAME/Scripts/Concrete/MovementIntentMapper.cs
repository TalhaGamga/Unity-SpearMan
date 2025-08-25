using UnityEngine;
public class MovementIntentMapper : IIntentMapper
{
    // Set this to match your RbMover's _maxJumpStage (or make it configurable)
    private readonly int _maxJumpStage = 2;

    public ActionIntent? MapInputToIntent(InputSnapshot inputSnapshot, CharacterSnapshot snapshot)
    {
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Move, out var moveInput);
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Jump, out var jumpInput);

        // 1. If attacking, and attack is cancelable, and RUN is held, allow run-cancel
        if (snapshot.IsAttacking && snapshot.Combat.IsCancelable && moveInput.IsHeld)
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = moveInput.Direction,
                    ActionType = MovementType.Move
                },
                Combat = new CombatAction
                {
                    ActionType = CombatType.Cancel
                },
            };
        }

        if (snapshot.IsAttacking && moveInput.IsHeld)
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = moveInput.Direction,
                    ActionType = MovementType.Idle
                }
            };
        }

        // 0. Handle Falling
        if (snapshot.Movement.State == MovementType.Fall)
        {
            return new ActionIntent
            {
                Movement = new MovementAction { ActionType = MovementType.Fall, Direction = moveInput.Direction }
            };
        }

        // 2. If attacking, and jump was just pressed, allow jump-cancel (eventful, and only if jump available)
        if (snapshot.Combat.IsCancelable && jumpInput.WasPresseedThisFrame && snapshot.Movement.JumpRight < _maxJumpStage)
        {
            return new ActionIntent
            {
                Movement = new MovementAction { ActionType = MovementType.Jump }
            };
        }

        // 3. Normal jump (eventful, only if jump available)
        if (!snapshot.IsAttacking && jumpInput.WasPresseedThisFrame && snapshot.Movement.JumpRight < _maxJumpStage)
        {
            return new ActionIntent
            {
                Movement = new MovementAction { ActionType = MovementType.Jump, Direction = moveInput.Direction }
            };
        }

        // 4. If run is held, move (stateful)
        if (moveInput.IsHeld && !snapshot.Movement.State.Equals(MovementType.Jump) && snapshot.Combat.CurrentState == CombatType.Idle)
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = moveInput.Direction,
                    ActionType = MovementType.Move
                }
            };
        }

        // 5. Idle fallback (no run/jump held)
        if (!moveInput.IsHeld && !snapshot.Movement.State.Equals(MovementType.Jump))
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = moveInput.Direction,
                    ActionType = MovementType.Idle
                }
            };
        }

        if ( snapshot.Movement.JumpRight > 0 && snapshot.Movement.State.Equals(MovementType.Jump) || snapshot.Movement.State.Equals(MovementType.Fall))
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = moveInput.Direction,
                    ActionType = MovementType.DoubleJump
                }
            };
        }

        return new ActionIntent
        {
            Movement = new MovementAction
            {
                Direction = moveInput.Direction
            }
        };

        return null;
    }
}
