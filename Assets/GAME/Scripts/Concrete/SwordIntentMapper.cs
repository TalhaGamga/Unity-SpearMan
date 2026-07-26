using UnityEngine;

public class SwordIntentMapper : IIntentMapper
{
    public ActionIntent? MapInputToIntent(
        InputSnapshot inputSnapshot,
        CharacterSnapshot snapshot)
    {
        inputSnapshot.CurrentInputs.TryGetValue(
            PlayerAction.PrimaryAttack,
            out var attackInput);
        inputSnapshot.CurrentInputs.TryGetValue(
            PlayerAction.Move,
            out var moveInput);

        if (!attackInput.WasPresseedThisFrame)
            return null;

        Vector2 moveDirection = moveInput.Value is Vector2 direction
            ? direction
            : Vector2.zero;

        bool isDashingJump =
            snapshot.Movement.State == MovementType.Jump &&
            snapshot.Movement.ComboType == MovementComboType.DashingJump;

        if (isDashingJump)
        {
            return CreateStabIntent(
                moveDirection,
                MovementType.Stab,
                version: 2);
        }

        if (!snapshot.Movement.IsGrounded ||
            snapshot.Movement.State == MovementType.Fall ||
            snapshot.Movement.State == MovementType.Jump)
        {
            return null;
        }

        if (snapshot.Movement.State == MovementType.Dash)
        {
            return CreateStabIntent(
                moveDirection,
                MovementType.None,
                version: 1);
        }

        return new ActionIntent
        {
            Movement = new MovementAction
            {
                Direction = moveDirection,
                ActionType = MovementType.Idle
            },
            Combat = new CombatAction
            {
                ActionType = CombatType.GroundedPrimaryAttack
            }
        };
    }

    private static ActionIntent CreateStabIntent(
        Vector2 moveDirection,
        MovementType movementType,
        int version)
    {
        return new ActionIntent
        {
            Movement = new MovementAction
            {
                Direction = moveDirection,
                ActionType = movementType
            },
            Combat = new CombatAction
            {
                ActionType = CombatType.Stab,
                Version = version
            }
        };
    }
}