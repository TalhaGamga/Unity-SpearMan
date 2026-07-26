using UnityEngine;

public class ReactionIntentMapper : IIntentMapper
{
    public ActionIntent? MapInputToIntent(
        InputSnapshot inputSnapshot,
        CharacterSnapshot snapshot)
    {
        ReactionSnapshot reaction = snapshot.Reaction;

        if (reaction.State == ReactionType.Dead)
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    ActionType = MovementType.Idle,
                    Direction = Vector2.zero
                },
                Combat = new CombatAction
                {
                    ActionType = CombatType.Idle
                }
            };
        }

        if (reaction.IsInHitStun &&
            reaction.LocksMovementInput &&
            reaction.LocksCombatInput)
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    ActionType = MovementType.Idle,
                    Direction = Vector2.zero
                },
                Combat = new CombatAction
                {
                    ActionType = CombatType.Idle
                }
            };
        }

        if (reaction.State == ReactionType.AirJuggle)
        {
            if (snapshot.Movement.IsGrounded)
            {
                return new ActionIntent
                {
                    Movement = new MovementAction
                    {
                        ActionType = MovementType.Idle,
                        Direction = Vector2.zero
                    },
                    Combat = new CombatAction
                    {
                        ActionType = CombatType.Idle
                    }
                };
            }
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    ActionType = reaction.AllowsAirDrift
                        ? MovementType.Fall
                        : MovementType.None,
                    Direction = reaction.AllowsAirDrift
                        ? TryReadMove(inputSnapshot)
                        : Vector2.zero
                },
                Combat = new CombatAction
                {
                    ActionType = CombatType.Idle
                }
            };
        }

        return null;
    }

    private static Vector2 TryReadMove(InputSnapshot inputSnapshot)
    {
        return inputSnapshot.CurrentInputs.TryGetValue(
            PlayerAction.Move,
            out var moveInput)
                ? (Vector2)moveInput.Value
                : Vector2.zero;
    }
}