using UnityEngine;

public class MovementIntentMapper : IIntentMapper
{
    private readonly int _maxJumpStage = 2;

    public ActionIntent? MapInputToIntent(InputSnapshot inputSnapshot, CharacterSnapshot snapshot)
    {
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Move, out var moveInput);
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Jump, out var jumpInput);
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Dash, out var dashInput);
        Vector2 moveDirection = moveInput.Value is Vector2 direction
            ? direction
            : Vector2.zero;

        if (snapshot.Combat.IsAttacking && snapshot.Combat.IsCancelable && moveInput.IsHeld)
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = moveDirection,
                    ActionType = MovementType.Move
                },
                Combat = new CombatAction
                {
                    ActionType = CombatType.Idle
                },
            };
        }

        if (!(snapshot.Combat.IsAttacking || snapshot.Combat.IsAttacking && snapshot.Combat.IsCancelable) && dashInput.WasPresseedThisFrame)
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = moveDirection,
                    ActionType = MovementType.Dash
                }
            };
        }

        if (snapshot.Movement.State == MovementType.Fall)
        {
            return new ActionIntent
            {
                Movement = new MovementAction { ActionType = MovementType.Fall, Direction = moveDirection }
            };
        }

        if (snapshot.Combat.IsCancelable && jumpInput.WasPresseedThisFrame && snapshot.Movement.JumpRight < _maxJumpStage)
        {
            return new ActionIntent
            {
                Movement = new MovementAction { ActionType = MovementType.Jump }
            };
        }

        if (!snapshot.Combat.IsAttacking && jumpInput.WasPresseedThisFrame && snapshot.Movement.JumpRight < _maxJumpStage && snapshot.Movement.IsGrounded)
        {
            return new ActionIntent
            {
                Movement = new MovementAction { ActionType = MovementType.Jump, Direction = moveDirection }
            };
        }

        if (moveInput.IsHeld && snapshot.Movement.IsGrounded && snapshot.Movement.State != MovementType.Dash && !snapshot.Movement.State.Equals(MovementType.Jump) && snapshot.Combat.State == CombatType.Idle)
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = moveDirection,
                    ActionType = MovementType.Move
                }
            };
        }

        if (!moveInput.IsHeld && snapshot.Movement.IsGrounded && !snapshot.Movement.State.Equals(MovementType.Jump) && !snapshot.Movement.State.Equals(MovementType.Dash))
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = moveDirection,
                    ActionType = MovementType.Idle
                }
            };
        }

        if (snapshot.Movement.JumpRight > 0 && snapshot.Movement.State.Equals(MovementType.Jump) || snapshot.Movement.State.Equals(MovementType.Fall))
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = moveDirection,
                    ActionType = MovementType.DoubleJump
                }
            };
        }

        return new ActionIntent
        {
            Movement = new MovementAction
            {
                Direction = moveDirection
            }
        };

        return null;
    }
}