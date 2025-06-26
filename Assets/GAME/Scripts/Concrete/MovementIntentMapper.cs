public class MovementIntentMapper : IIntentMapper
{
    public ActionIntent? MapInputToIntent(InputType input, CharacterSnapshot snapshot)
    {
        if (input.Action == PlayerAction.Run)
        {
            return new ActionIntent()
            {
                Movement = new MovementAction
                {
                    Direction = input.Direction,
                    ActionType = MovementType.Run
                },
                Animator = new AnimatorAction
                {
                    ActionType = AnimationType.Run,
                    UseRootMotion = true
                }
            };
        }

        if (input.Action == PlayerAction.Jump && input.IsHeld)
        {
            return new ActionIntent()
            {
                Movement = new MovementAction { ActionType = MovementType.Jump },
                Animator = new AnimatorAction
                {
                    ActionType = AnimationType.Jump,
                    UseRootMotion = false
                }
            };
        }
        return null;
    }
}