using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private InputControls inputControls;
    private ICharacterPromptReceiver promptReceiver;

    private void Awake()
    {
        inputControls = new InputControls();
        inputControls?.Enable();

        promptReceiver = GameObject.FindWithTag("Player").GetComponent<CharacterPromptReceiver>();
    }

    private void OnEnable()
    {
        inputControls.Character.Movement.started += onMoveInput;
        inputControls.Character.Movement.canceled += onMoveInput;
        inputControls.Character.Jump.started += onJumpInput;
        inputControls.Character.Jump.canceled += onJumpCancel;
    }

    private void OnDisable()
    {
        inputControls.Character.Movement.started -= onMoveInput;
        inputControls.Character.Movement.canceled -= onMoveInput;
        inputControls.Character.Jump.started -= onJumpInput;
        inputControls.Character.Jump.canceled -= onJumpCancel;
    }

    private void onMoveInput(InputAction.CallbackContext context)
    {
        promptReceiver.OnMoveInput(context.ReadValue<Vector2>());
    }

    private void onJumpInput(InputAction.CallbackContext context)
    {
        promptReceiver.OnJumpInput?.Invoke();
    }

    private void onJumpCancel(InputAction.CallbackContext context)
    {
        promptReceiver?.OnJumpCancel?.Invoke();
    }
}