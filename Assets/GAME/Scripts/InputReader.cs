using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using static PlayerInputActions;

public interface IInputReader
{
    public Vector2 Direction { get; }
    void Enable();
}

[CreateAssetMenu(menuName = "ScriptableObjects/InputReader")]
public class InputReader : ScriptableObject, IPlayerActions, IInputReader
{
    public event UnityAction<Vector2> Move = delegate { };
    public event UnityAction<bool> Jump = delegate { };
    public event UnityAction<bool> Attack = delegate { };
    public event UnityAction<bool> Dash = delegate { };
    public event UnityAction<Vector2> MouseDelta = delegate { };

    public Vector2 Direction => _inputActions.Player.Move.ReadValue<Vector2>();
    public bool IsJumpKeyPressed => _inputActions.Player.Jump.IsPressed();

    private PlayerInputActions _inputActions;

    public void Enable()
    {
        if (_inputActions == null)
        {
            _inputActions = new PlayerInputActions();
            _inputActions.Player.SetCallbacks(this);
        }

        _inputActions.Enable();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (context.started || context.canceled)
            Move.Invoke(context.ReadValue<Vector2>());
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        switch (context.phase)
        {
            case InputActionPhase.Started:
                Jump.Invoke(true);
                break;
            case InputActionPhase.Canceled:
                Jump.Invoke(false);
                break;
        }
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        switch (context.phase)
        {
            case InputActionPhase.Started:
                Attack.Invoke(true);
                break;
            case InputActionPhase.Canceled:
                Attack.Invoke(false);
                break;
        }
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        switch (context.phase)
        {
            case InputActionPhase.Started:
                Dash.Invoke(true);
                break;
            case InputActionPhase.Canceled:
                Dash.Invoke(false);
                break;
        }
    }

    public void OnMouseDelta(InputAction.CallbackContext context)
    {
        var delta = context.ReadValue<Vector2>();
        Debug.Log(delta);
    }

    public void OnCrouch(InputAction.CallbackContext context)
    {
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
    }

    public void OnLook(InputAction.CallbackContext context)
    {
    }


    public void OnNext(InputAction.CallbackContext context)
    {
    }

    public void OnPrevious(InputAction.CallbackContext context)
    {
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
    }


}