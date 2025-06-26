using R3;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] private InputReader _input;
    [SerializeField] private CharacterHub _hub;

    public Subject<InputType> InputStream { get; } = new();

    private void Start()
    {
        _input.Move += direction =>
        {
            InputStream.OnNext(new InputType
            {
                Direction = direction,
                Action = PlayerAction.Run,
                IsHeld = direction.magnitude > 0
            });
        };

        _input.Jump += isPressed =>
        {
            InputStream.OnNext(new InputType
            {
                Action = PlayerAction.Jump,
                IsHeld = isPressed
            });
        };

        _input.Attack += isPressed =>
        {
            InputStream.OnNext(new InputType
            {
                Action = PlayerAction.PrimaryAttack,
                IsHeld = isPressed
            });
        };

        _input.Enable();
    }

    public void OnDash(InputAction.CallbackContext ctx)
    {

    }

    public void OnClimb(InputAction.CallbackContext ctx)
    {
    }
}
