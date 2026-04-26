[System.Serializable]
public class AttackReactiveProfile
{
    public float Damage;

    public bool HasImpact;
    public ImpactData Impact;

    public bool HasReaction;
    public HitReaction Reaction;

    public bool Breaks;
}