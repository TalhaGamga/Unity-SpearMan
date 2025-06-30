using System.Collections.Generic;

public struct ActionIntent
{
    public MovementAction? Movement;
    public CombatAction? Combat;
    public IEnumerable<AnimatorParamUpdate> AnimatorUpdates;
    // Extend with UI, VFX, Audio as needed
}