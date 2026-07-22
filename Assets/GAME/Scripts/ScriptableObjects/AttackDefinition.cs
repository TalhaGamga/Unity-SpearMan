using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "ScriptableObjects/Combat/Attack Definition")]
public class AttackDefinition : ScriptableObject
{
    public string Key;

    public float Damage;

    public bool HasImpact;
    public ImpactData Impact;

    public bool HasReaction;
    public HitReaction Reaction;

    [FormerlySerializedAs("Breaks")]
    public bool IsDestructive;
    public DestructData Destruct;
}