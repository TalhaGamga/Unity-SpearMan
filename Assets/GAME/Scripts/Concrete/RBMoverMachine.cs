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
        public MovementType CurrentType => _context.State;

        [SerializeField] private Context _context;
        [SerializeField] private StateMachine<MovementType> _stateMachine;

        private CompositeDisposable _disposables = new();
        private IMovementManager _manager;

        private Transform[] _groundCheckPoints;
        private float _groundCheckDistance;

        private Transform _characterOrientator;
        private Subject<Unit> _snapshotStreamer = new();
        private BehaviorSubject<MovementType> _transitionStreamer = new(MovementType.Idle);

        private const float MinSqrDelta = 1e-8f;
        private const float GroundedUpwardVelocityThreshold = 0.1f;

        private bool _isForcedMotionActive;
        private ImpactData _impact;
        private float _deltaTime;
        private float _physicsDeltaTime;
        private Vector3 _rootMotionVelocity;

        public void Init(IMovementManager movementManager, Subject<MovementSnapshot> snapshotStream, Subject<MovementTransition> transitionStream)
        {
            _stateMachine = new StateMachine<MovementType>();
            _manager = movementManager;
            _groundCheckPoints = _manager.GroundCheckPoints;
            _groundCheckDistance = _manager.GroundCheckDistance;
            _context.PlatformLayer = _manager.GroundLayer;
            _characterOrientator = _manager.CharacterOrientator;

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
            IState launchedState = new ConcreteState();
            IState forcedFallState = new ConcreteState();
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
                settleGroundedMotion();
                submitSnapshot();
            });

            idleState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.Idle);
                settleGroundedMotion();
                submitSnapshot();
            });

            launchedState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.Launched);
                setMovementConstraints(false);
                configureImpactMotion(_impact);
                submitSnapshot();
            });

            forcedFallState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.ForcedFall);
                setMovementConstraints(false);
                submitSnapshot();
            });

            fallState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.Fall);
                setMovementConstraints(false);
                setHorizontalSpeed(_context.AirborneMovementSpeed);
                submitSnapshot();
            });

            neutralState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.Neutral);
                settleGroundedMotion();
                submitSnapshot();
            });

            jumpState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.Jump);
                setMovementConstraints(false);
                submitSnapshot();
            });

            dashState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.Dash);
                setMovementConstraints(true);
                dash();
                submitSnapshot();
            });

            stabState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.Stab);
                setMovementConstraints(true);
                _context.Rb.linearVelocity = Vector3.zero;
                submitSnapshot();
            });
            #endregion

            #region OnExit
            dashState.OnExit.AddListener(() =>
            {
                setMovementConstraints(false);
            });

            moveState.OnExit.AddListener(() =>
            {
                setMovementConstraints(false);
            });

            jumpState.OnExit.AddListener(() =>
            {
                setComboType(MovementComboType.None);
            });

            forcedFallState.OnExit.AddListener(() =>
            {
                _context.VerticalVelocity = 0;
                resetForcedMotion();
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
                submitSnapshot();
            });

            idleState.OnUpdate.AddListener(() =>
            {
                blendSpeed();
                submitSnapshot();
            });

            jumpState.OnUpdate.AddListener(() =>
            {
                setCharacterOrientator();
                blendSpeed();
            });

            launchedState.OnUpdate.AddListener(() =>
            {
                setCharacterOrientator();
                blendSpeed();
            });

            forcedFallState.OnUpdate.AddListener(() =>
            {
                setCharacterOrientator();
                blendSpeed();
            });

            fallState.OnUpdate.AddListener(() =>
            {
                setCharacterOrientator();
                blendSpeed();
            });

            stabState.OnUpdate.AddListener(() =>
            {
                if (!_context.IsStabStarted)
                {
                    setCharacterOrientator();
                }

                Vector3 stabDir = findStabDirection();

                if (_context.IsStabbing && !_context.IsStabStarted)
                {
                    _context.IsStabStarted = true;

                    Vector3 targetDirection = stabDir;
                    Vector3 targetPoint = findStabPoint(targetDirection);
                    stab(targetPoint);
                    setRotationInStabbing(targetPoint);
                }
            });
            #endregion

            #region OnPhysicsUpdate
            moveState.OnPhysicsUpdate.AddListener(() =>
            {
                applyRootMotionAsVelocity();
            });

            idleState.OnPhysicsUpdate.AddListener(() =>
            {
                applyRootMotionAsVelocity();
            });

            jumpState.OnPhysicsUpdate.AddListener(() =>
            {
                handleGravity();
                handleAirborneMovement();
            });

            launchedState.OnPhysicsUpdate.AddListener(() =>
            {
                handleGravity();
                handleAirborneMovement();
            });

            forcedFallState.OnPhysicsUpdate.AddListener(() =>
            {
                handleGravity();
                handleAirborneMovement();
            });

            fallState.OnPhysicsUpdate.AddListener(() =>
            {
                handleGravity();
                handleAirborneMovement();
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
            var toLaunched = new StateTransition<MovementType>(null, launchedState, MovementType.Launched, onTransition: () => Debug.Log("Transitioning to Launched"));
            var relaunch = new StateTransition<MovementType>(launchedState, launchedState, MovementType.Launched, onTransition: () => Debug.Log("Re-entering Launched"));
            var toFall = new StateTransition<MovementType>(null, fallState, MovementType.Fall, () => !isGrounded() && !_context.State.Equals(MovementType.Jump) && !_context.State.Equals(MovementType.Launched) && !_context.State.Equals(MovementType.ForcedFall) && !_context.State.Equals(MovementType.Dash) && !_context.State.Equals(MovementType.Stab), () => Debug.Log("Transitioning to Fall"));
            var toJump = new StateTransition<MovementType>(null, jumpState, MovementType.Jump, onTransition: () =>
            {
                Debug.Log("Transitioning To Jump");
                setVerticalVelocity(calculateJumpVelocity());
                setHorizontalSpeed(_context.AirborneMovementSpeed);
            });

            var jumpToFall = new StateTransition<MovementType>(jumpState, fallState, MovementType.Fall, () => _context.Rb.linearVelocity.y < 0, () => Debug.Log("Transitioning to fall from jump"));
            var launchedToForcedFall = new StateTransition<MovementType>(launchedState, forcedFallState, MovementType.ForcedFall, () => _context.VerticalVelocity <= 0f, () => Debug.Log("Transitioning to forced fall from launch"));
            var forcedFallToNeutral = new StateTransition<MovementType>(forcedFallState, neutralState, MovementType.Neutral,
                () => isGrounded(),
                () => Debug.Log("Transitioning to Neutral from forced fall"));
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
            _stateMachine.AddIntentBasedTransition(relaunch);
            _stateMachine.AddIntentBasedTransition(toLaunched);
            _stateMachine.AddIntentBasedTransition(toJump);
            _stateMachine.AddIntentBasedTransition(dashToJump);
            _stateMachine.AddIntentBasedTransition(toDash);
            _stateMachine.AddIntentBasedTransition(toStab);

            _stateMachine.AddAutonomicTransition(fallToNeutral);
            _stateMachine.AddAutonomicTransition(stabToNeutral);
            _stateMachine.AddAutonomicTransition(toFall);
            _stateMachine.AddAutonomicTransition(jumpToFall);
            _stateMachine.AddAutonomicTransition(launchedToForcedFall);
            _stateMachine.AddAutonomicTransition(forcedFallToNeutral);
            _stateMachine.AddAutonomicTransition(dashToNeutral);

            setContextGravity();
            _stateMachine.SetState(MovementType.Idle);
        }

        public void End()
        {
            _disposables?.Dispose();
            _disposables = null;
        }

        public void HandleAction(MovementAction action)
        {
            if (_isForcedMotionActive)
                return;

            _context.MoveInput = action.Direction;

            if (action.ActionType == MovementType.None)
                return;

            _stateMachine.SetState(action.ActionType);
        }

        public void HandleImpact(ImpactData impact)
        {
            if (impact.Force <= 0f ||
                impact.Direction.sqrMagnitude <= MinSqrDelta)
            {
                return;
            }

            _impact = impact;
            _stateMachine.SetState(MovementType.Launched);
        }

        public void HandleRootMotion(RootMotionFrame rootMotion)
        {
            _context.RootMotionDeltaPosition = rootMotion.DeltaPosition;

            if (Time.deltaTime > Mathf.Epsilon)
            {
                _rootMotionVelocity =
                    rootMotion.DeltaPosition / Time.deltaTime;
            }
        }

        public void UpdateMover(float deltaTime)
        {
            _deltaTime = deltaTime;
            _stateMachine.Update();
        }

        public void PhysicsUpdateMover(float deltaTime)
        {
            _physicsDeltaTime = deltaTime;
            _stateMachine.PhysicsUpdate();
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
            if (_context.VerticalVelocity >
                GroundedUpwardVelocityThreshold)
            {
                return false;
            }

            foreach (var checkPoint in _groundCheckPoints)
            {
                if (checkPoint != null && Physics.CheckSphere(
                    checkPoint.position,
                    _groundCheckDistance,
                    _context.PlatformLayer,
                    QueryTriggerInteraction.Ignore
                ))
                {
                    return true;
                }
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
            _context.MovementBlend = Mathf.MoveTowards(
                _context.MovementBlend,
                desiredBlend,
                _context.BlendAcceleration * _deltaTime
            );
        }

        private void applyRootMotionAsVelocity()
        {
            if (_physicsDeltaTime <= Mathf.Epsilon)
                return;

            Vector3 velocity = PhysicsAxesUtility.Project(
                _rootMotionVelocity,
                PhysicsAxes.Z
            );

            _context.Rb.linearVelocity = velocity;

            _context.RootMotionDeltaPosition = Vector3.zero;
        }

        private void stab(Vector3 stabPoint)
        {
            Vector3 planarPoint = PhysicsAxesUtility.ConstrainPoint(
                stabPoint,
                _context.MoverTransform.position,
                PhysicsAxes.YZ
            );
            _context.MoverTransform.DOMove(planarPoint, _context.StabDuration).SetEase(_context.StabEase);
        }

        private Vector3 findStabDirection()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, _context.CharacterMouseSensor))
            {
                Vector3 baseDir = Vector3.down;
                Vector3 dir = PhysicsAxesUtility.Direction(hit.point - _characterOrientator.position, PhysicsAxes.YZ);

                if (dir == Vector3.zero)
                    return Vector3.zero;

                float angle = Vector3.SignedAngle(baseDir, dir, Vector3.right);

                if (angle > 0)
                    angle = Mathf.Clamp(angle, _context.StabMinAngle, _context.StabMaxAngle);
                else
                    angle = Mathf.Clamp(angle, -_context.StabMaxAngle, -_context.StabMinAngle);

                Quaternion rot = Quaternion.AngleAxis(angle, Vector3.right);
                Vector3 clampedDir = rot * baseDir;

                Debug.DrawLine(_characterOrientator.position, hit.point, Color.yellow);     // raw mouse ray hit
                Debug.DrawRay(_characterOrientator.position, baseDir * 2f, Color.blue);     // base direction
                Debug.DrawRay(_characterOrientator.position, dir * 2f, Color.red);          // original dir
                Debug.DrawRay(_characterOrientator.position, clampedDir * 2f, Color.green); // clamped dir

                return clampedDir.normalized;
            }
            return Vector3.zero;
        }

        private Vector3 findStabPoint(Vector3 direction)
        {
            Vector3 planarDirection = PhysicsAxesUtility.Direction(direction, PhysicsAxes.YZ);

            if (planarDirection == Vector3.zero)
                return _context.MoverTransform.position;

            Vector3 origin = _context.MoverTransform.position;
            Ray ray = new Ray(origin, planarDirection);

            if (Physics.Raycast(ray, out RaycastHit hit, _context.StabRange, _context.PlatformLayer))
                return PhysicsAxesUtility.ConstrainPoint(hit.point, origin, PhysicsAxes.YZ);

            return ray.origin + ray.direction * _context.StabRange;
        }

        private void setRotationInStabbing(Vector3 stabPoint)
        {
            float deltaZ = stabPoint.z - _context.MoverTransform.position.z;

            Vector3 targetEuler = (deltaZ > 0f)
                ? new Vector3(0f, 0f, 0f)
                : new Vector3(0f, 180f, 0f);

            _characterOrientator.DOLocalRotate(
                targetEuler,
                1f / _context.FaceTurnSpeedInDegree,
                RotateMode.Fast
            );
        }

        private void settleGroundedMotion()
        {
            snapToGroundSurface();

            _context.VerticalVelocity = 0f;
            _context.Rb.linearVelocity = new Vector3(
                0f,
                0f,
                _context.Rb.linearVelocity.z
            );
            _context.RootMotionDeltaPosition = Vector3.zero;
            setMovementConstraints(true);
        }

        private void snapToGroundSurface()
        {
            float probeDistance = Mathf.Max(
                _context.GroundSnapDistance,
                _groundCheckDistance
            );
            bool foundGround = false;
            float highestCorrection = float.NegativeInfinity;

            foreach (Transform checkPoint in _groundCheckPoints)
            {
                if (checkPoint == null)
                    continue;

                Vector3 origin = checkPoint.position +
                    Vector3.up * probeDistance;

                if (!Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    probeDistance * 2f,
                    _context.PlatformLayer,
                    QueryTriggerInteraction.Ignore
                ))
                {
                    continue;
                }

                float correction = hit.point.y - checkPoint.position.y;
                highestCorrection = Mathf.Max(
                    highestCorrection,
                    correction
                );
                foundGround = true;
            }

            if (!foundGround)
                return;

            Vector3 position = _context.Rb.position;
            position.y += highestCorrection;
            _context.Rb.position = position;
        }

        private void setMovementConstraints(bool freezeVerticalPosition)
        {
            RigidbodyConstraints constraints =
                RigidbodyConstraints.FreezeRotation |
                RigidbodyConstraints.FreezePositionX;

            if (freezeVerticalPosition)
                constraints |= RigidbodyConstraints.FreezePositionY;

            _context.Rb.constraints = constraints;
        }

        private void dash()
        {
            _context.Rb.linearVelocity = new Vector3(
                0f,
                0f,
                _context.DashSpeed * _context.LastFaceX
            );
            _context.VerticalVelocity = 0;
        }

        private void configureImpactMotion(ImpactData impact)
        {
            Vector3 direction = PhysicsAxesUtility.Direction(
                impact.Direction,
                PhysicsAxes.YZ
            );

            _isForcedMotionActive = true;
            _context.MoveInput = new Vector2(direction.z, 0f);
            setHorizontalSpeed(impact.Force);
            setVerticalVelocity(direction.y * impact.Force);
        }

        private void resetForcedMotion()
        {
            _isForcedMotionActive = false;
        }

        private void handleAirborneMovement()
        {
            _context.Rb.linearVelocity = new Vector3(
                0f,
                _context.VerticalVelocity,
                _context.HorizontalVelocity
            );
        }

        private void handleGravity()
        {
            if (_context.VerticalVelocity <= 0f && isGrounded())
            {
                _context.VerticalVelocity = 0f;
                return;
            }

            _context.VerticalVelocity -=
                _context.Gravity * _physicsDeltaTime;
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

            _characterOrientator.localRotation =
                Quaternion.RotateTowards(
                    _characterOrientator.localRotation,
                    targetLocalRot,
                    _context.FaceTurnSpeedInDegree * _deltaTime
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

            public LayerMask PlatformLayer;
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
            [Min(0f)] public float GroundSnapDistance = 0.25f;
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
            public float StabRange = 10f;

            public float StabMinAngle = 30;
            public float StabMaxAngle = 80;
            public LayerMask CharacterMouseSensor;
            [HideInInspector] public float Gravity;
            [HideInInspector] public float VerticalVelocity;
            [HideInInspector] public float HorizontalVelocity => MoveInput.x * HorizontalSpeed;
            [HideInInspector] public float HorizontalSpeed;
        }
    }
}