using R3;
using UnityEngine;

public class MovementManager : MonoBehaviour, IMovementManager, IMovementInputReceiver, IReactiveCapabilityProvider
{
    public BehaviorSubject<MovementSnapshot> SnapshotStream { get; } = new(MovementSnapshot.Default);

    public Transform CharacterOrientator => _characterModelTransform;
    public Transform CharacterTranslater => _characterTransform;

    [SerializeField] private Transform _characterModelTransform;
    [SerializeField] private Transform _characterTransform;
    [SerializeField] private Transform[] _groundCheckPoints;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private float _groundCheckDistance = 0.1f;

    [SerializeField] private MovementType currentState;

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

    private void Awake()
    {
        SetMover(new RbMover());
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
    }

    private void Update()
    {
        _currentMover?.UpdateMover(Time.deltaTime);
        currentState = _currentMover.LastState;
    }

    public void SetMover(IMover newMover)
    {
        _currentMover?.End();
        _currentMover = newMover;
        _currentMover?.Init(this, SnapshotStream);

        _disposables.Clear();

        SnapshotStream.AddTo(_disposables);
    }

    public void SetSpeedModifier(float newModifier) => _speedModifier = newModifier;
    public void SetJumpModifier(float newModifier) => _jumpModifier = newModifier;

    public void HandleInput(MovementAction action)
    {
        Debug.Log("HandleMovementInput");
        _currentMover.HandleInput(action);
    }

    public void HandleRootMotion(RootMotionFrame rootMotion)
    {
        _currentMover.HandleRootMotion(rootMotion.DeltaPosition);
    }


    public bool GetIsGrounded()
    {
        foreach (var checkPoint in _groundCheckPoints)
        {
            return Physics.OverlapSphere(checkPoint.position, _groundCheckDistance, _groundLayer).Length > 0;
        }

        return false;
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
}
