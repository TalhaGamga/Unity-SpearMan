using UnityEngine.InputSystem;

public enum MovementActionType
{
    Jump,
    Dash,
    Climb,
    SwimUp,
    Interact,
    // Add more as needed
}

public struct MovementAction
{
    public MovementActionType ActionType;
    public InputAction.CallbackContext InputContext;

    public MovementAction(MovementActionType actionType, InputAction.CallbackContext ctx)
    {
        ActionType = actionType;
        InputContext = ctx;
    }
}
