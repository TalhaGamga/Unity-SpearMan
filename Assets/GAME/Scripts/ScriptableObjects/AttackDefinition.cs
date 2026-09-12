using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "ScriptableObjects/Combat/Attack Definition")]
public class AttackDefinition : ScriptableObject
{
    public string Key;

    public float Damage;

    [Tooltip("Who moves the character while this attack runs. RootMotion lets " +
        "the clip's authored lunge carry the body and reduces move input to " +
        "facing only - which is what keeps the model and the collider in the " +
        "same place. Simulated keeps the mover in charge, for attacks the " +
        "player is meant to walk out of.")]
    public LocomotionSource Locomotion = LocomotionSource.RootMotion;

    public bool HasImpact;
    public ImpactSettings Impact;

    public bool HasReaction;
    public HitReactionSettings Reaction;

    [FormerlySerializedAs("Breaks")]
    public bool IsDestructive;

    public bool CanSlice;
}