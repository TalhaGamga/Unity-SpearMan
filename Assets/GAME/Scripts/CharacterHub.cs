using Movement;
using R3;
using UnityEngine;

public class CharacterHub : MonoBehaviour
{
    [SerializeField] private PlayerInputHandler _inputHandler;
    [SerializeField] private AnimatorSystem _animatorSystem;
    [SerializeField] private MovementManager _movementManager;
    [SerializeField] private CombatManager _combatManager;

    public ActionSystem _actionSystem;
    private Subject<ReactionSnapshot> _dummyReactionSnapshotStream = new();
    private CompositeDisposable _disposables = new();

    private void Awake()
    {
        _actionSystem = new ActionSystem(
            _inputHandler.InputSnapshotStream,
            _movementManager.SnapshotStream,
            _combatManager.SnapshotStream,
            _dummyReactionSnapshotStream,
            new CompositeIntentMapper(new SwordIntentMapper(), new MovementIntentMapper())
            );

        _movementManager.TransitionStream
        .Subscribe(transition =>
        {
            _actionSystem.ProcessIntent();
        });

        _combatManager.TransitionStream
        .Subscribe(transition =>
        {
            _actionSystem.ProcessIntent();
        });

        _actionSystem.MovementIntentStream
            .Subscribe(_movementManager.HandleInput)
            .AddTo(_disposables);

        _actionSystem.CombatIntentStream
            .Subscribe(_combatManager.HandleInput)
            .AddTo(_disposables);

        _movementManager.SnapshotStream.
            Subscribe(_ => _actionSystem.ProcessAnimator())
            .AddTo(_disposables);

        _combatManager.SnapshotStream.
            Subscribe(_ => _actionSystem.ProcessAnimator())
            .AddTo(_disposables);

        _actionSystem.AnimatorUpdates
            .Subscribe(_animatorSystem.HandleAnimatorUpdates)
            .AddTo(_disposables);

        _animatorSystem.RootMotionStream
            .Subscribe(_movementManager.HandleRootMotion)
            .AddTo(_disposables);

        _animatorSystem.CombatAnimationFrameStream
            .Subscribe(_combatManager.OnAnimationFrame)
            .AddTo(_disposables);

        _animatorSystem.MovementAnimationFrameStream
            .Subscribe(_movementManager.OnAnimationFrame)
            .AddTo(_disposables);
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
    }
}