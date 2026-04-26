using UnityEngine;

[System.Serializable]
public struct HitReaction
{
    public ReactionType Type;
    public Vector2 Direction;
    public float Force;
    public float Duration;
    public bool LocksMovementInput;
    public bool LocksCombatInput;
    public bool AllowsAirDrift;
}