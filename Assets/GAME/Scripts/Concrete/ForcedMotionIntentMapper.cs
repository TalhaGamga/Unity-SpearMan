using UnityEngine;

public sealed class ForcedMotionIntentMapper : IIntentMapper
{
    public ActionIntent? MapInputToIntent(
        InputSnapshot inputSnapshot,
        CharacterSnapshot snapshot)
    {
        if (snapshot.Movement.State != MovementType.Launched &&
            snapshot.Movement.State != MovementType.ForcedFall)
        {
            return null;
        }

        return new ActionIntent
        {
            Movement = new MovementAction
            {
                ActionType = MovementType.None,
                Direction = Vector2.zero
            },
            Combat = new CombatAction
            {
                ActionType = CombatType.Idle
            }
        };
    }
}
