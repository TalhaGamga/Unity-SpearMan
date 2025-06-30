using UnityEngine;

public struct AnimatorParamUpdate
{
    public string ParamName;
    public AnimatorControllerParameterType ParamType;
    public object Value;
    public bool ResetTrigger;

    public static AnimatorParamUpdate Trigger(string paramName, bool reset = false) =>
    new AnimatorParamUpdate
    {
        ParamName = paramName,
        ParamType = AnimatorControllerParameterType.Trigger,
        Value = null,
        ResetTrigger = reset
    };
}