public class SwordIntentMapper : IIntentMapper
{
    public ActionIntent? MapInputToIntent(InputSnapshot inputSnapshot, CharacterSnapshot characterSnapshot)
    {
        // Grab current inputs (null if not present)
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.PrimaryAttack, out var attackInput);
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Run, out var runInput);
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Jump, out var jumpInput);
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Parry, out var parryInput);

        // Example: Running Attack
        if (attackInput.IsHeld && runInput.IsHeld && characterSnapshot.Movement.State == MovementType.Run)
        {
            return new ActionIntent
            {
                Movement = new MovementAction { Direction = runInput.Direction, ActionType = MovementType.Run },
                Combat = new CombatAction { ActionType = CombatType.PrimaryAttack },
                Animator = new AnimatorAction { ActionType = AnimationType.RunningSlash }
            };
        }

        // Example: Jumping Attack
        if (attackInput.IsHeld && jumpInput.IsHeld && characterSnapshot.Movement.State == MovementType.Jump)
        {
            return new ActionIntent
            {
                Movement = new MovementAction { ActionType = MovementType.Jump },
                Combat = new CombatAction { ActionType = CombatType.PrimaryAttack },
                Animator = new AnimatorAction { ActionType = AnimationType.JumpSlash }
            };
        }

        // Standard Attack
        if (attackInput.IsHeld)
        {
            return new ActionIntent
            {
                Movement = new MovementAction { Direction = attackInput.Direction, ActionType = MovementType.Idle },
                Combat = new CombatAction { ActionType = CombatType.PrimaryAttack },
                Animator = new AnimatorAction { ActionType = AnimationType.Slash }
            };
        }

        // Parry (example, could be eventful)
        if (parryInput.IsHeld)
        {
            return new ActionIntent
            {
                Combat = new CombatAction { ActionType = CombatType.Parry },
                Animator = new AnimatorAction { ActionType = AnimationType.Parry }
            };
        }

        // No actionable intent
        return null;
    }
}
