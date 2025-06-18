using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] private MovementManager movementManager;
    [SerializeField] private CombatManager combatManager;
    [SerializeField] private InputReader _input;
    private IMovementInputReceiver _movementManager => movementManager;
    private ICombatInputReceiver _combatInputReceiver => combatManager;
    private Vector2 _moveInput;

    private void Start()
    { 
        _input.Move += direction => _moveInput = direction;
        _input.Jump += isJumpKeyPressed =>
        {
            if (isJumpKeyPressed)
            {
                _movementManager.HandleInput(new MovementAction()
                { ActionType = MovementType.Jump });
            } 
            else
            {
                _movementManager.HandleInput(new MovementAction()
                { ActionType = MovementType.Land });
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
