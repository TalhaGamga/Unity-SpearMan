using UnityEngine;

public struct ReactionSnapshot
{
    public static readonly ReactionSnapshot Default = new ReactionSnapshot
    {
        State = ReactionType.None,
        Direction = Vector2.zero,
        Force = 0f,
        Duration = 0f,
        IsInHitStun = false,
        IsAirborneByReaction = false,
        LocksMovementInput = false,
        LocksCombatInput = false,
        AllowsAirDrift = false
    };

    public ReactionType State;
    public Vector2 Direction;
    public float Force;
    public float Duration;

    public bool IsInHitStun;
    public bool IsAirborneByReaction;
    public bool LocksMovementInput;
    public bool LocksCombatInput;
    public bool AllowsAirDrift;
}