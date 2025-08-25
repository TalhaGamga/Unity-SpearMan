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

            private Transform[] _groundCheckPoints;
            private float _groundCheckDistance;
            private LayerMask _groundLayer;
            private Transform _characterorientator;
            private Subject<Unit> _submitSnapshotStream = new();
            private BehaviorSubject<MovementType> _submitTransitionStream = new(MovementType.Idle);

            private const float MinSqrDelta = 1e-8f;
            public void Init(IMovementManager movementManager, Subject<MovementSnapshot> snapshotStream, Subject<MovementTransition> transitionStream)
            {
                _stateMachine = new StateMachine<MovementType>();
                _manager = movementManager;
                _groundCheckPoints = _manager.GroundCheckPoints;
                _groundCheckDistance = _manager.GroundCheckDistance;
                _groundLayer = _manager.GroundLayer;
                _characterorientator = _manager.CharacterOrientator;

                _stateMachine.OnTransitionedAutonomously.AddListener(submitAutonomicStateTransition);

                _submitSnapshotStream.
                    Select(_ => new MovementSnapshot(_context.State, _context.MovementBlend, _context.JumpStage))
                    .DistinctUntilChanged()
                    .Subscribe(snapshotStream.OnNext)
                    .AddTo(_disposables);

                _submitTransitionStream
                    .Pairwise()
                    .Subscribe(pair =>
                    {
                        transitionStream.OnNext(new MovementTransition(pair.Previous, pair.Current));
                    }
                    ).AddTo(_disposables);

                IState moveState = new ConcreteState();
                IState idleState = new ConcreteState();
                IState fallState = new ConcreteState();
                IState neutralState = new ConcreteState();
                IState jumpState = new ConcreteState();

                moveState.OnEnter.AddListener(() =>
                {
                    setContextState(MovementType.Move);
                    constraintRbAxisY(true);
                    submitSnapshot();
                });

                idleState.OnEnter.AddListener(() =>
                {
                    setContextState(MovementType.Idle);
                    submitSnapshot();
                });

                fallState.OnEnter.AddListener(() =>
                {
                    setContextState(MovementType.Fall);
                    submitSnapshot();
                });


                moveState.OnExit.AddListener(() =>
                {
                    constraintRbAxisY(false);
                });

                fallState.OnExit.AddListener(() => _context.VerticalVelocity = 0);


                neutralState.OnEnter.AddListener(() =>
                {
                    setContextState(MovementType.Neutral);
                    submitSnapshot();
                });

                jumpState.OnEnter.AddListener(() =>
                {
                    setContextState(MovementType.Jump);
                    jump();
                    submitSnapshot();
                });


                moveState.OnUpdate.AddListener(() =>
                {
                    setCharacterOrientator();
                    blendSpeed();
                    applyRootMotionAsVelocity();
                    submitSnapshot();
                });

                idleState.OnUpdate.AddListener(() =>
                {
                    blendSpeed();
                    applyRootMotionAsVelocity();
                    submitSnapshot();
                });

                jumpState.OnUpdate.AddListener(() =>
                {
                    setCharacterOrientator();
                    blendSpeed();
                    handleGravity();
                    handleAirborneMovement();
                });

                fallState.OnUpdate.AddListener(() =>
                {
                    setCharacterOrientator();
                    blendSpeed();
                    handleGravity();
                    handleAirborneMovement();
                });

                var toMove = new StateTransition<MovementType>(MovementType.None, MovementType.Move, moveState, () => Debug.Log("Transitioning to Move"));
                var toIdle = new StateTransition<MovementType>(MovementType.None, MovementType.Idle, idleState, () => Debug.Log("Transitioning to Idle"));
                var toFall = new StateTransition<MovementType>(MovementType.None, MovementType.Fall, fallState, () => !isGrounded() && !_context.State.Equals(MovementType.Jump), () => Debug.Log("Transitioning to Fall"));
                var toJump = new StateTransition<MovementType>(MovementType.None, MovementType.Jump, jumpState, () => Debug.Log("Transitioning To Jump"));
                var jumpToFall = new StateTransition<MovementType>(MovementType.Jump, MovementType.Fall, fallState, () => _context.Rb.linearVelocity.y < 0, () => Debug.Log("Transitioning to fall from jump"));
                var fallToNeutral = new StateTransition<MovementType>(MovementType.Fall, MovementType.Neutral, neutralState, () => isGrounded(), () => Debug.Log("Transitioning to Neutral"));

                _stateMachine.AddIntentBasedTransition(toMove);
                _stateMachine.AddIntentBasedTransition(toIdle);
                _stateMachine.AddIntentBasedTransition(toJump);

                _stateMachine.AddAutonomicTransition(fallToNeutral);
                _stateMachine.AddAutonomicTransition(toFall);
                _stateMachine.AddAutonomicTransition(jumpToFall);

                _stateMachine.SetState(MovementType.Idle);

                _context.Gravity = (2f * _context.JumpHeight) / (Mathf.Pow(_context.JumpTimeToPeak, 2));

            }

            public void End()
            {
                _disposables?.Dispose();
                _disposables = null;
            }

            public void HandleAction(MovementAction action)
            {
                _context.MoveInput = action.Direction;

                if (action.ActionType == MovementType.None) return;

                _stateMachine.SetState(action.ActionType);
            }

            public void HandleRootMotion(RootMotionFrame rootMotion)
            {
                _context.RootMotionDeltaPosition = rootMotion.DeltaPosition;
            }

            public void UpdateMover(float deltaTime)
            {
                _stateMachine.Update();
            }

            private bool isGrounded()
            {
                foreach (var checkPoint in _groundCheckPoints)
                {
                    return Physics.OverlapSphere(checkPoint.position, _groundCheckDistance, _groundLayer).Length > 0;
                }

                return false;
            }

            private void setContextState(MovementType movementType)
            {
                _context.State = movementType;
            }

            private void submitSnapshot()
            {
                _submitSnapshotStream.OnNext(Unit.Default);
            }

            private void submitAutonomicStateTransition()
            {
                _submitTransitionStream.OnNext(_context.State);
            }

            private void blendSpeed()
            {
                float desiredBlend = _context.MoveInput.magnitude;
                _context.MovementBlend = Mathf.MoveTowards(_context.MovementBlend, desiredBlend, _context.BlendAcceleration * Time.deltaTime);
            }

            private void applyRootMotionAsVelocity()
            {
                Vector3 delta = _context.RootMotionDeltaPosition;

                Vector3 velocity = new Vector3(delta.x, delta.y, delta.z) / Time.deltaTime;

                _context.Rb.linearVelocity = new Vector3(velocity.x, velocity.y, velocity.z);

                _context.RootMotionDeltaPosition = Vector3.zero;
            }

            private void constraintRbAxisY(bool isAllowed)
            {
                _context.Rb.constraints = isAllowed
                    ? RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY   // lock Y + all rotations
                    : RigidbodyConstraints.FreezeRotation;
            }

            private void jump()
            {
                _context.VerticalVelocity = _context.Gravity * _context.JumpTimeToPeak;
            }

            private void handleAirborneMovement()
            {
                _context.Rb.linearVelocity = new Vector3(0, _context.VerticalVelocity, _context.MoveInput.x * _context.AirborneMovementSpeed);
            }

            private void handleGravity()
            {
                _context.VerticalVelocity -= _context.Gravity * Time.deltaTime;
            }

            private void setCharacterOrientator()
            {
                if (Mathf.Approximately(_context.MoveInput.magnitude, 1))
                {
                    var original = _characterorientator.localScale;
                    _characterorientator.localScale = new Vector3(original.x, original.y, _context.MoveInput.x);
                }
            }


            [System.Serializable]
            public class Context
            {
                public MovementType State;
                public Vector2 MoveInput;
                public Vector3 RootMotionDeltaPosition;
                public Rigidbody Rb;
                public float BlendAcceleration;
                public float MovementBlend;
                public int JumpStage;
                public float JumpHeight;
                public float AirborneMovementSpeed;
                public float JumpTimeToPeak;
                [HideInInspector] public float Gravity;
                [HideInInspector] public float VerticalVelocity;
            }
        }
    }
}