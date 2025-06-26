using UnityEngine;

public struct InputType
{
    public PlayerAction Action;
    public bool IsHeld;
    public float Value;
    public Vector2 Direction;

    public InputBehavior Behavior =>
        InputBehaviorMap.Behavior.TryGetValue(Action, out var b) ? b : InputBehavior.Eventful;
}