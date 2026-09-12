using System.Collections.Generic;

public static class InputBehaviorMap
{
    public static readonly Dictionary<PlayerAction, InputBehavior> Behavior = new()
    {
        {PlayerAction.MouseDelta,InputBehavior.Stateful },
        { PlayerAction.Move, InputBehavior.Stateful },
        // Stateful, not eventful: variable jump height needs the release edge,
        // and an eventful action never reports one - it collapses back to
        // "not held" before the release can be observed.
        { PlayerAction.Jump, InputBehavior.Stateful },
        { PlayerAction.PrimaryAttack, InputBehavior.Eventful },
        {PlayerAction.Dash, InputBehavior.Eventful },
        // Extend...
    };
}
