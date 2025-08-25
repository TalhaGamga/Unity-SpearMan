using System.Collections.Generic;

public static class InputBehaviorMap
{
    public static readonly Dictionary<PlayerAction, InputBehavior> Behavior = new()
    {
        { PlayerAction.Move, InputBehavior.Stateful },
        { PlayerAction.Jump, InputBehavior.Eventful },
        { PlayerAction.PrimaryAttack, InputBehavior.Eventful },
        // Extend...
    };
}
