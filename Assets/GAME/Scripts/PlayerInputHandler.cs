using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] private MovementManager movementManager;
    [SerializeField] private InputReader _input;
    private IMovementInputReceiver _movementManager => movementManager;

    private Vector2 _moveInput;

    private void Start()
    {
        _input.Move += direction => _moveInput = direction;
        _input.Jump += isJumpKeyPressed =>
        {
            if (isJumpKeyPressed)
            {
                _movementManager.HandleInput(new MovementAction()
                { ActionType = MovementActionType.Jump });
            }
            else
            {
                _movementManager.HandleInput(new MovementAction()
                { ActionType = MovementActionType.Land });
            }
        };

        _input.Enable();
    }

    public void OnJump(bool isJumping)
    {

    }

    public void OnDash(InputAction.CallbackContext ctx)
    {

    }

    public void OnClimb(InputAction.CallbackContext ctx)
    {
    }

    private void Update()
    {
        _movementManager.SetMoveInput(_moveInput);
    }
}
