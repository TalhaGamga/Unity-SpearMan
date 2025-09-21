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
}