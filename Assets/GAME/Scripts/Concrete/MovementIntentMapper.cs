using UnityEngine;

public class MovementIntentMapper : IIntentMapper
{
    public ActionIntent? MapInputToIntent(InputSnapshot inputSnapshot, CharacterSnapshot snapshot)
    {
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Move, out var moveInput);
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Jump, out var jumpInput);
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Dash, out var dashInput);

        Vector2 moveDirection = moveInput.Value is Vector2 direction
            ? direction
            : Vector2.zero;

        JumpHoldState jumpHold = JumpInputReader.ReadHold(inputSnapshot);

        // Combat declares who owns horizontal motion while it runs.
        // Movement just forwards that declaration to the mover.
        LocomotionSource locomotion = snapshot.Combat.Locomotion;
        bool isAttacking = snapshot.Combat.IsAttacking;

        // Edge-triggered evasive actions outrank passive held movement. The old
        // order let a held direction swallow a dash pressed on the same frame.
        if (dashInput.WasPresseedThisFrame &&
            snapshot.Combat.CanCancelInto(CombatCancelOptions.Dash))
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = moveDirection,
                    ActionType = MovementType.Dash,
                    JumpHold = jumpHold,
                    Locomotion = LocomotionSource.Simulated
                },
                Combat = isAttacking
                    ? new CombatAction { ActionType = CombatType.Idle }
                    : (CombatAction?)null
            };
        }

        // Jump already has its own landing buffer in the mover. Here we only
        // decide whether the current attack permits the request to reach it.
        if (jumpInput.WasPresseedThisFrame &&
            snapshot.Combat.CanCancelInto(CombatCancelOptions.Jump))
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = moveDirection,
                    ActionType = MovementType.Jump,
                    JumpHold = JumpHoldState.Held,
                    Locomotion = LocomotionSource.Simulated
                },
                Combat = isAttacking
                    ? new CombatAction { ActionType = CombatType.Idle }
                    : (CombatAction?)null
            };
        }

        if (isAttacking &&
            snapshot.Combat.CanCancelInto(CombatCancelOptions.Move) &&
            moveInput.IsHeld)
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = moveDirection,
                    ActionType = MovementType.Move,
                    JumpHold = jumpHold,
                    // This branch cancels the attack, so the mover takes the
                    // ground back on the same frame rather than a frame later.
                    Locomotion = LocomotionSource.Simulated
                },
                Combat = new CombatAction { ActionType = CombatType.Idle }
            };
        }

        if (snapshot.Movement.State == MovementType.Fall)
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    ActionType = MovementType.Fall,
                    Direction = moveDirection,
                    JumpHold = jumpHold,
                    Locomotion = locomotion
                }
            };
        }

        if (moveInput.IsHeld && snapshot.Movement.IsGrounded && snapshot.Movement.State != MovementType.Dash && !snapshot.Movement.State.Equals(MovementType.Jump) && snapshot.Combat.State == CombatType.Idle)
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = moveDirection,
                    ActionType = MovementType.Move,
                    JumpHold = jumpHold,
                    Locomotion = locomotion
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
                    ActionType = MovementType.Idle,
                    JumpHold = jumpHold,
                    Locomotion = locomotion
                }
            };
        }

        // Airborne passthrough: no state change, but air control and the jump
        // hold flag still have to reach the mover every frame.
        return new ActionIntent
        {
            Movement = new MovementAction
            {
                Direction = moveDirection,
                ActionType = MovementType.None,
                JumpHold = jumpHold,
                Locomotion = locomotion
            }
        };
    }
}
