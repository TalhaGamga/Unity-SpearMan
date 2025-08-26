using UnityEngine;

public struct CombatAnimationFrame
{
    public string Action;
    public string EventKey;
    public int Stage;
    public bool IsCancelable;
    public string StateName;
    public int ComboStep;      // New!
    public string ComboType;   // New!

    public CombatAnimationFrame(
        string action,
        string eventKey,
        int stage,
        bool isCancelable,
        string stateName,
        int comboStep = 0,
        string comboType = "")
    {
        Action = action;
        EventKey = eventKey;
        Stage = stage;
        IsCancelable = isCancelable;
        StateName = stateName;
        ComboStep = comboStep;
        ComboType = comboType;
    }
}
