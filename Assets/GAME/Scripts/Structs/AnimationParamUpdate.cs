using UnityEngine;

public enum AnimatorParamUpdateType
{
    Float, Int, Bool, Trigger, RootMotion
}

public struct AnimatorParamUpdate
{
    public string ParamName;
    public AnimatorParamUpdateType ParamType;
    public object Value;
    public bool ResetTrigger;

    public static AnimatorParamUpdate Trigger(string paramName, bool reset = false) =>
    new AnimatorParamUpdate
    {
        ParamName = paramName,
        ParamType = AnimatorParamUpdateType.Trigger,
        Value = null,
        ResetTrigger = reset
    };

    public static AnimatorParamUpdate RootMotion(bool useRootMotion) => new AnimatorParamUpdate
    {
        ParamType = AnimatorParamUpdateType.RootMotion,
        Value = useRootMotion
    };
}