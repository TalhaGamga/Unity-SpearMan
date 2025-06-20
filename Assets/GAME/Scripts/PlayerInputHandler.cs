using R3;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] private InputReader _input;
    [SerializeField] private CharacterHub _hub;

    public Subject<InputType> InputStream { get; } = new();
    public BehaviorSubject<Vector2> MovementDirectionStream { get; } = new(Vector2.zero);

    private void Start()
    {
        _input.Move += direction => MovementDirectionStream.OnNext(direction);

        _input.Move += direction =>
        {
            InputStream.OnNext(new InputType
            {
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
                Action = PlayerAction.Attack,
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
