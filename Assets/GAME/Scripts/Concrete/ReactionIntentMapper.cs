using UnityEngine;

public class ReactionIntentMapper : IIntentMapper
{
    public ActionIntent? MapInputToIntent(InputSnapshot inputSnapshot, CharacterSnapshot snapshot)
    {
        var reaction = snapshot.Reaction;

        if (reaction.State == ReactionType.Dead)
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    ActionType = MovementType.Idle,
                    Direction = Vector2.zero,
                    ReactionState = reaction.State
                },
                Combat = new CombatAction
                {
                    ActionType = CombatType.Idle
                }
            };
        }

        if (reaction.IsInHitStun && reaction.LocksMovementInput && reaction.LocksCombatInput)
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    ActionType = MovementType.Idle,
                    Direction = Vector2.zero,
                    ReactionState = reaction.State
                },
                Combat = new CombatAction
                {
                    ActionType = CombatType.Idle
                }
            };
        }

        if (reaction.State == ReactionType.Launch || reaction.State == ReactionType.AirJuggle)
        {
            return new ActionIntent
            {
                Movement = new MovementAction
                {
                    ActionType = MovementType.Fall, // this should change   
                    Direction = reaction.AllowsAirDrift
                        ? TryReadMove(inputSnapshot)
                        : reaction.Direction,
                    ReactionState = reaction.State
                },
                Combat = new CombatAction
                {
                    ActionType = CombatType.Idle
                }
            };
        }

        return null;
    }

    private Vector2 TryReadMove(InputSnapshot inputSnapshot)
    {
        return inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Move, out var moveInput)
            ? (Vector2)moveInput.Value
            : Vector2.zero;
    }
}