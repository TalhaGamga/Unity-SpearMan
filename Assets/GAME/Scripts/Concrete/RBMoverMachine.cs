using DevVorpian;
using Movement.State;
using R3;
using UnityEngine;

namespace Movement
{
    namespace Mover
    {
        [System.Serializable]
        public class RBMoverMachine : IMover
        {
            public MovementType CurrentType => MovementType.None;

            [SerializeField] private Context _context;

            [SerializeField] private StateMachine<MovementType> _stateMachine;
            private CompositeDisposable _disposables = new();
            private IMovementManager _manager;

            public void Init(IMovementManager movementManager, Subject<MovementSnapshot> snapshotStream, Subject<MovementTransition> transitionStream)
            {
                _stateMachine = new StateMachine<MovementType>();
                _manager = movementManager;

                IState moveState = new RbMove(_context);
                IState idleState = new RbIdle(_context);
                IState fallState = new RbFall(_context);
                IState neutralState = new RbNeutral(_context);

                StateTransition<MovementType> toMove = new StateTransition<MovementType>(MovementType.None, MovementType.Move, moveState, () => Debug.Log("Transitioning to Move"));
                StateTransition<MovementType> toIdle = new StateTransition<MovementType>(MovementType.None, MovementType.Idle, idleState, () => Debug.Log("Transitioning to Idle"));
                StateTransition<MovementType> toFall = new StateTransition<MovementType>(MovementType.None, MovementType.Fall, () => !_manager.GetIsGrounded(), fallState, () => Debug.Log("Transitioning to Fall"));
                StateTransition<MovementType> fallToNeutral = new StateTransition<MovementType>(MovementType.Fall, MovementType.Neutral, () => _manager.GetIsGrounded(), neutralState, () => Debug.Log("Transitioning to Neutral"));

                _stateMachine.AddIntentBasedTransition(toMove);
                _stateMachine.AddIntentBasedTransition(toIdle);

                _stateMachine.AddAutonomicTransition(fallToNeutral);
                _stateMachine.AddAutonomicTransition(toFall);

                _stateMachine.OnTransitionedAutonomously = () => _context.AutonomicTransitionStream.OnNext(_context.State);

                var sub = _context.AnyRelevantChange
                    .Select(_ => new MovementSnapshot(_context.State, _context.Speed, _context.JumpStage))
                    .DistinctUntilChanged()
                    .Subscribe(snapshotStream.OnNext)
                    .AddTo(_disposables);


                var autonomicTransition = _context.AutonomicTransitionStream
                    .Pairwise()
                    .Subscribe(pair =>
                    {
                        transitionStream.OnNext(new MovementTransition(pair.Previous, pair.Current));
                    }
                    ).AddTo(_disposables);

                _stateMachine.SetState(MovementType.Idle);
            }

            public void End()
            {
                _disposables?.Dispose();
                _disposables = null;
            }

            public void HandleAction(MovementAction action)
            {
                _context.MoveInput = action.Direction;
                _stateMachine.SetState(action.ActionType);
            }

            public void HandleRootMotion(Vector3 delta)
            {
                Debug.Log(delta);
                _context.RootMotionDelta = delta;
            }

            public void UpdateMover(float deltaTime)
            {
                _stateMachine.Update();
            }

            [System.Serializable]
            public class Context
            {
                public BehaviorSubject<MovementType> AutonomicTransitionStream = new(MovementType.Idle);

                public Vector2 MoveInput;
                public Vector3 RootMotionDelta;
                public Rigidbody Rb;

                private MovementType _state = MovementType.Idle;
                public MovementType State
                {
                    get => _state;
                    set
                    {
                        if (_state == value) return;
                        _state = value;
                        _changedSubject.OnNext(Unit.Default);
                    }
                }

                private float _speed;
                private readonly BehaviorSubject<float> _speedSubject = new(0f);
                public Observable<float> SpeedChanged => _speedSubject;
                public float Speed
                {
                    get => _speed;
                    set
                    {
                        if (Mathf.Approximately(_speed, value)) return;
                        _speed = value;
                        _speedSubject.OnNext(value);
                        _changedSubject.OnNext(Unit.Default);
                    }
                }

                private int _jumpStage;
                private readonly BehaviorSubject<int> _jumpStageSubject = new(0);
                public Observable<int> JumpStageChanged => _jumpStageSubject;
                public int JumpStage
                {
                    get => _jumpStage;
                    set
                    {
                        if (_jumpStage == value) return;
                        _jumpStage = value;
                        _jumpStageSubject.OnNext(value);
                        _changedSubject.OnNext(Unit.Default);
                    }
                }

                private readonly Subject<Unit> _changedSubject = new();
                public Observable<Unit> AnyRelevantChange => _changedSubject;
            }
        }
    }
}