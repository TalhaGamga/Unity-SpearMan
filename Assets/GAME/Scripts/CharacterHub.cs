using Movement;
using R3;
using UnityEngine;

public class CharacterHub : MonoBehaviour
{
    [SerializeField] private MonoBehaviour _inputHandlerSource;
    [SerializeField] private AnimatorSystem _animatorSystem;
    [SerializeField] private MovementManager _movementManager;
    [SerializeField] private CombatManager _combatManager;
    [SerializeField] private ReactionManager _reactionManager;

    private IInputHandler _inputHandler;

    public ActionSystem _actionSystem;
    private CompositeDisposable _disposables = new();

    private void Awake()
    {
        _inputHandler = _inputHandlerSource as IInputHandler;

        if (_inputHandler == null)
        {
            Debug.LogError($"{name}: Assigned input source does not implement IInputHandler.");
            return;
        }

        _actionSystem = new ActionSystem(
            _inputHandler.InputSnapshotStream,
            _movementManager.SnapshotStream,
            _combatManager.SnapshotStream,
            _reactionManager.SnapshotStream,
            new CompositeIntentMapper(
                new ForcedMotionIntentMapper(),
                new ReactionIntentMapper(),
                new SwordIntentMapper(),
                new MovementIntentMapper())
        );

        WireIntentTriggers();
        WireAnimatorTriggers();
        WireActionOutputs();
        WireAnimationOutputs();
    }

    private void WireIntentTriggers()
    {
        _inputHandler.InputSnapshotStream
            .Subscribe(_ => _actionSystem.ProcessIntent())
            .AddTo(_disposables);

        _movementManager.TransitionStream
            .Subscribe(_ => _actionSystem.ProcessIntent())
            .AddTo(_disposables);

        _combatManager.TransitionStream
            .Subscribe(_ => _actionSystem.ProcessIntent())
            .AddTo(_disposables);

        _reactionManager.TransitionStream
            .Subscribe(_ => _actionSystem.ProcessIntent())
            .AddTo(_disposables);
    }

    private void WireAnimatorTriggers()
    {
        _movementManager.SnapshotStream
            .Subscribe(_ => _actionSystem.ProcessAnimator())
            .AddTo(_disposables);

        _combatManager.SnapshotStream
            .Subscribe(_ => _actionSystem.ProcessAnimator())
            .AddTo(_disposables);

        _reactionManager.SnapshotStream
            .Subscribe(_ => _actionSystem.ProcessAnimator())
            .AddTo(_disposables);

        _reactionManager.TransitionStream
            .Select(AnimationParameterMapper.ReactionAnimatorMapper)
            .Subscribe(_animatorSystem.HandleAnimatorUpdates)
            .AddTo(_disposables);
    }

    private void WireActionOutputs()
    {
        _actionSystem.MovementIntentStream
            .Subscribe(_movementManager.HandleAction)
            .AddTo(_disposables);

        _actionSystem.CombatIntentStream
            .Subscribe(_combatManager.HandleAction)
            .AddTo(_disposables);
    }

    private void WireAnimationOutputs()
    {
        _actionSystem.AnimatorUpdateStream
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