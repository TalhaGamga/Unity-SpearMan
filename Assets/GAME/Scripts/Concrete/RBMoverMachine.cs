using DevVorpian;
using Movement.State;
using R3;
using System;
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

            private Transform[] _groundCheckPoints;
            private float _groundCheckDistance;
            private LayerMask _groundLayer;
            private Transform _characterorientator;

            [SerializeField] private float _orientationSpeed = 10f;
            public void Init(IMovementManager movementManager, Subject<MovementSnapshot> snapshotStream, Subject<MovementTransition> transitionStream)
            {
                _stateMachine = new StateMachine<MovementType>();
                _manager = movementManager;
                _groundCheckPoints = _manager.GroundCheckPoints;
                _groundCheckDistance = _manager.GroundCheckDistance;
                _groundLayer = _manager.GroundLayer;
                _characterorientator = _manager.CharacterOrientator;

                IState moveState = new RbMove(_context);
                IState idleState = new RbIdle(_context);
                IState fallState = new RbFall(_context);
                IState neutralState = new RbNeutral(_context);

                StateTransition<MovementType> toMove = new StateTransition<MovementType>(MovementType.None, MovementType.Move, moveState, () => Debug.Log("Transitioning to Move"));
                StateTransition<MovementType> toIdle = new StateTransition<MovementType>(MovementType.None, MovementType.Idle, idleState, () => Debug.Log("Transitioning to Idle"));
                StateTransition<MovementType> toFall = new StateTransition<MovementType>(MovementType.None, MovementType.Fall, () => !IsGrounded(), fallState, () => Debug.Log("Transitioning to Fall"));
                StateTransition<MovementType> fallToNeutral = new StateTransition<MovementType>(MovementType.Fall, MovementType.Neutral, () => IsGrounded(), neutralState, () => Debug.Log("Transitioning to Neutral"));

                _stateMachine.AddIntentBasedTransition(toMove);
                _stateMachine.AddIntentBasedTransition(toIdle);

                _stateMachine.AddAutonomicTransition(fallToNeutral);
                _stateMachine.AddAutonomicTransition(toFall);

                _stateMachine.OnTransitionedAutonomously = () => _context.AutonomicTransitionStream.OnNext(_context.State);

                _context.AnyRelevantChange
                    .Select(_ => new MovementSnapshot(_context.State, _context.Speed, _context.JumpStage))
                    .DistinctUntilChanged()
                    .Subscribe(snapshotStream.OnNext)
                    .AddTo(_disposables);

                _context.AutonomicTransitionStream
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

                if (Mathf.Approximately(action.Direction.magnitude, 1))
                {
                    var original = _characterorientator.localScale;
                    _characterorientator.localScale = new Vector3(original.x, original.y, action.Direction.x);
                }
            }

            public void HandleRootMotion(RootMotionFrame rootMotion)
            {
                _context.RootMotionDeltaPosition = rootMotion.DeltaPosition;
            }

            public void UpdateMover(float deltaTime)
            {
                _stateMachine.Update();
            }

            public bool IsGrounded()
            {
                foreach (var checkPoint in _groundCheckPoints)
                {
                    return Physics.OverlapSphere(checkPoint.position, _groundCheckDistance, _groundLayer).Length > 0;
                }

                return false;
            }

            [System.Serializable]
            public class Context
            {
                public BehaviorSubject<MovementType> AutonomicTransitionStream = new(MovementType.Idle);

                public Vector2 MoveInput;
                public Vector2 FacingDirection;
                public Vector3 RootMotionDeltaPosition;
                public Quaternion LogicalFacing;
                public Rigidbody Rb;
                public float MaxMoveSpeed;
                public float Acceleration;
                public float JumpHeight;
                public float JumpTimeToPeak;


                private MovementType _state = MovementType.Idle;
                public MovementType State
                {
                    get => _state;
                    set
                    {
                        if (_state == value) return;
                        _state = value;
                    }
                }

                private float _speed;
                public float Speed
                {
                    get => _speed;
                    set
                    {
                        if (Mathf.Approximately(_speed, value)) return;
                        _speed = value;
                    }
                }

                private int _jumpStage;
                public int JumpStage
                {
                    get => _jumpStage;
                    set
                    {
                        if (_jumpStage == value) return;
                        _jumpStage = value;
                    }
                }

                public void SubmitChange()
                {
                    _changedSubject.OnNext(Unit.Default);
                }

                private readonly Subject<Unit> _changedSubject = new();
                public Observable<Unit> AnyRelevantChange => _changedSubject;
            }
        }
    }
}