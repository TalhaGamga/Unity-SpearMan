using DevVorpian;
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

        _stateMachine.OnTransitionedAutonomously.AddListener(submitAutonomicStateTransition);

        _snapshotStreamer
            .Select(_ => buildSnapshot())
            .DistinctUntilChanged()
            .Subscribe(snapshotStream.OnNext)
            .AddTo(_disposables);

        _transitionStreamer
            .Pairwise()
            .Subscribe(pair =>
            {
                transitionStream.OnNext(new ReactionTransition(pair.Previous, pair.Current));
            })
            .AddTo(_disposables);

        // states and transitions will be added next

        submitSnapshot();
    }

    public void End()
    {
        _disposables?.Dispose();
        _disposables = null;
    }

    public void HandleAction(ReactionAction action)
    {
        if (action.ActionType == ReactionType.None)
            return;

        _stateMachine.SetState(action.ActionType);
        _transitionStreamer.OnNext(action.ActionType);
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
            Direction = _context.Direction,
            Force = _context.Force,
            Duration = _context.Duration,
            IsInHitStun = _context.IsInHitStun,
            IsAirborneByReaction = _context.IsAirborneByReaction,
            LocksMovementInput = _context.LocksMovementInput,
            LocksCombatInput = _context.LocksCombatInput,
            AllowsAirDrift = _context.AllowsAirDrift
        };
    }

    private void submitSnapshot()
    {
        _snapshotStreamer.OnNext(Unit.Default);
    }

    private void submitAutonomicStateTransition()
    {
        _transitionStreamer.OnNext(_context.State);
    }

    [System.Serializable]
    public class Context
    {
        public ReactionType State = ReactionType.None;

        public Vector2 Direction;
        public float Force;
        public float Duration;
        public float Elapsed;

        public bool IsInHitStun;
        public bool IsAirborneByReaction;
        public bool LocksMovementInput;
        public bool LocksCombatInput;
        public bool AllowsAirDrift;
    }
}