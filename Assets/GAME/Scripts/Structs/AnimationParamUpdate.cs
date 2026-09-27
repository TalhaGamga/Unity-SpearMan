public enum AnimatorParamUpdateType
{
    Float, Int, Bool, Trigger, RootMotion
}

/// <summary>
/// One parameter write on its way to the animator.
/// </summary>
/// <remarks>
/// Typed fields rather than one boxed object. The mapper emits a dozen of
/// these per state change and every one of them used to allocate, for a value
/// the receiving switch immediately cast back to the type it already knew from
/// <see cref="ParamType"/>.
///
/// The hash is resolved at construction, from <see cref="AnimatorParams"/>, so
/// the name is looked up once per write rather than re-hashed inside Unity on
/// every setter call. The name is kept beside it because triggers still go
/// through the string path and because an error worth reading names the
/// parameter rather than a number.
/// </remarks>
public struct AnimatorParamUpdate
{
    public string ParamName;
    public int ParamHash;
    public AnimatorParamUpdateType ParamType;

    public float FloatValue;
    public int IntValue;
    public bool BoolValue;
    public bool ResetTrigger;

    public static AnimatorParamUpdate Bool(string name, bool value)
    {
        return new AnimatorParamUpdate
        {
            ParamName = name,
            ParamHash = AnimatorParams.HashOf(name),
            ParamType = AnimatorParamUpdateType.Bool,
            BoolValue = value
        };
    }

    public static AnimatorParamUpdate Float(string name, float value)
    {
        return new AnimatorParamUpdate
        {
            ParamName = name,
            ParamHash = AnimatorParams.HashOf(name),
            ParamType = AnimatorParamUpdateType.Float,
            FloatValue = value
        };
    }

    public static AnimatorParamUpdate Int(string name, int value)
    {
        return new AnimatorParamUpdate
        {
            ParamName = name,
            ParamHash = AnimatorParams.HashOf(name),
            ParamType = AnimatorParamUpdateType.Int,
            IntValue = value
        };
    }

    public static AnimatorParamUpdate Trigger(string name, bool reset = false)
    {
        return new AnimatorParamUpdate
        {
            ParamName = name,
            ParamHash = AnimatorParams.HashOf(name),
            ParamType = AnimatorParamUpdateType.Trigger,
            ResetTrigger = reset
        };
    }

    public static AnimatorParamUpdate RootMotion(bool enabled)
    {
        return new AnimatorParamUpdate
        {
            ParamType = AnimatorParamUpdateType.RootMotion,
            BoolValue = enabled
        };
    }
}
