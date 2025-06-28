using System.Collections.Generic;

public struct InputSnapshot
{
    public IReadOnlyDictionary<PlayerAction, InputType> CurrentInputs;
    public float TimeStamp;

    // Empty snapshot (with empty dictionary and default values)
    public static InputSnapshot Empty => new InputSnapshot
    {
        CurrentInputs = new Dictionary<PlayerAction, InputType>(),
        TimeStamp = 0f
    };
}
