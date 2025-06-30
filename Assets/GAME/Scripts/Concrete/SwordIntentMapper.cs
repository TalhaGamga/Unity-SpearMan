using UnityEngine;

public class SwordIntentMapper : IIntentMapper
{
    public ActionIntent? MapInputToIntent(InputSnapshot inputSnapshot, CharacterSnapshot snapshot)
    {
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.PrimaryAttack, out var attackInput);
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Run, out var runInput);
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Jump, out var jumpInput);
        inputSnapshot.CurrentInputs.TryGetValue(PlayerAction.Parry, out var parryInput); 

        // Always map movement for animator parameters 
        var animatorUpdates = AnimationParameterMapper.MapCombat(snapshot.Combat);

        // 1. Running Attack (stateful + eventful)
        if (attackInput.WasPresseedThisFrame && runInput.IsHeld && snapshot.Movement.State == MovementType.Run)
        {
            return new ActionIntent
            {
                Movement = new MovementAction { Direction = runInput.Direction, ActionType = MovementType.Run },
                Combat = new CombatAction { ActionType = CombatType.PrimaryAttack },
                AnimatorUpdates = animatorUpdates
            };
        }

        // 2. Jumping Attack (eventful)
        if (attackInput.WasPresseedThisFrame && snapshot.Movement.State == MovementType.Jump)
        {
            return new ActionIntent
            {
                Movement = new MovementAction { ActionType = MovementType.Jump },
                Combat = new CombatAction { ActionType = CombatType.PrimaryAttack },
                AnimatorUpdates = animatorUpdates
            };
        }

        // 3. Standard Attack (eventful)
        if (attackInput.WasPresseedThisFrame)
        {
            Debug.Log("Attack Input given");
            return new ActionIntent
            {
                Movement = new MovementAction { Direction = attackInput.Direction, ActionType = MovementType.Idle },
                Combat = new CombatAction { ActionType = CombatType.PrimaryAttack },
                AnimatorUpdates = animatorUpdates
            };
        }

        // 4. Parry (eventful)
        if (parryInput.WasPresseedThisFrame)
        {
            return new ActionIntent
            {
                Combat = new CombatAction { ActionType = CombatType.Parry },
                AnimatorUpdates = animatorUpdates
            };
        }

        // Optionally: always send animator updates to keep parameters in sync (even if no combat)
        // If needed, uncomment:
        // return new ActionIntent { AnimatorUpdates = animatorUpdates };

        // No actionable intent
        return null;
    }
}
