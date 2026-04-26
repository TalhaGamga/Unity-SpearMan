using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Combat/Attack Definition")]
public class AttackDefinition : ScriptableObject
{
    public string Key;

    public float Damage;

    public bool HasImpact;
    public ImpactData Impact;

    public bool HasReaction;
    public HitReaction Reaction;

    public bool Breaks;
}