using UnityEngine;
using UnityEngine.InputSystem;

public class MovementInputContext
{
    public Vector2 MoveInput;
    public bool JumpPressed;
    public bool ClimbPressed;
    public bool DashPressed;
    public InputAction.CallbackContext RawContext;
}