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
                Direction = Vector2.zero,
                // Forced motion ignores input, but the jump button's release
                // still has to land or the mover keeps the press latched and
                // eats the player's next jump after recovery.
                JumpHold = JumpInputReader.ReadHold(inputSnapshot)
            },
            Combat = new CombatAction
            {
                ActionType = CombatType.Idle
            }
        };
    }
}
