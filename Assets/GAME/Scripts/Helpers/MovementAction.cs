using UnityEngine;

public enum MovementActionType
{
    Move,
    Jump,
    Land,
    Dash,
    Climb,
    SwimUp,
    Interact,
    // Add more as needed
}

public struct MovementAction
{
    public MovementActionType ActionType;
    public float Duration;
    public Vector2 Direction;
}
