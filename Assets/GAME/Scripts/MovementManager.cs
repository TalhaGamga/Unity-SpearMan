using R3;
using UnityEngine;

public class MovementManager : MonoBehaviour, IMovementManager, IMovementInputReceiver, IMovementStateProvider, IReactiveCapabilityProvider, IInitializable<CharacterHub>
{
    public Transform CharacterOrientator => _characterModelTransform;
    public Transform CharacterTranslater => _characterTransform;

    public Observable<MovementSnapshot> Stream => _currentMover.Stream;

    public bool IsGrounded => _isGrounded;
    public float CurrentSpeed => _currentSpeed;


    [SerializeField] private Transform _characterModelTransform;
    [SerializeField] private Transform _characterTransform;
    [SerializeField] private Transform[] _groundCheckPoints;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private float _groundCheckDistance = 0.1f;

    private IMover _currentMover;
    private bool _isGrounded;
    private float _speedModifier = 1f;
    private float _jumpModifier = 1f;
    private float _currentSpeed;

    private readonly BehaviorSubject<(bool, string)> _movability = new((true, ""));
    private readonly BehaviorSubject<(bool, string)> _jumpability = new((true, ""));
    private readonly Subject<MovementSnapshot> _stream = new();

    private void OnDrawGizmosSelected()
    {
        if (_groundCheckPoints != null)
        {
            Gizmos.color = Color.green;
            foreach (var checkPoint in _groundCheckPoints)
            {
                if (checkPoint == null) continue;
                Gizmos.DrawLine(checkPoint.position, checkPoint.position + Vector3.down * _groundCheckDistance);
            }
        }
    }


    private void Awake()
    {
        SetMover(new RbMover());
    }

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
        _currentMover.HandleInput(action);
    }

    public void SetMoveInput(Vector2 move)
    {
        _currentMover?.SetMoveInput(move);
    }

    public bool GetIsGrounded()
    {
        foreach (var checkPoint in _groundCheckPoints)
        {
            if (checkPoint == null) continue;

            // Cast a ray straight down from each point
            if (Physics.Raycast(checkPoint.position, Vector3.down, out RaycastHit hit, _groundCheckDistance, _groundLayer))
            {
                return true; // At least one point is touching ground
            }
        }
        return false; // No points touching ground
    }

    public void Initialize(CharacterHub hub)
    {

    }
}
