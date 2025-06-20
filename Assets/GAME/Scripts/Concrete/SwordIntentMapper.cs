public class SwordIntentMapper : IIntentMapper
{
    public ActionIntent? MapInputToIntent(InputType input, CharacterSnapshot snapshot)
    {
        if (input.Action == PlayerAction.Attack)
        {
            if (snapshot.Movement.State == MovementType.Run)
                return new ActionIntent
                {
                    Movement = new MovementAction { ActionType = MovementType.Run },
                    Combat = new CombatAction { ActionType = CombatType.Slash },
                    Animator = new AnimatorAction { ActionType = AnimationType.RunningSlash }
                };

            if (snapshot.Movement.State == MovementType.Jump)
                return new ActionIntent
                {
                    Movement = new MovementAction { ActionType = MovementType.Jump },
                    Combat = new CombatAction { ActionType = CombatType.JumpSlash },
                    Animator = new AnimatorAction { ActionType = AnimationType.JumpSlash }
                };

            return new ActionIntent
            {
                Movement = new MovementAction { ActionType = MovementType.Idle },
                Combat = new CombatAction { ActionType = CombatType.Slash },
                Animator = new AnimatorAction { ActionType = AnimationType.Slash }
            };
        }

        if (input.Action == PlayerAction.Parry)
        {
            return new ActionIntent
            {
                Combat = new CombatAction { ActionType = CombatType.Parry },
                Animator = new AnimatorAction { ActionType = AnimationType.Parry }
            };
        }

        return null;
    }
} 