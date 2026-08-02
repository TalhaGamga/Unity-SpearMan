using Movement;
using R3;
using UnityEngine;

public class CharacterHub : MonoBehaviour
{
    private const string LaunchLyingStateName = "Knockdown_LyingFront 1";
    private const string GetUpTriggerName = "GetUp";

    [SerializeField] private MonoBehaviour _inputHandlerSource;
    [SerializeField] private AnimatorSystem _animatorSystem;
    [SerializeField] private MovementManager _movementManager;
    [SerializeField] private CombatManager _combatManager;
    [SerializeField] private ReactionManager _reactionManager;

    private IInputHandler _inputHandler;

    public ActionSystem _actionSystem;
    private CompositeDisposable _disposables = new();
    private bool _awaitingLaunchLandingRecovery;

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
        _reactionManager.TransitionStream
            .Subscribe(TrackLaunchLandingRecovery)
            .AddTo(_disposables);

        _movementManager.TransitionStream
            .Subscribe(HandleLandingAnimatorTransition)
            .AddTo(_disposables);

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

    private void TrackLaunchLandingRecovery(ReactionTransition transition)
    {
        switch (transition.To)
        {
            case ReactionType.Launch:
            case ReactionType.AirJuggle:
                _animatorSystem.CancelPendingStateCompletionTrigger();
                _awaitingLaunchLandingRecovery = true;
                break;
            case ReactionType.LightHit:
            case ReactionType.Knockdown:
            case ReactionType.Dead:
                _awaitingLaunchLandingRecovery = false;
                _animatorSystem.CancelPendingStateCompletionTrigger();
                break;
        }
    }

    private void HandleLandingAnimatorTransition(MovementTransition transition)
    {
        if (!_awaitingLaunchLandingRecovery ||
            transition.From != MovementType.ForcedFall ||
            transition.To != MovementType.Neutral)
        {
            return;
        }

        _awaitingLaunchLandingRecovery = false;
        _animatorSystem.TriggerAfterStateCompletes(
            LaunchLyingStateName,
            GetUpTriggerName
        );
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