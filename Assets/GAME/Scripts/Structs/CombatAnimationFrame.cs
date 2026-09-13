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
    public AnimationClip SourceClip;

    /// <summary>
    /// Visual cue key, authored on the clip as <c>Cue=...</c>. Meaningless to
    /// combat - the equipped weapon looks it up in its own visual pack. Kept
    /// separate from <see cref="Action"/> because that field already carries
    /// movement semantics.
    /// </summary>
    public string Cue;

    public CombatAnimationFrame(
        string action,
        string eventKey,
        int stage,
        bool isCancelable,
        string stateName,
        int comboStep = 0,
        string comboType = "",
        string cue = "")
    {
        Cue = cue;
        Action = action;
        EventKey = eventKey;
        Stage = stage;
        IsCancelable = isCancelable;
        StateName = stateName;
        ComboStep = comboStep;
        ComboType = comboType;
        SourceClip = null;
    }
}
