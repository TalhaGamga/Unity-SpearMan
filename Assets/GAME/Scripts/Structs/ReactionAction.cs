using UnityEngine;

public struct ReactionAction
{
    public ReactionType ActionType;
    public Vector2 Direction;
    public float Force;
    public float Duration;
    public bool LocksMovementInput;
    public bool LocksCombatInput;
    public bool AllowsAirDrift;
}