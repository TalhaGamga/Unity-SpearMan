[System.Serializable]
public struct HitReaction
{
    public HitReactionType Type;
    public float Duration;
    public bool LocksMovementInput;
    public bool LocksCombatInput;
    public bool AllowsAirDrift;
}