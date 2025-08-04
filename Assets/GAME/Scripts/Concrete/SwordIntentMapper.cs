using UnityEngine;

public class SwordIntentMapper : IIntentMapper
{
    public ActionIntent? MapInputToIntent(InputSnapshot inputSnapshot, CharacterSnapshot snapshot)
    {
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.PrimaryAttack, out var attackInput);
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Run, out var runInput);
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Jump, out var jumpInput);
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Parry, out var parryInput);
        // 1. Combo-aware Standard Attack (eventful)
        // (Let SwordCombat decide if this should start or continue a combo)
        if (attackInput.WasPresseedThisFrame && snapshot.Movement.State != MovementType.Run)
        {
            // Optionally: You could block combos in air if needed
            if (snapshot.Movement.State == MovementType.Jump || snapshot.Movement.State == MovementType.Fall)
                return null;

            // Optionally: Only allow if not attacking or in a combo window
            // if (!snapshot.Combat.IsAttacking && snapshot.Combat.ComboStep == 0)
            //    return ... // only new attack

            return new ActionIntent
            {
                // If running, movement type could be set to Run, but typically you go Idle when slashing
                Movement = new MovementAction { Direction = attackInput.Direction, ActionType = MovementType.Idle },
                Combat = new CombatAction { ActionType = CombatType.PrimaryAttack }
            };
        }

        // 2. Running Attack (eventful)
        // (Usually handled above, but you can special-case if running state matters)
        if (attackInput.WasPresseedThisFrame && runInput.IsHeld && snapshot.Movement.State == MovementType.Run)
        {
            return new ActionIntent
            {
                Movement = new MovementAction { Direction = runInput.Direction, ActionType = MovementType.Idle },
                Combat = new CombatAction { ActionType = CombatType.PrimaryAttack }
            };
        }

        // 3. Parry (eventful)
        if (parryInput.WasPresseedThisFrame)
        {
            return new ActionIntent
            {
                Combat = new CombatAction { ActionType = CombatType.Parry }
            };
        }

        // No actionable intent
        return null;
    }
}
