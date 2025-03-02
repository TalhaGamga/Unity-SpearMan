using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private InputControls inputControls;
    private CharacterPromptReceiver promptReceiver;

    private void Awake()
    {
        inputControls = new InputControls();
        inputControls.Enable();

        promptReceiver = GameObject.FindWithTag("Player").GetComponent<CharacterPromptReceiver>();
    }

    private void OnEnable()
    {
        inputControls.Movement.Move.started += onMoveInput;
        inputControls.Movement.Move.canceled += onMoveInput;
        inputControls.Movement.Jump.started += onJumpInput;
        inputControls.Movement.Jump.canceled += onJumpCancel;

        inputControls.Rifle.Fire.started += onPrimaryCombatInput;
        inputControls.Rifle.Fire.canceled += onPrimaryCombatCancel;
        inputControls.Rifle.Reload.started += onReloadInput;
    }


    private void OnDisable()
    {
        inputControls.Movement.Move.started -= onMoveInput;
        inputControls.Movement.Move.canceled -= onMoveInput;
        inputControls.Movement.Jump.started -= onJumpInput;
        inputControls.Movement.Jump.canceled -= onJumpCancel;

        inputControls.Rifle.Fire.started -= onPrimaryCombatInput;
        inputControls.Rifle.Fire.canceled -= onPrimaryCombatCancel;
        inputControls.Rifle.Reload.started -= onReloadInput;
    }

    private void onMoveInput(InputAction.CallbackContext context)
    {
        promptReceiver.InvokeMoveInput(context.ReadValue<Vector2>());
    }

    private void onJumpInput(InputAction.CallbackContext context)
    {
        promptReceiver?.InvokeJumpInput();
    }

    private void onJumpCancel(InputAction.CallbackContext context)
    {
        promptReceiver?.InvokeJumpCancel();
    }

    private void onPrimaryCombatInput(InputAction.CallbackContext context)
    {
        promptReceiver?.InvokePrimaryCombatInput();
    }

    private void onPrimaryCombatCancel(InputAction.CallbackContext context)
    {
        promptReceiver?.InvokePrimaryCombatCancel();
    }

    private void onReloadInput(InputAction.CallbackContext context)
    {
        promptReceiver?.InvokeOnReloadInput();
    }
}
