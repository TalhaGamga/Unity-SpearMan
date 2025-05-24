using R3;
using UnityEngine;

public class MovementManager : MonoBehaviour, IMovementManager, IMovementInputReceiver, IMovementStateProvider, IReactiveCapabilityProvider
{
    [SerializeField] private Transform _characterModelTransform;
    public Transform CharacterModelTransform => _characterModelTransform;

    private readonly Subject<MovementSnapshot> _stream = new();
    public Observable<MovementSnapshot> Stream => _stream;

    bool IMovementStateProvider.IsGrounded => throw new System.NotImplementedException();

    public bool CurrentSpeed => throw new System.NotImplementedException();

    private IMover _currentMover;

    private bool _isGrounded;
    private float _speedModifier = 1f;
    private float _jumpModifier = 1f;

    private readonly BehaviorSubject<(bool, string)> _movability = new((true, ""));
    private readonly BehaviorSubject<(bool, string)> _jumpability = new((true, ""));

    public bool IsGrounded() => _isGrounded;

    public Observable<(bool Allowed, string Reason)> ObserveCapability(Capability capability)
    {
        return capability switch
        {
            Capability.Movability => _movability,
            Capability.Jumpability => _jumpability,
            _ => Observable.Return((true, ""))
        };
    }

    public void SetMover(IMover newMover)
    {
        _currentMover?.End();
        _currentMover = newMover;
        _currentMover?.Init(this);
    }

    public void SetSpeedModifier(float newModifier) => _speedModifier = newModifier;
    public void SetJumpModifier(float newModifier) => _jumpModifier = newModifier;

    private void Update()
    {
        _currentMover?.UpdateMover(Time.deltaTime);
    }

    public void HandleInput(MovementAction action)
    {
        _currentMover?.HandleInput(action);
    }

    public void SetMoveInput(Vector2 move)
    {
        _currentMover?.SetMoveInput(move);
    }
}
