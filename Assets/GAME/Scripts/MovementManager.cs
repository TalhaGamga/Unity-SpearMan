using R3;
using UnityEngine;

public class MovementManager : MonoBehaviour, IMovementManager, IMovementInputReceiver, IReactiveCapabilityProvider, IInitializable<CharacterHub>
{
    public Transform CharacterOrientator => _characterModelTransform;
    public Transform CharacterTranslater => _characterTransform;

    public Observable<MovementSnapshot> Stream => _currentMover.Stream;
    public BehaviorSubject<MovementSnapshot> SnapshotStream { get; } = new(MovementSnapshot.Default); // System will be like

    [SerializeField] private Transform _characterModelTransform;
    [SerializeField] private Transform _characterTransform;
    [SerializeField] private Transform[] _groundCheckPoints;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private float _groundCheckDistance = 0.1f;

    private IMover _currentMover;
    private float _speedModifier = 1f;
    private float _jumpModifier = 1f;
    private float _currentSpeed;

    private readonly BehaviorSubject<(bool, string)> _movability = new((true, ""));
    private readonly BehaviorSubject<(bool, string)> _jumpability = new((true, ""));
    private readonly Subject<MovementSnapshot> _stream = new();
    private readonly CompositeDisposable _disposables = new();

    private void OnDrawGizmosSelected()
    {
        if (_groundCheckPoints != null)
        {
            Gizmos.color = Color.green;
            foreach (var checkPoint in _groundCheckPoints)
            {
                if (checkPoint == null) continue;
                Gizmos.DrawSphere(checkPoint.position, _groundCheckDistance);
            }
        }

    }

    private void OnDestroy()
    {
        _disposables.Dispose();
    }

    private void Awake()
    {
        SetMover(new RbMover());
    }
    private void Update()
    {
        _currentMover?.UpdateMover(Time.deltaTime);
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

        _disposables.Clear();

        _currentMover.Stream
            .Subscribe(snapshot =>
            {
                SnapshotStream.OnNext(snapshot);
            })
            .AddTo(_disposables);
    }

    public void SetSpeedModifier(float newModifier) => _speedModifier = newModifier;
    public void SetJumpModifier(float newModifier) => _jumpModifier = newModifier;


    public void HandleInput(MovementAction action)
    {
        _currentMover.HandleInput(action);
    }

    public void HandleRootMotion(RootMotionFrame rootMotion)
    {
        _currentMover.HandleRootMotion(rootMotion.DeltaPosition);
    }

    public void SetMoveInput(Vector2 move)
    {
        _currentMover?.SetMoveInput(move);
    }

    public bool GetIsGrounded()
    {
        foreach (var checkPoint in _groundCheckPoints)
        {
            return Physics.OverlapSphere(checkPoint.position, _groundCheckDistance, _groundLayer).Length > 0;
        }

        return false;
    }

    public void Initialize(CharacterHub hub)
    {
    }
}
