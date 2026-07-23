using UnityEngine.Serialization;

[System.Serializable]
public struct HitReactionSettings
{
    public ReactionType Type;

    [FormerlySerializedAs("Force")]
    public float ForceMultiplier;

    public float Duration;
    public bool LocksMovementInput;
    public bool LocksCombatInput;
    public bool AllowsAirDrift;
}
