using DevVorpian;
using DG.Tweening;
using Movement.State;
using R3;
using UnityEngine;

namespace Movement.Mover
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

        private Transform _characterorientator;
        private Subject<Unit> _snapshotStreamer = new();
        private BehaviorSubject<MovementType> _transitionStreamer = new(MovementType.Idle);

        private const float MinSqrDelta = 1e-8f;
        public void Init(IMovementManager movementManager, Subject<MovementSnapshot> snapshotStream, Subject<MovementTransition> transitionStream)
        {
            _stateMachine = new StateMachine<MovementType>();
            _manager = movementManager;
            _manager = movementManager;
            _groundCheckPoints = _manager.GroundCheckPoints;
            _groundCheckDistance = _manager.GroundCheckDistance;
            _context.GroundLayer = _manager.GroundLayer;
            _characterorientator = _manager.CharacterOrientator;

            _stateMachine.OnTransitionedAutonomously.AddListener(submitAutonomicStateTransition);

            _snapshotStreamer
                .Select(_ => new MovementSnapshot(_context.State, _context.ComboType, _context.MovementBlend, _context.JumpRight, isGrounded()))
                .DistinctUntilChanged()
                .Subscribe(snapshotStream.OnNext)
                .AddTo(_disposables);

            _transitionStreamer
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
            IState doubleJumpState = new ConcreteState();
            IState dashState = new ConcreteState();
            IState stabState = new ConcreteState();

            #region OnEnter
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
                constraintRbAxisY(false);
                setHorizontalSpeed(_context.AirborneMovementSpeed);
                submitSnapshot();
            });

            neutralState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.Neutral);
                submitSnapshot();
            });

            jumpState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.Jump);

                submitSnapshot();
            });

            dashState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.Dash);
                constraintRbAxisY(true);
                dash();
                submitSnapshot();
            });

            stabState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.Stab);
                constraintRbAxisY(true);
                _context.Rb.linearVelocity = Vector3.zero;
                submitSnapshot();
            });
            #endregion

            #region OnExit
            dashState.OnExit.AddListener(() =>
            {
                constraintRbAxisY(false);
            });

            moveState.OnExit.AddListener(() =>
            {
                constraintRbAxisY(false);
            });

            jumpState.OnExit.AddListener(() =>
            {
                setComboType(MovementComboType.None);
            });

            fallState.OnExit.AddListener(() =>
            {
                _context.VerticalVelocity = 0;
            });
            #endregion

            #region OnUpdate
            idleState.OnUpdate.AddListener(() =>
            {
                setCharacterOrientator();
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

            stabState.OnUpdate.AddListener(() =>
            {
                if (_context.IsStabbing && !_context.IsStabStarted)
                {
                    stab();
                    _context.IsStabStarted = true;
                }
            });
            #endregion

            #region OnExit
            stabState.OnExit.AddListener(() =>
            {
                _context.IsStabStarted = false;
                _context.IsStabbing = false;
            });
            #endregion

            var toIdle = new StateTransition<MovementType>(null, idleState, MovementType.Idle, onTransition: () => Debug.Log("Transitioning to Idle"));
            var toMove = new StateTransition<MovementType>(null, moveState, MovementType.Move, onTransition: () => Debug.Log("Transitioning to Move"));
            var toFall = new StateTransition<MovementType>(null, fallState, MovementType.Fall, () => !isGrounded() && !_context.State.Equals(MovementType.Jump) && !_context.State.Equals(MovementType.Dash) && !_context.State.Equals(MovementType.Stab), () => Debug.Log("Transitioning to Fall"));
            var toJump = new StateTransition<MovementType>(null, jumpState, MovementType.Jump, onTransition: () =>
            {
                Debug.Log("Transitioning To Jump");
                setVerticalVelocity(calculateJumpVelocity());
                setHorizontalSpeed(_context.AirborneMovementSpeed);
            });

            var jumpToFall = new StateTransition<MovementType>(jumpState, fallState, MovementType.Fall, () => _context.Rb.linearVelocity.y < 0, () => Debug.Log("Transitioning to fall from jump"));
            var fallToNeutral = new StateTransition<MovementType>(fallState, neutralState, MovementType.Neutral, () => isGrounded(), () => Debug.Log("Transitioning to Neutral"));
            var dashToJump = new StateTransition<MovementType>(dashState, jumpState, MovementType.Jump, onTransition: () =>
            {
                Debug.Log("Transitioning to Jump from dash");
                setComboType(MovementComboType.DashingJump);
                setVerticalVelocity(calculateJumpVelocity() / 1.5f);
                setHorizontalSpeed(_context.DashSpeed);
            });

            var toDash = new StateTransition<MovementType>(null, dashState, MovementType.Dash, onTransition: () => { Debug.Log("Transitioning To Dash"); _context.IsDashEnded = false; });
            var dashToNeutral = new StateTransition<MovementType>(dashState, neutralState, MovementType.Neutral, () => _context.IsDashEnded, () => Debug.Log("Transitioning to Neutral"));
            var toStab = new StateTransition<MovementType>(null, stabState, MovementType.Stab, onTransition: () =>
            {
                Debug.Log("Transitioning to Stab");
                _context.IsStabEnded = false;
            });
            var stabToNeutral = new StateTransition<MovementType>(stabState, neutralState, MovementType.Neutral, condition: () => _context.IsStabEnded, onTransition: () => Debug.Log("Transitioning to Neutral from Stab"));

            _stateMachine.AddIntentBasedTransition(toIdle);
            _stateMachine.AddIntentBasedTransition(toMove);
            _stateMachine.AddIntentBasedTransition(toJump);
            _stateMachine.AddIntentBasedTransition(dashToJump);
            _stateMachine.AddIntentBasedTransition(toDash);
            _stateMachine.AddIntentBasedTransition(toStab);

            _stateMachine.AddAutonomicTransition(fallToNeutral);
            _stateMachine.AddAutonomicTransition(stabToNeutral);
            _stateMachine.AddAutonomicTransition(toFall);
            _stateMachine.AddAutonomicTransition(jumpToFall);
            _stateMachine.AddAutonomicTransition(dashToNeutral);

            _stateMachine.SetState(MovementType.Idle);

            setContextGravity();
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

        public void OnAnimationFrame(MovementAnimationFrame animationFrame)
        {
            if (animationFrame.Action == "Dash")
            {
                if (animationFrame.EventKey == "DashEnded")
                {
                    _context.IsDashEnded = true;
                }
            }

            if (animationFrame.EventKey == "StabEnded")
            {
                _context.IsStabEnded = true;
            }

            if (animationFrame.EventKey == "StabStarted")
            {
                _context.IsStabbing = true;
            }
        }

        private bool isGrounded()
        {
            foreach (var checkPoint in _groundCheckPoints)
            {
                return Physics.OverlapSphere(checkPoint.position, _groundCheckDistance, _context.GroundLayer).Length > 0;
            }

            return false;
        }

        private void setContextState(MovementType movementType)
        {
            _context.State = movementType;
        }

        private void submitSnapshot()
        {
            _snapshotStreamer.OnNext(Unit.Default);
        }

        private void submitAutonomicStateTransition()
        {
            _transitionStreamer.OnNext(_context.State);
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

        private void stab()
        {
            _context.MoverTransform.DOMove(_context.StabPoint.position, _context.StabDuration).SetEase(_context.StabEase);
        }

        private void constraintRbAxisY(bool isAllowed)
        {
            _context.Rb.constraints = isAllowed
                ? RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY   // lock Y + all rotations
                : RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionX;
        }
        private void dash()
        {
            _context.Rb.linearVelocity = new Vector3(0, 0, _context.DashSpeed * _context.LastFaceX);
            _context.VerticalVelocity = 0;
        }

        private void handleAirborneMovement()
        {
            _context.Rb.linearVelocity = new Vector3(0, _context.VerticalVelocity, _context.HorizontalVelocity);
        }

        private void handleGravity()
        {
            _context.VerticalVelocity -= _context.Gravity * Time.deltaTime;
        }

        private void setHorizontalSpeed(float speed)
        {
            _context.HorizontalSpeed = speed;
        }

        private void setVerticalVelocity(float velocity)
        {
            _context.VerticalVelocity = velocity;
        }

        private float calculateJumpVelocity()
        {
            return _context.Gravity * _context.JumpTimeToPeak;
        }

        private void setCharacterOrientator()
        {
            float x = _context.MoveInput.x;

            if (Mathf.Abs(x) > _context.FaceDeadzone)
            {
                float sign = Mathf.Sign(x);
                if (sign != 0f) _context.LastFaceX = (int)sign;
            }

            float target = _context.LastFaceX;

            Quaternion targetLocalRot = (target > 0f)
                ? Quaternion.Euler(0f, 0f, 0f)
                : Quaternion.Euler(0f, 180f, 0f);

            _characterorientator.localRotation =
                Quaternion.RotateTowards(
                    _characterorientator.localRotation,
                    targetLocalRot,
                    _context.FaceTurnSpeedInDegree * Time.deltaTime
                );
        }

        private void setJumpStage(int stage)
        {
            _context.JumpRight = stage;
        }

        private void setContextGravity()
        {
            _context.Gravity = (2f * _context.JumpHeight) / (Mathf.Pow(_context.JumpTimeToPeak, 2));
        }

        private void setComboType(MovementComboType comboType)
        {
            _context.ComboType = comboType;
        }

        [System.Serializable]
        public class Context
        {
            public MovementType State;
            public MovementComboType ComboType;
            public Transform StabPoint;

            public LayerMask GroundLayer;
            public Vector2 MoveInput;
            public Vector3 RootMotionDeltaPosition;
            public Rigidbody Rb;
            public Transform MoverTransform;
            public float BlendAcceleration;
            public float MovementBlend;
            public int JumpRight;
            public float JumpHeight;
            public float AirborneMovementSpeed;
            public float JumpTimeToPeak;
            public float StabDuration;
            public bool IsStabStarted;
            public bool IsStabbing;
            public Ease StabEase;

            public float FaceTurnSpeedInDegree = 720;
            public float FaceDeadzone = 0.05f;
            public int LastFaceX = 1;

            public float DashSpeed = 10f;
            public bool IsDashEnded = false;
            public bool IsStabEnded = false;

            [HideInInspector] public float Gravity;
            [HideInInspector] public float VerticalVelocity;
            [HideInInspector] public float HorizontalVelocity => MoveInput.x * HorizontalSpeed;
            [HideInInspector] public float HorizontalSpeed;
        }
    }
}