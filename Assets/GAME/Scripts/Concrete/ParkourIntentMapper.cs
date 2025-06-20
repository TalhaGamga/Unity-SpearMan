public class ParkourIntentMapper : IIntentMapper
{
    public ActionIntent? MapInputToIntent(InputType input, CharacterSnapshot snapshot)
    {
        if (snapshot.Movement.State == MovementType.Parkour)
        {
            if (input.Action == PlayerAction.Attack && input.IsHeld)
            {
                return new ActionIntent
                {
                    Movement = new MovementAction { ActionType = MovementType.Parkour },
                    Animator = new AnimatorAction { ActionType = AnimationType.ParkourAttack }
                };
            }
        }
        return null;
    }
}