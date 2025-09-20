using System.Collections.Generic;

public static class InputBehaviorMap
{
    public static readonly Dictionary<PlayerAction, InputBehavior> Behavior = new()
    {
        {PlayerAction.MouseDelta,InputBehavior.Stateful },
        { PlayerAction.Move, InputBehavior.Stateful },
        { PlayerAction.Jump, InputBehavior.Eventful },
        { PlayerAction.PrimaryAttack, InputBehavior.Eventful },
        {PlayerAction.Dash, InputBehavior.Eventful },
        // Extend...
    };
}
