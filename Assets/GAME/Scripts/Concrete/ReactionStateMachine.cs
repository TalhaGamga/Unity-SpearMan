using DevVorpian;
using Movement.State;
using R3;
using UnityEngine;

[System.Serializable]
public class ReactionStateMachine : IReactor
{
    public ReactionType CurrentType => _context.State;

    [SerializeField] private Context _context;
    [SerializeField] private StateMachine<ReactionType> _stateMachine;

    private CompositeDisposable _disposables = new();
    private IReactionManager _manager;

    private Subject<Unit> _snapshotStreamer = new();
    private BehaviorSubject<ReactionType> _transitionStreamer = new(ReactionType.None);

    public void Init(
        IReactionManager reactionManager,
        Subject<ReactionSnapshot> snapshotStream,
        Subject<ReactionTransition> transitionStream)
    {
        _stateMachine = new StateMachine<ReactionType>();
        _manager = reactionManager;

        _snapshotStreamer
            .Select(_ => buildSnapshot())
            .DistinctUntilChanged()
            .Subscribe(snapshotStream.OnNext)
            .AddTo(_disposables);

        _transitionStreamer
            .DistinctUntilChanged()
            .Pairwise()
            .Subscribe(pair =>
            {
                transitionStream.OnNext(new ReactionTransition(pair.Previous, pair.Current));
            })
            .AddTo(_disposables);

        IState noneState = new ConcreteState();
        IState lightHitState = new ConcreteState();
        IState launchState = new ConcreteState();
        IState airJuggleState = new ConcreteState();
        IState recoveryState = new ConcreteState();
        IState deadState = new ConcreteState();

        #region OnEnter

        noneState.OnEnter.AddListener(() =>
        {
            setState(ReactionType.None);
            resetReactionFlags();
            SubmitSnapshot();
        });

        lightHitState.OnEnter.AddListener(() =>
        {
            applyPendingActionToContext();

            setState(ReactionType.LightHit);
            _context.Elapsed = 0f;
            _context.IsInHitStun = true;

            SubmitSnapshot();
        });

        launchState.OnEnter.AddListener(() =>
        {
            applyPendingActionToContext();

            setState(ReactionType.Launch);
            _context.Elapsed = 0f;
            _context.IsInHitStun = true;

            SubmitSnapshot();
        });

        airJuggleState.OnEnter.AddListener(() =>
        {
            applyPendingActionToContext();

            setState(ReactionType.AirJuggle);
            _context.Elapsed = 0f;
            _context.IsInHitStun = true;

            SubmitSnapshot();
        });

        recoveryState.OnEnter.AddListener(() =>
        {
            setState(ReactionType.Recovery);
            _context.Elapsed = 0f;

            _context.IsInHitStun = false;
            _context.LocksMovementInput = false;
            _context.LocksCombatInput = false;

            SubmitSnapshot();
        });

        deadState.OnEnter.AddListener(() =>
        {
            setState(ReactionType.Dead);
            _context.Elapsed = 0f;

            _context.IsInHitStun = false;
            _context.LocksMovementInput = true;
            _context.LocksCombatInput = true;
            _context.AllowsAirDrift = false;

            SubmitSnapshot();
        });

        #endregion

        #region OnUpdate

        noneState.OnUpdate.AddListener(() =>
        {
            SubmitSnapshot();
        });

        lightHitState.OnUpdate.AddListener(() =>
        {
            SubmitSnapshot();
        });

        launchState.OnUpdate.AddListener(() =>
        {
            SubmitSnapshot();
        });

        airJuggleState.OnUpdate.AddListener(() =>
        {
            SubmitSnapshot();
        });

        recoveryState.OnUpdate.AddListener(() =>
        {
            SubmitSnapshot();
        });

        deadState.OnUpdate.AddListener(() =>
        {
            SubmitSnapshot();
        });

        #endregion

        #region OnExit

        lightHitState.OnExit.AddListener(() =>
        {
            SubmitSnapshot();
        });

        launchState.OnExit.AddListener(() =>
        {
            SubmitSnapshot();
        });

        airJuggleState.OnExit.AddListener(() =>
        {
            SubmitSnapshot();
        });

        recoveryState.OnExit.AddListener(() =>
        {
            _context.Duration = 0f;
            SubmitSnapshot();
        });

        #endregion

        #region IntentBasedTransitions

        var toNone = new StateTransition<ReactionType>(
            null,
            noneState,
            ReactionType.None,
            () => _context.State != ReactionType.Dead
        );

        var toLightHit = new StateTransition<ReactionType>(
            null,
            lightHitState,
            ReactionType.LightHit,
            () => _context.State != ReactionType.Dead
        );

        var toLaunch = new StateTransition<ReactionType>(
            null,
            launchState,
            ReactionType.Launch,
            () => _context.State != ReactionType.Dead
        );

        var toAirJuggle = new StateTransition<ReactionType>(
            null,
            airJuggleState,
            ReactionType.AirJuggle,
            () => _context.State == ReactionType.Launch || _context.State == ReactionType.AirJuggle
        );

        var toDead = new StateTransition<ReactionType>(
            null,
            deadState,
            ReactionType.Dead,
            () => true
        );

        _stateMachine.AddIntentBasedTransition(toNone);
        _stateMachine.AddIntentBasedTransition(toLightHit);
        _stateMachine.AddIntentBasedTransition(toLaunch);
        _stateMachine.AddIntentBasedTransition(toAirJuggle);
        _stateMachine.AddIntentBasedTransition(toDead);

        #endregion

        #region AutonomicTransitions

        var lightHitToRecovery = new StateTransition<ReactionType>(
            lightHitState,
            recoveryState,
            ReactionType.Recovery,
            () => _context.Elapsed >= _context.Duration
        );

        var launchToRecovery = new StateTransition<ReactionType>(
            launchState,
            recoveryState,
            ReactionType.Recovery,
            () => _context.Elapsed >= _context.Duration
        );

        var airJuggleToRecovery = new StateTransition<ReactionType>(
            airJuggleState,
            recoveryState,
            ReactionType.Recovery,
            () => _context.Elapsed >= _context.Duration
        );

        var recoveryToNone = new StateTransition<ReactionType>(
            recoveryState,
            noneState,
            ReactionType.None,
            () => _context.Elapsed >= _context.RecoveryDuration
        );

        _stateMachine.AddAutonomicTransition(lightHitToRecovery);
        _stateMachine.AddAutonomicTransition(launchToRecovery);
        _stateMachine.AddAutonomicTransition(airJuggleToRecovery);
        _stateMachine.AddAutonomicTransition(recoveryToNone);

        #endregion

        _stateMachine.SetState(ReactionType.None);
        SubmitSnapshot();
    }

    public void End()
    {
        _disposables?.Dispose();
        _disposables = null;
    }

    public void HandleAction(HitReaction action)
    {
        if (!tryResolveRuntimeState(action.Type, out ReactionType state))
            return;

        _context.PendingAction = action;
        _context.Version++;
        _stateMachine.SetState(state);
    }

    public void UpdateReactor(float deltaTime)
    {
        _context.Elapsed += deltaTime;
        _stateMachine.Update();
    }

    private ReactionSnapshot buildSnapshot()
    {
        return new ReactionSnapshot
        {
            State = _context.State,
            Version = _context.Version,
            Duration = _context.Duration,
            IsInHitStun = _context.IsInHitStun,
            LocksMovementInput = _context.LocksMovementInput,
            LocksCombatInput = _context.LocksCombatInput,
            AllowsAirDrift = _context.AllowsAirDrift
        };
    }

    private void SubmitSnapshot()
    {
        _snapshotStreamer.OnNext(Unit.Default);
        _transitionStreamer.OnNext(_context.State);
    }

    private static bool tryResolveRuntimeState(
        HitReactionType request,
        out ReactionType state)
    {
        switch (request)
        {
            case HitReactionType.LightStagger:
                state = ReactionType.LightHit;
                return true;
            case HitReactionType.Launch:
                state = ReactionType.Launch;
                return true;
            case HitReactionType.AirJuggle:
                state = ReactionType.AirJuggle;
                return true;
            default:
                state = ReactionType.None;
                return false;
        }
    }

    private void applyPendingActionToContext()
    {
        _context.Duration = Mathf.Max(_context.PendingAction.Duration, _context.MinStateDuration);
        _context.LocksMovementInput = _context.PendingAction.LocksMovementInput;
        _context.LocksCombatInput = _context.PendingAction.LocksCombatInput;
        _context.AllowsAirDrift = _context.PendingAction.AllowsAirDrift;
    }

    private void resetReactionFlags()
    {
        _context.Duration = 0f;
        _context.Elapsed = 0f;

        _context.IsInHitStun = false;
        _context.LocksMovementInput = false;
        _context.LocksCombatInput = false;
        _context.AllowsAirDrift = false;
    }

    private void setState(ReactionType state)
    {
        _context.State = state;
    }

    [System.Serializable]
    public class Context
    {
        public ReactionType State = ReactionType.None;
        public int Version;
        public HitReaction PendingAction;
        public float Duration;
        public float Elapsed;

        public bool IsInHitStun;
        public bool LocksMovementInput;
        public bool LocksCombatInput;
        public bool AllowsAirDrift;

        public float RecoveryDuration = 0.12f;
        public float MinStateDuration = 0.05f;
    }
}