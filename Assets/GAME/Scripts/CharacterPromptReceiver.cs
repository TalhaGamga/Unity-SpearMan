using System;
using UnityEngine;

public class CharacterPromptReceiver : MonoBehaviour, ICharacterPromptReceiver
{
    public event Action<Vector2> OnMoveInput;
    public event Action OnJumpInput;
    public event Action OnJumpCancel;
    public event Action OnPrimaryCombatInput;
    public event Action OnSecondaryCombatInput;
    public event Action OnPrimaryCombatCancel;
    public event Action OnSecondaryCombatCancel;
    public event Action OnReloadInput;

    public void InvokeMoveInput(Vector2 input) => OnMoveInput?.Invoke(input);
    public void InvokeJumpInput() => OnJumpInput?.Invoke();
    public void InvokeJumpCancel() => OnJumpCancel?.Invoke();
    public void InvokePrimaryCombatInput() => OnPrimaryCombatInput?.Invoke();
    public void InvokeSecondaryCombatInput() => OnSecondaryCombatInput?.Invoke();
    public void InvokePrimaryCombatCancel() => OnPrimaryCombatCancel?.Invoke();
    public void InvokeSecondaryCombatCancel() => OnSecondaryCombatCancel?.Invoke();
    public void InvokeOnReloadInput() => OnReloadInput?.Invoke();
}