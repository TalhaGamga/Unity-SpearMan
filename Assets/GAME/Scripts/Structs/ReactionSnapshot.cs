public struct ReactionSnapshot
{
    public static readonly ReactionSnapshot Default = new ReactionSnapshot
    {
        State = ReactionType.None,
        Version = 0,
        Duration = 0f,
        IsInHitStun = false,
        LocksMovementInput = false,
        LocksCombatInput = false,
        AllowsAirDrift = false
    };

    public ReactionType State;
    public int Version;
    public float Duration;

    public bool IsInHitStun;
    public bool LocksMovementInput;
    public bool LocksCombatInput;
    public bool AllowsAirDrift;
}