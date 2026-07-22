using UnityEngine.Serialization;

[System.Serializable]
public class AttackReactiveProfile
{
    public float Damage;

    public bool HasImpact;
    public ImpactData Impact;

    public bool HasReaction;
    public HitReaction Reaction;

    [FormerlySerializedAs("Breaks")]
    public bool IsDestructive;
    public DestructData Destruct;
}