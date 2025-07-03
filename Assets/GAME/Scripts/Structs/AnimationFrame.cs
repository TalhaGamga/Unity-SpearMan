using UnityEngine;

public struct AnimationFrame
{
    public string Action;
    public string EventKey;
    public int Stage;
    public bool IsCancelable;
    public string StateName;
    public float NormalizedTime;
    public int Layer;
    public Animator Animator;
    public int ComboStep;      // New!
    public string ComboType;   // New!

    public AnimationFrame(
        string action,
        string eventKey,
        int stage,
        bool isCancelable,
        string stateName,
        float normalizedTime,
        int layer,
        Animator animator,
        int comboStep = 0,
        string comboType = "")
    {
        Action = action;
        EventKey = eventKey;
        Stage = stage;
        IsCancelable = isCancelable;
        StateName = stateName;
        NormalizedTime = normalizedTime;
        Layer = layer;
        Animator = animator;
        ComboStep = comboStep;
        ComboType = comboType;
    }
}
