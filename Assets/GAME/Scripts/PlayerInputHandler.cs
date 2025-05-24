using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] private MovementManager movementManager;
    private IMovementInputReceiver _movementManager => movementManager;

    private Vector2 _moveInput;

    public void OnMove(InputAction.CallbackContext ctx)
    {
        Debug.Log("OnMove");
        _moveInput = ctx.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            var action = new MovementAction(MovementActionType.Jump, ctx);
            _movementManager.HandleInput(action);
        }
    }

    public void OnDash(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            var action = new MovementAction(MovementActionType.Dash, ctx);
            _movementManager.HandleInput(action);
        }
    }

    public void OnClimb(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            var action = new MovementAction(MovementActionType.Climb, ctx);
            _movementManager.HandleInput(action);
        }
    }

    private void Update()
    {
        _movementManager.SetMoveInput(_moveInput);
    }
}
