using UnityEngine.Serialization;

[System.Serializable]
public class AttackReactiveProfile
{
    public float Damage;

    public bool HasImpact;
    public ImpactSettings Impact;

    public bool HasReaction;
    public HitReactionSettings Reaction;

    [FormerlySerializedAs("Breaks")]
    public bool IsDestructive;
    public DestructSettings Destruct;

    public bool CanSlice;
    public SliceSettings Slice;
}