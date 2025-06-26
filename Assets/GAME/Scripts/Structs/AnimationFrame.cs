using UnityEngine;

public struct AnimationFrame
{
    public string ActionType;        // e.g. "Slash", "Fire", "Parry"
    public string EventKey;          // e.g. "HitWindowStart", "HitWindowEnd", "SlashEnd"
    public int Stage;                // For combos or phases (optional)
    public bool IsCancelable;        // If attack can be canceled now
    public string StateName;         // Optional: Animator state name
    public float NormalizedTime;     // 0.0–1.0 in current state
    public int Layer;                // Animator layer index
    public Animator AnimatorSource;  // Who fired it (optional, for advanced use)

    public AnimationFrame(
        string actionType,
        string eventKey,
        int stage,
        bool isCancelable,
        string stateName,
        float normalizedTime,
        int layer,
        Animator animatorSource)
    {
        ActionType = actionType;
        EventKey = eventKey;
        Stage = stage;
        IsCancelable = isCancelable;
        StateName = stateName;
        NormalizedTime = normalizedTime;
        Layer = layer;
        AnimatorSource = animatorSource;
    }
}
