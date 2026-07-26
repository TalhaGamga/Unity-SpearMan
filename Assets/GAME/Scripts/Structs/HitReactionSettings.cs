[System.Serializable]
public struct HitReactionSettings
{
    public HitReactionType Type;
    public float Duration;
    public bool LocksMovementInput;
    public bool LocksCombatInput;
    public bool AllowsAirDrift;
}