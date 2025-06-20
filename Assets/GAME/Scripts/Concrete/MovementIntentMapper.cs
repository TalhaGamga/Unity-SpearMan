using UnityEngine;

public class MovementIntentMapper : IIntentMapper
{
    public ActionIntent? MapInputToIntent(InputType input, CharacterSnapshot snapshot)
    {
        Debug.Log("Movement Intent Mapper");
        if (input.Action == PlayerAction.Run)
        {
            return new ActionIntent()
            {
                Movement = new MovementAction { ActionType = MovementType.Run },
                Animator = new AnimatorAction
                {
                    ActionType = AnimationType.Run,
                    UseRootMotion = true
                }
            };
        }
        return null;
    }
}