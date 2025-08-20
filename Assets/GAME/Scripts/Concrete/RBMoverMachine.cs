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

                StateTransition<MovementType> toMove = new StateTransition<MovementType>(MovementType.None, MovementType.Move, moveState, () => Debug.Log("Transitioning to Move"));
                StateTransition<MovementType> toIdle = new StateTransition<MovementType>(MovementType.None, MovementType.Idle, idleState, () => Debug.Log("Transitioning to Idle"));
                StateTransition<MovementType> moveToIdle = new StateTransition<MovementType>(MovementType.Move, MovementType.Idle, idleState, () => Debug.Log("Transitioning to Idle from Move"));
                StateTransition<MovementType> toFall = new StateTransition<MovementType>(MovementType.None, MovementType.Fall, () => !_manager.GetIsGrounded(), fallState, () => Debug.Log("Transitioning to Fall"));

                _stateMachine.AddTransition(toMove);
                _stateMachine.AddTransition(toIdle);
                _stateMachine.AddTransition(moveToIdle);

                _stateMachine.AddAutonomicTransition(toFall);

                //_stateMachine.OnAutonomicTransition += () => transitionStream.OnNext(new MovementTransition());

                var coalesced = _context.AnyRelevantChange
                    .ThrottleFirstFrame(1)
                    .Subscribe(_ =>
                    {
                        snapshotStream.OnNext(new MovementSnapshot(_context.State, _context.Speed, _context.JumpStage));
                    });
                _disposables.Add(coalesced);

                var stateChanged = _context.StateChanged
                    .Pairwise()
                    .Subscribe(pair =>
                    {
                        transitionStream.OnNext(new MovementTransition(pair.Previous, pair.Current));
                    }
                    );
                _disposables.Add(stateChanged);

                snapshotStream.OnNext(new MovementSnapshot(_context.State, _context.Speed, _context.JumpStage));

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
                _context.RootMotionDelta = delta;
            }

            public void UpdateMover(float deltaTime)
            {
                _stateMachine.Update();
            }

            [System.Serializable]
            public class Context
            {
                public Vector2 MoveInput;
                public Vector3 RootMotionDelta;
                public Rigidbody Rb;

                private MovementType _state = MovementType.Idle;
                private readonly BehaviorSubject<MovementType> _stateSubject = new(MovementType.Idle);
                public Observable<MovementType> StateChanged => _stateSubject;
                public MovementType State
                {
                    get => _state;

                    set
                    {
                        if (_state == value) return;
                        _state = value;
                        _stateSubject.OnNext(value);
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