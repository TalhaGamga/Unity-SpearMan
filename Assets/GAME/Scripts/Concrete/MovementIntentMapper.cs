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

        if (snapshot.Combat.IsAttacking && snapshot.Combat.IsCancelable && moveInput.IsHeld)
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
                    ActionType = MovementType.Dash,
                    JumpHold = jumpHold,
                    Locomotion = locomotion
                }
            };
        }

        // Jump is evaluated before the airborne branches so an air jump is not
        // swallowed by the Fall passthrough. Whether the jump is actually legal
        // - grounded, inside coyote time, or spending an air jump - is the
        // mover's call, not the mapper's. The mapper only states intent.
        bool jumpLockedByAttack =
            snapshot.Combat.IsAttacking && !snapshot.Combat.IsCancelable;

        if (jumpInput.WasPresseedThisFrame && !jumpLockedByAttack)
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    Direction = moveDirection,
                    ActionType = MovementType.Jump,
                    JumpHold = JumpHoldState.Held,
                    Locomotion = locomotion
                }
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
