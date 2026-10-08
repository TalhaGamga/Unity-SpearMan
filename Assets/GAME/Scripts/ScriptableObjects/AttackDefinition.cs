using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "ScriptableObjects/Combat/Attack Definition")]
public class AttackDefinition : ScriptableObject
{
    public string Key;

    public float Damage;

    [Header("Flow")]
    [Min(0.01f)]
    [Tooltip("How long an early primary-attack press remains valid while this " +
        "attack waits for its next chain point. The buffer accepts the " +
        "input; animation events still decide when the next attack may begin.")]
    public float AttackBufferDuration = 0.18f;

    [Tooltip("Escapes available as soon as the attack begins. Attack chaining " +
        "remains reserved for the authored combo window.")]
    public CombatCancelOptions StartupCancels =
        CombatCancelOptions.Jump | CombatCancelOptions.Dash;

    [Tooltip("Escapes added when the damage window closes.")]
    public CombatCancelOptions AfterActiveCancels =
        CombatCancelOptions.Mobility;

    [Tooltip("Escapes available at the clip's late Cancelable marker.")]
    public CombatCancelOptions RecoveryCancels = CombatCancelOptions.All;

    [Tooltip("Who moves the character while this attack runs. RootMotion lets " +
        "the clip's authored lunge carry the body and reduces move input to " +
        "facing only - which is what keeps the model and the collider in the " +
        "same place. Simulated keeps the mover in charge, for attacks the " +
        "player is meant to walk out of.")]
    public LocomotionSource Locomotion = LocomotionSource.RootMotion;

    public bool HasImpact;
    public ImpactSettings Impact;

    [Tooltip("Makes a character briefly follow the live weapon tip, then " +
        "release into an angle-shaped launch. This is intentionally " +
        "separate from Impact, which is a one-shot launch impulse.")]
    public bool HasPierce;
    public PierceSettings Pierce;

    public bool HasReaction;
    public HitReactionSettings Reaction;

    [FormerlySerializedAs("Breaks")]
    public bool IsDestructive;

    public bool CanSlice;
}
