using System.Collections.Generic;

public static class InputBehaviorMap
{
    public static readonly Dictionary<PlayerAction, InputBehavior> Behavior = new()
    {
        { PlayerAction.Run, InputBehavior.Stateful },
        { PlayerAction.Jump, InputBehavior.Eventful },
        { PlayerAction.Attack, InputBehavior.Eventful },
        // Extend...
    };
}
