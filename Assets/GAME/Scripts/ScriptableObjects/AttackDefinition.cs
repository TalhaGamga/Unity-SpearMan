using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "ScriptableObjects/Combat/Attack Definition")]
public class AttackDefinition : ScriptableObject
{
    public string Key;

    public float Damage;

    public bool HasImpact;
    public ImpactSettings Impact;

    public bool HasReaction;
    public HitReactionSettings Reaction;

    [FormerlySerializedAs("Breaks")]
    public bool IsDestructive;
    public DestructSettings Destruct;

    public bool CanSlice;
}