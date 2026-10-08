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

        private Transform _characterOrientator;
        private Subject<Unit> _snapshotStreamer = new();
        private BehaviorSubject<MovementType> _transitionStreamer = new(MovementType.Idle);

        private const float MinSqrDelta = 1e-8f;
        private const float GroundedUpwardVelocityThreshold = 0.1f;
        private const float StabCollisionSkin = 0.03f;

        private bool _isForcedMotionActive;
        private ImpactData _impact;
        private PierceData _pierce;
        private Vector3 _pierceRootOffset;
        private float _pierceElapsed;
        private bool _pierceFollowing;
        private float _deltaTime;
        private float _physicsDeltaTime;

        private readonly Collider[] _stabAimCandidates = new Collider[24];

        /// <summary>
        /// Every feel number lives in the design asset. Nothing here authors a
        /// gravity or a launch velocity directly, so tweaking the asset at play
        /// time takes effect on the very next physics step.
        /// </summary>
        private MovementDesignSO design => _context.Design;

        public void Init(IMovementManager movementManager, Subject<MovementSnapshot> snapshotStream, Subject<MovementTransition> transitionStream)
        {
            _stateMachine = new StateMachine<MovementType>();
            _manager = movementManager;
            _context.PlatformLayer = _manager.GroundLayer;
            _characterOrientator = _manager.CharacterOrientator;
            configureBodyConstraints();

            if (_context.Design == null)
            {
                Debug.LogError(
                    "RBMoverMachine: no MovementDesignSO assigned. Traversal " +
                    "has no reference units to work from and will not move.");
                return;
            }

            _stateMachine.OnTransitionedAutonomously.AddListener(submitAutonomicStateTransition);

            _snapshotStreamer
                .Select(_ => new MovementSnapshot(_context.State, _context.ComboType, _context.MovementBlend, _context.JumpRight, isGrounded(), _context.IsAirJump))
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
            IState piercedState = new ConcreteState();
            IState fallState = new ConcreteState();
            IState neutralState = new ConcreteState();
            IState jumpState = new ConcreteState();
            IState dashState = new ConcreteState();
            IState stabState = new ConcreteState();

            #region OnEnter
            moveState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.Move);
                submitSnapshot();
            });

            idleState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.Idle);
                submitSnapshot();
            });

            launchedState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.Launched);
                configureImpactMotion(_impact);
                submitSnapshot();
            });

            forcedFallState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.ForcedFall);
                submitSnapshot();
            });

            piercedState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.Pierced);
                configurePierceMotion(_pierce);
                submitSnapshot();
            });

            fallState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.Fall);
                setHorizontalSpeed(airSpeed());
                seedAirMomentum();
                submitSnapshot();
            });

            neutralState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.Neutral);
                resetForcedMotion();
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
                dash();
                submitSnapshot();
            });

            stabState.OnEnter.AddListener(() =>
            {
                setContextState(MovementType.Stab);
                _context.Rb.linearVelocity = Vector3.zero;
                submitSnapshot();
            });
            #endregion

            #region OnExit
            jumpState.OnExit.AddListener(() =>
            {
                setComboType(MovementComboType.None);
            });

            forcedFallState.OnExit.AddListener(() =>
            {
                _context.VerticalVelocity = 0;
                resetForcedMotion();
            });

            piercedState.OnExit.AddListener(() =>
            {
                resetForcedMotion();
            });

            fallState.OnExit.AddListener(() =>
            {
                _context.VerticalVelocity = 0;
            });

            stabState.OnExit.AddListener(() =>
            {
                _context.IsStabStarted = false;
                _context.IsStabbing = false;
            });
            #endregion

            #region OnUpdate
            idleState.OnUpdate.AddListener(() =>
            {
                setCharacterOrientator();
                blendSpeed();
                submitSnapshot();
            });

            moveState.OnUpdate.AddListener(() =>
            {
                setCharacterOrientator();
                blendSpeed();
                submitSnapshot();
            });

            neutralState.OnUpdate.AddListener(() =>
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

            piercedState.OnUpdate.AddListener(() =>
            {
                setCharacterOrientator();
                submitSnapshot();
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

                if (_context.IsStabbing && !_context.IsStabStarted)
                {
                    _context.IsStabStarted = true;

                    Vector3 targetDirection = findStabDirection();
                    Vector3 targetPoint = findStabPoint(targetDirection);
                    stab(targetPoint);
                    setRotationInStabbing(targetPoint);
                }
            });
            #endregion

            #region OnPhysicsUpdate
            moveState.OnPhysicsUpdate.AddListener(() =>
            {
                maintainGroundedMotion();
                applyGroundedMovement();
            });

            idleState.OnPhysicsUpdate.AddListener(() =>
            {
                maintainGroundedMotion();
                applyGroundedMovement();
            });

            neutralState.OnPhysicsUpdate.AddListener(() =>
            {
                stopGroundedMotion();
            });

            jumpState.OnPhysicsUpdate.AddListener(() =>
            {
                applyJumpCut();
                simulateAirborneMotion(useAirControl: true);
            });

            launchedState.OnPhysicsUpdate.AddListener(() =>
            {
                simulateAirborneMotion(useAirControl: false);
            });

            forcedFallState.OnPhysicsUpdate.AddListener(() =>
            {
                simulateAirborneMotion(useAirControl: false);
            });

            piercedState.OnPhysicsUpdate.AddListener(applyPierceMotion);

            fallState.OnPhysicsUpdate.AddListener(() =>
            {
                simulateAirborneMotion(useAirControl: true);
            });
            #endregion

            // StateTransition logs on every transition unless given an action.
            // Transitions are evaluated in FixedUpdate, so silence the ones
            // that have nothing to do rather than flooding the console while
            // tuning.
            System.Action quiet = () => { };

            var toIdle = new StateTransition<MovementType>(null, idleState, MovementType.Idle, onTransition: quiet);
            var toMove = new StateTransition<MovementType>(null, moveState, MovementType.Move, onTransition: quiet);
            var toLaunched = new StateTransition<MovementType>(null, launchedState, MovementType.Launched, onTransition: quiet);
            var relaunch = new StateTransition<MovementType>(launchedState, launchedState, MovementType.Launched, onTransition: quiet);
            var toPierced = new StateTransition<MovementType>(null, piercedState, MovementType.Pierced, onTransition: quiet);
            var repierce = new StateTransition<MovementType>(piercedState, piercedState, MovementType.Pierced, onTransition: quiet);
            var toFall = new StateTransition<MovementType>(null, fallState, MovementType.Fall, () => !isGrounded() && !_context.State.Equals(MovementType.Jump) && !_context.State.Equals(MovementType.Launched) && !_context.State.Equals(MovementType.ForcedFall) && !_context.State.Equals(MovementType.Pierced) && !_context.State.Equals(MovementType.Dash) && !_context.State.Equals(MovementType.Stab), quiet);

            // Ground jump, coyote jump and jump-out-of-fall all arrive here.
            // canJump is the single gate: the mapper only states intent.
            var toJump = new StateTransition<MovementType>(
                null, jumpState, MovementType.Jump,
                condition: canJump,
                onTransition: performJump);

            // Self transition, because a null From never re-enters its own
            // state. This is the air jump.
            var airJump = new StateTransition<MovementType>(
                jumpState, jumpState, MovementType.Jump,
                condition: canJump,
                onTransition: performJump);

            var jumpToFall = new StateTransition<MovementType>(jumpState, fallState, MovementType.Fall, () => _context.Rb.linearVelocity.y < 0, quiet);

            // A normal landing goes straight to Idle, not through Neutral.
            // Neutral resets the movement blend to zero, which costs the player
            // a re-acceleration ramp on every single landing - fine as recovery
            // after a knockdown, a stall when you are just running and jumping.
            var jumpToIdle = new StateTransition<MovementType>(
                jumpState,
                idleState,
                MovementType.Idle,
                () => _context.VerticalVelocity <= 0f && isGrounded(),
                quiet);
            var launchedToForcedFall = new StateTransition<MovementType>(launchedState, forcedFallState, MovementType.ForcedFall, () => _context.VerticalVelocity <= 0f, quiet);
            var forcedFallToNeutral = new StateTransition<MovementType>(forcedFallState, neutralState, MovementType.Neutral,
                () => isGrounded(), quiet);
            var neutralToIdle = new StateTransition<MovementType>(
                neutralState,
                idleState,
                MovementType.Idle,
                () => isGrounded(),
                quiet);
            var fallToIdle = new StateTransition<MovementType>(fallState, idleState, MovementType.Idle, () => isGrounded(), quiet);

            var dashToJump = new StateTransition<MovementType>(
                dashState, jumpState, MovementType.Jump,
                condition: canJump,
                onTransition: () =>
                {
                    setComboType(MovementComboType.DashingJump);
                    performJump(design.DashJumpVelocity);
                    setHorizontalSpeed(dashSpeed());
                });

            var toDash = new StateTransition<MovementType>(null, dashState, MovementType.Dash, onTransition: () => _context.IsDashEnded = false);
            var dashToNeutral = new StateTransition<MovementType>(dashState, neutralState, MovementType.Neutral, () => _context.IsDashEnded, quiet);
            var toStab = new StateTransition<MovementType>(null, stabState, MovementType.Stab, onTransition: () => _context.IsStabEnded = false);
            var stabToNeutral = new StateTransition<MovementType>(stabState, neutralState, MovementType.Neutral, condition: () => _context.IsStabEnded, onTransition: quiet);

            _stateMachine.AddIntentBasedTransition(toIdle);
            _stateMachine.AddIntentBasedTransition(toMove);
            _stateMachine.AddIntentBasedTransition(relaunch);
            _stateMachine.AddIntentBasedTransition(toLaunched);
            _stateMachine.AddIntentBasedTransition(repierce);
            _stateMachine.AddIntentBasedTransition(toPierced);
            _stateMachine.AddIntentBasedTransition(airJump);
            _stateMachine.AddIntentBasedTransition(dashToJump);
            _stateMachine.AddIntentBasedTransition(toJump);
            _stateMachine.AddIntentBasedTransition(toDash);
            _stateMachine.AddIntentBasedTransition(toStab);

            _stateMachine.AddAutonomicTransition(fallToIdle);
            _stateMachine.AddAutonomicTransition(stabToNeutral);
            _stateMachine.AddAutonomicTransition(toFall);
            _stateMachine.AddAutonomicTransition(jumpToIdle);
            _stateMachine.AddAutonomicTransition(jumpToFall);
            _stateMachine.AddAutonomicTransition(launchedToForcedFall);
            _stateMachine.AddAutonomicTransition(forcedFallToNeutral);
            _stateMachine.AddAutonomicTransition(neutralToIdle);
            _stateMachine.AddAutonomicTransition(dashToNeutral);

            _stateMachine.SetState(MovementType.Idle);
        }

        public void End()
        {
            _disposables?.Dispose();
            _disposables = null;
        }

        public void HandleAction(MovementAction action)
        {
            // Jump bookkeeping runs even under forced motion, otherwise a
            // release during a knockdown is lost and the press stays latched
            // past recovery.
            if (action.JumpHold != JumpHoldState.Unchanged)
            {
                _context.JumpHold = action.JumpHold;

                if (action.JumpHold == JumpHoldState.Released)
                    _context.JumpPressLatched = false;
            }

            if (_isForcedMotionActive)
                return;

            _context.MoveInput = action.Direction;
            _context.Locomotion = action.Locomotion;

            if (action.ActionType == MovementType.None)
                return;

            if (action.ActionType == MovementType.Jump)
            {
                // One jump per press. Intents are re-evaluated whenever any
                // stream ticks, so without this latch a single press could be
                // replayed into the ground jump and the air jump back to back.
                if (_context.JumpPressLatched)
                    return;

                _context.JumpPressLatched = true;
                _context.JumpHold = JumpHoldState.Held;

                // The press is remembered even when it is illegal right now.
                // The buffer replays it the moment canJump opens up, which is
                // what makes an early press before landing feel responsive.
                _context.JumpBufferTimer =
                    design != null ? design.JumpBufferTime : 0f;
            }

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

        public void HandlePierce(PierceData pierce)
        {
            if (pierce.Source == null ||
                pierce.Direction.sqrMagnitude <= MinSqrDelta)
            {
                return;
            }

            _pierce = pierce;
            _stateMachine.SetState(MovementType.Pierced);
        }

        public void HandleRootMotion(RootMotionFrame rootMotion)
        {
            _context.RootMotionDeltaPosition +=
                rootMotion.DeltaPosition;
        }

        public void UpdateMover(float deltaTime)
        {
            _deltaTime = deltaTime;
            // Movement transitions depend on Rigidbody and ground-query state.
            // Evaluate them in the physics step so landing cannot be delayed by
            // a render frame.
            _stateMachine.Update(checkTransitions: false);
        }

        public void PhysicsUpdateMover(float deltaTime)
        {
            _physicsDeltaTime = deltaTime;

            if (design == null)
                return;

            tickTraversalTimers(deltaTime);
            replayBufferedJump();

            _stateMachine.PhysicsUpdate(checkTransitions: true);
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

        #region Traversal Budget

        /// <summary>
        /// Runs before the state machine every physics step so coyote time,
        /// the jump buffer and the jump budget stay correct regardless of which
        /// state happens to be active.
        /// </summary>
        private void tickTraversalTimers(float deltaTime)
        {
            if (isGrounded())
            {
                _context.CoyoteTimer = design.CoyoteTime;
                setJumpCount(0);
            }
            else if (_context.CoyoteTimer > 0f)
            {
                _context.CoyoteTimer -= deltaTime;

                if (_context.CoyoteTimer <= 0f && _context.JumpCount == 0)
                {
                    // Coyote window closed without a jump. Spend the ground
                    // jump so a late press buys an air jump, not a free
                    // full-power launch out of thin air.
                    setJumpCount(1);
                }
            }

            if (_context.JumpBufferTimer > 0f)
                _context.JumpBufferTimer -= deltaTime;
        }

        private void replayBufferedJump()
        {
            if (_context.JumpBufferTimer <= 0f)
                return;

            // SetState is a no-op while canJump is false, so this simply keeps
            // retrying until the buffer expires or the jump lands.
            _stateMachine.SetState(MovementType.Jump);
        }

        private bool canJump()
        {
            if (design == null || _isForcedMotionActive)
                return false;

            switch (_context.State)
            {
                case MovementType.Launched:
                case MovementType.ForcedFall:
                case MovementType.Stab:
                    return false;
            }

            return _context.JumpCount < design.MaxJumpCount;
        }

        private void performJump()
        {
            bool isAirJump = _context.JumpCount > 0;
            performJump(isAirJump ? design.AirJumpVelocity : design.JumpVelocity);
            setHorizontalSpeed(airSpeed());
        }

        private void performJump(float launchVelocity)
        {
            // The mover already decides this a few lines up to pick a launch
            // velocity, and then throws it away. Keeping it lets the animator
            // read the mechanic instead of re-deriving it from a counter whose
            // ceiling lives in a designer asset. Every entry into Jump passes
            // through here - including the dash-jump, which calls this overload
            // directly - so this covers them all.
            _context.IsAirJump = _context.JumpCount > 0;

            setJumpCount(_context.JumpCount + 1);
            _context.CoyoteTimer = 0f;
            _context.JumpBufferTimer = 0f;
            _context.JumpCutApplied = false;
            _context.JumpHold = JumpHoldState.Held;

            seedAirMomentum();
            setVerticalVelocity(launchVelocity * _manager.JumpModifier);
        }

        /// <summary>
        /// Trades the rest of the rise for a short hop when the button is let
        /// go early. Applied once per jump so a held-then-released-then-held
        /// input cannot re-cut the same arc.
        /// </summary>
        private void applyJumpCut()
        {
            if (_context.JumpCutApplied ||
                _context.JumpHold == JumpHoldState.Held)
            {
                return;
            }

            float verticalVelocity = _context.Rb.linearVelocity.y;
            if (verticalVelocity <= 0f)
                return;

            _context.JumpCutApplied = true;
            setVerticalVelocity(verticalVelocity * design.JumpCutMultiplier);
        }

        private void setJumpCount(int count)
        {
            _context.JumpCount = count;
            setJumpStage(count);
        }

        #endregion

        private bool isGrounded()
        {
            if (_context.Rb.linearVelocity.y >
                GroundedUpwardVelocityThreshold)
            {
                return false;
            }

            return _manager.HasGroundContact;
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

        /// <summary>
        /// Airborne motion is fully simulated, so authored root motion has no
        /// say in it. It still has to be drained every step: the animator keeps
        /// pushing deltas the whole time the character is in the air, and if
        /// they are left to pile up the first grounded step consumes the entire
        /// jump's worth at once and fires the character across the room.
        /// </summary>
        private void discardRootMotion()
        {
            _context.RootMotionDeltaPosition = Vector3.zero;
        }

        /// <summary>
        /// Ground movement is simulated, not authored.
        ///
        /// It used to be driven by root motion, which made the animator the
        /// authority on how fast the character travels. That reverses the
        /// mechanic-first order and it breaks in every blend: while a landing
        /// or a turn clip is cross-fading, the authored delta is near zero, so
        /// the character stalls on every single landing no matter what the
        /// player is holding. Speed now comes from the design asset and the
        /// animator merely shows it.
        /// </summary>
        /// <summary>
        /// Applies whichever locomotion source the current action declared.
        ///
        /// The mover does not guess. Traversal says Simulated and gets the
        /// design asset's speed; an attack says RootMotion and gets the clip's
        /// authored travel, with move input demoted to facing only. Guessing
        /// from velocities cannot tell an authored lunge apart from a
        /// cross-fade that authors nothing, and gets one of them wrong.
        /// </summary>
        private void applyGroundedMovement()
        {
            // Drained unconditionally: root motion the mover does not use must
            // never bank up and land in one lump on a later step.
            float authored = consumeRootMotionVelocity();
            float verticalVelocity = _context.Rb.linearVelocity.y;

            float horizontal;

            switch (_context.Locomotion)
            {
                case LocomotionSource.RootMotion:
                    horizontal = authored;
                    break;

                case LocomotionSource.Frozen:
                    horizontal = 0f;
                    break;

                default:
                    horizontal = simulateGroundVelocity();
                    break;
            }

            _context.Rb.linearVelocity = new Vector3(
                0f,
                verticalVelocity,
                horizontal
            );
        }

        /// <summary>
        /// Accelerates the current ground velocity toward what the input asks
        /// for, at the rates the design asset authors.
        /// </summary>
        private float simulateGroundVelocity()
        {
            float target =
                Mathf.Clamp(_context.MoveInput.x, -1f, 1f) * groundSpeed();

            float current = _context.Rb.linearVelocity.z;

            bool speedingUp =
                Mathf.Abs(target) > Mathf.Abs(current) ||
                (target != 0f && current != 0f &&
                    Mathf.Sign(target) != Mathf.Sign(current));

            float responseTime = speedingUp
                ? design.GroundAccelerationTime
                : design.GroundBrakeTime;

            float step = responseTime > Mathf.Epsilon
                ? (groundSpeed() / responseTime) * _physicsDeltaTime
                : Mathf.Infinity;

            return Mathf.MoveTowards(current, target, step);
        }

        /// <summary>
        /// Drains the root motion banked since the last physics step and states
        /// it as a velocity. Always drains, whether or not the caller uses the
        /// result, so nothing can pile up and land in one lump later.
        /// </summary>
        private float consumeRootMotionVelocity()
        {
            Vector3 delta = PhysicsAxesUtility.Project(
                _context.RootMotionDeltaPosition,
                PhysicsAxes.Z
            );
            discardRootMotion();

            if (_physicsDeltaTime <= Mathf.Epsilon)
                return 0f;

            return (delta.z / _physicsDeltaTime) * _manager.SpeedModifier;
        }

        private void stab(Vector3 stabPoint)
        {
            Vector3 planarPoint = PhysicsAxesUtility.ConstrainPoint(
                stabPoint,
                _context.MoverTransform.position,
                PhysicsAxes.YZ
            );
            _context.Rb.DOMove(
                planarPoint,
                _context.StabDuration
            )
                .SetEase(_context.StabEase)
                .SetUpdate(UpdateType.Fixed);
        }

        private Vector3 findStabDirection()
        {
            Camera camera = Camera.main;
            if (camera == null)
                return Vector3.zero;

            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, _context.CharacterMouseSensor))
            {
                Vector3 dir = PhysicsAxesUtility.Direction(hit.point - _characterOrientator.position, PhysicsAxes.YZ);

                if (dir == Vector3.zero)
                    return Vector3.zero;

                Vector3 clampedDir = clampStabDirection(dir);
                Vector3 assistedDir = findAimAssistedStabDirection(clampedDir);

                Debug.DrawLine(_characterOrientator.position, hit.point, Color.yellow);     // raw mouse ray hit
                Debug.DrawRay(_characterOrientator.position, Vector3.down * 2f, Color.blue); // base direction
                Debug.DrawRay(_characterOrientator.position, dir * 2f, Color.red);          // original dir
                Debug.DrawRay(_characterOrientator.position, clampedDir * 2f, Color.green); // clamped dir
                Debug.DrawRay(_characterOrientator.position, assistedDir * 2.5f, Color.cyan); // assisted dir

                return assistedDir;
            }
            return Vector3.zero;
        }

        private Vector3 clampStabDirection(Vector3 direction)
        {
            Vector3 planar = PhysicsAxesUtility.Direction(
                direction,
                PhysicsAxes.YZ
            );
            if (planar == Vector3.zero)
                return Vector3.zero;

            float angle = Vector3.SignedAngle(
                Vector3.down,
                planar,
                Vector3.right
            );

            if (angle >= 0f)
                angle = Mathf.Clamp(angle, _context.StabMinAngle, _context.StabMaxAngle);
            else
                angle = Mathf.Clamp(angle, -_context.StabMaxAngle, -_context.StabMinAngle);

            return (Quaternion.AngleAxis(angle, Vector3.right) *
                Vector3.down).normalized;
        }

        /// <summary>
        /// Soft lock: mouse direction stays authoritative, but a damageable
        /// character inside a narrow cone bends the stab toward its centre.
        /// Targets outside the cone or behind platform geometry are ignored.
        /// </summary>
        private Vector3 findAimAssistedStabDirection(Vector3 mouseDirection)
        {
            if (mouseDirection == Vector3.zero ||
                _context.StabAimAssistAngle <= 0f ||
                _context.StabAimAssistStrength <= 0f)
            {
                return mouseDirection;
            }

            Vector3 origin = _context.MoverTransform.position;
            int count = Physics.OverlapSphereNonAlloc(
                origin,
                _context.StabRange,
                _stabAimCandidates,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore
            );

            Vector3 bestDirection = mouseDirection;
            float bestScore = float.PositiveInfinity;

            for (int i = 0; i < count; i++)
            {
                Collider candidate = _stabAimCandidates[i];
                if (candidate == null)
                    continue;

                GameObject target = candidate.attachedRigidbody != null
                    ? candidate.attachedRigidbody.gameObject
                    : candidate.gameObject;

                if (target == null ||
                    target == _context.MoverTransform.gameObject ||
                    target.transform.IsChildOf(_context.MoverTransform) ||
                    (target.GetComponent<IHitReactable>() == null &&
                     target.GetComponent<IDamageable>() == null))
                {
                    continue;
                }

                Vector3 targetPoint = candidate.bounds.center;
                Vector3 targetDirection = PhysicsAxesUtility.Direction(
                    targetPoint - origin,
                    PhysicsAxes.YZ
                );
                if (targetDirection == Vector3.zero)
                    continue;

                float angle = Vector3.Angle(mouseDirection, targetDirection);
                if (angle > _context.StabAimAssistAngle)
                    continue;

                float distance = Vector3.Distance(
                    GameplayPlane.Flatten(origin),
                    GameplayPlane.Flatten(targetPoint)
                );

                if (Physics.Raycast(
                    origin,
                    targetDirection,
                    out RaycastHit obstruction,
                    distance,
                    _context.PlatformLayer,
                    QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                // Angle is the primary intent signal. Distance only breaks
                // ties so a nearer target wins over one directly behind it.
                float score = angle + distance * 0.05f;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                bestDirection = targetDirection;
            }

            if (float.IsPositiveInfinity(bestScore))
                return mouseDirection;

            Vector3 steered = Vector3.Slerp(
                mouseDirection,
                bestDirection,
                Mathf.Clamp01(_context.StabAimAssistStrength)
            ).normalized;

            return clampStabDirection(steered);
        }

        private Vector3 findStabPoint(Vector3 direction)
        {
            Vector3 planarDirection = PhysicsAxesUtility.Direction(direction, PhysicsAxes.YZ);

            if (planarDirection == Vector3.zero)
                return _context.MoverTransform.position;

            Vector3 origin = _context.MoverTransform.position;
            Ray ray = new Ray(origin, planarDirection);
            float travelDistance = _context.StabRange;

            if (Physics.Raycast(ray, out RaycastHit hit, _context.StabRange, _context.PlatformLayer))
                travelDistance = Mathf.Min(travelDistance, hit.distance);

            // DOMove advances the dynamic body in FixedUpdate. At stab speed a
            // rendered frame can contain several physics steps, so relying on
            // the eventual collision callback still allows the body to appear
            // beyond a thin target for one render frame. Sweep the Rigidbody's
            // actual collider volume up front and make the tween end on the
            // near side of the first solid object.
            if (_context.Rb.SweepTest(
                    planarDirection,
                    out RaycastHit bodyHit,
                    travelDistance,
                    QueryTriggerInteraction.Ignore))
            {
                travelDistance = Mathf.Min(
                    travelDistance,
                    Mathf.Max(0f, bodyHit.distance - StabCollisionSkin)
                );
            }

            Vector3 target = ray.origin + ray.direction * travelDistance;
            return PhysicsAxesUtility.ConstrainPoint(
                target,
                origin,
                PhysicsAxes.YZ
            );
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

        private void maintainGroundedMotion()
        {
            if (!isGrounded())
            {
                // At most one physics step lands here: the Fall transition is
                // evaluated immediately after this update. Keep the horizontal
                // velocity the body already has so the takeoff frame cannot pop.
                discardRootMotion();
                handleGravity(allowApexHang: true);
                _context.Rb.linearVelocity = new Vector3(
                    0f,
                    _context.VerticalVelocity,
                    _context.Rb.linearVelocity.z
                );
                return;
            }

            stickToGround();
        }

        /// <summary>
        /// Holds a small downward velocity while standing.
        ///
        /// Resting at exactly zero means the body never presses into the floor,
        /// so a single depenetration nudge is enough to break contact - which
        /// reads as a phantom airborne frame in the middle of a flat run. A
        /// constant press guarantees the contact is re-made every step, and
        /// costs nothing because the mover owns vertical velocity outright.
        /// </summary>
        private void stickToGround()
        {
            _context.VerticalVelocity = -design.GroundStickSpeed;
            _context.Rb.linearVelocity = new Vector3(
                0f,
                -design.GroundStickSpeed,
                _context.Rb.linearVelocity.z
            );
        }

        private void stopGroundedMotion()
        {
            maintainGroundedMotion();
            _context.HorizontalSpeed = 0f;
            _context.AirHorizontalVelocity = 0f;
            _context.Rb.linearVelocity = new Vector3(
                0f,
                _context.Rb.linearVelocity.y,
                0f
            );
            _context.RootMotionDeltaPosition = Vector3.zero;
        }

        private void simulateAirborneMotion(bool useAirControl)
        {
            discardRootMotion();

            if (isGrounded())
            {
                stickToGround();
                return;
            }

            handleGravity(allowApexHang: useAirControl);
            handleAirborneMovement(useAirControl);
        }

        private void configureBodyConstraints()
        {
            _context.Rb.constraints =
                RigidbodyConstraints.FreezeRotation |
                RigidbodyConstraints.FreezePositionX;
        }

        private void dash()
        {
            _context.Rb.linearVelocity = new Vector3(
                0f,
                0f,
                dashSpeed() * _context.LastFaceX
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

        private void configurePierceMotion(PierceData pierce)
        {
            _isForcedMotionActive = true;
            _pierceFollowing = true;
            _pierceElapsed = 0f;
            float attackSign = Mathf.Sign(pierce.AttackerForward.z);
            if (Mathf.Approximately(attackSign, 0f))
                attackSign = _context.LastFaceX;

            // Match the impact orientation contract from the previous commit:
            // forced motion writes MoveInput and the regular orientator turns
            // the rig from that value. Pierce does not own a second facing
            // policy based on live positions.
            _context.MoveInput = new Vector2(attackSign, 0f);
            _context.MovementBlend = 0f;
            _context.RootMotionDeltaPosition = Vector3.zero;

            Vector3 currentPosition = _context.Rb.position;
            _pierceRootOffset = currentPosition - pierce.Point;

            _context.Rb.linearVelocity = Vector3.zero;
        }

        /// <summary>
        /// A controlled physical reaction: first preserve the target's offset
        /// from the live spear tip, then hand that motion into a flatter
        /// ballistic launch. The root Rigidbody remains authoritative, so
        /// collision and ground contact keep working while the reaction
        /// animation poses the rig above it.
        /// </summary>
        private void applyPierceMotion()
        {
            float deltaTime = Mathf.Max(_physicsDeltaTime, 0.0001f);
            PierceSettings settings = _pierce.Settings;
            _pierceElapsed += deltaTime;
            discardRootMotion();

            Vector3 tip = Vector3.zero;
            bool canFollow = _pierceFollowing &&
                _pierceElapsed <= Mathf.Max(0f, settings.MaxFollowDuration) &&
                _pierce.Source != null &&
                _pierce.Source.IsPierceActive &&
                _pierce.Source.TryGetPiercePoint(out tip);

            if (canFollow)
            {
                Vector3 desired = PhysicsAxesUtility.ConstrainPoint(
                    tip + _pierceRootOffset,
                    _context.Rb.position,
                    PhysicsAxes.YZ
                );

                // A descending stab should scrape a grounded target along the
                // floor, not tunnel its root through it. Upward contact remains
                // free to lift an airborne target.
                if (isGrounded() && desired.y < _context.Rb.position.y)
                    desired.y = _context.Rb.position.y;

                Vector3 velocity = (desired - _context.Rb.position) / deltaTime;
                float maxFollowSpeed = Mathf.Max(0f, settings.MaxFollowSpeed);
                if (maxFollowSpeed > 0f)
                    velocity = Vector3.ClampMagnitude(velocity, maxFollowSpeed);

                if (isGrounded() && velocity.y <= 0f)
                    velocity.y = -design.GroundStickSpeed;

                _context.Rb.linearVelocity = new Vector3(
                    0f,
                    velocity.y,
                    velocity.z
                );

                return;
            }

            releasePiercedTarget(settings);
        }

        /// <summary>
        /// Converts the authored stab angle into an upward release with the
        /// same horizontal sign. Only a fraction of its vertical component is
        /// retained, so even a steep downward stab throws the target mostly
        /// backward rather than popping it up like the heavy attack.
        /// </summary>
        private void releasePiercedTarget(PierceSettings settings)
        {
            Vector3 stabDirection = PhysicsAxesUtility.Direction(
                _pierce.Direction,
                PhysicsAxes.YZ
            );

            // Use the same stable attack-forward rule as the impact pipeline
            // did in the previous commit. Blade velocity can reverse during
            // the animation, but the attacker's authored forward cannot.
            float horizontalSign = Mathf.Sign(_pierce.AttackerForward.z);
            if (Mathf.Approximately(horizontalSign, 0f))
                horizontalSign = Mathf.Sign(stabDirection.z);
            if (Mathf.Approximately(horizontalSign, 0f))
                horizontalSign = _context.LastFaceX;

            float authoredScale = settings.ReleaseVerticalScale > 0f
                ? settings.ReleaseVerticalScale
                : 0.55f;
            float authoredMinimum = settings.MinimumReleaseVerticalRatio > 0f
                ? settings.MinimumReleaseVerticalRatio
                : 0.28f;
            float authoredMaximum = settings.MaximumReleaseVerticalRatio > 0f
                ? settings.MaximumReleaseVerticalRatio
                : 0.45f;

            float minimumVertical = Mathf.Clamp01(
                Mathf.Min(
                    authoredMinimum,
                    authoredMaximum
                )
            );
            float maximumVertical = Mathf.Clamp(
                Mathf.Max(
                    authoredMinimum,
                    authoredMaximum
                ),
                minimumVertical,
                0.95f
            );
            float verticalRatio = Mathf.Clamp(
                Mathf.Abs(stabDirection.y) *
                    Mathf.Clamp01(authoredScale),
                minimumVertical,
                maximumVertical
            );
            float horizontalRatio = Mathf.Sqrt(
                1f - verticalRatio * verticalRatio
            );
            Vector3 releaseDirection =
                Vector3.forward * (horizontalSign * horizontalRatio) +
                Vector3.up * verticalRatio;

            _pierceFollowing = false;
            _impact = new ImpactData(
                releaseDirection,
                settings.ReleaseForce > 0f
                    ? settings.ReleaseForce
                    : 14f,
                _context.Rb.position,
                PhysicsAxes.YZ,
                PhysicsAxes.X
            );

            _stateMachine.SetState(MovementType.Launched);
        }

        private void resetForcedMotion()
        {
            _isForcedMotionActive = false;
            _pierceFollowing = false;
            _pierceElapsed = 0f;
            // A reaction can interrupt an attack mid-swing, and the combat
            // snapshot that would hand the ground back may never arrive. Recover
            // to the mover owning locomotion rather than leaving the character
            // waiting on root motion that is no longer playing.
            _context.Locomotion = LocomotionSource.Simulated;
            _context.MoveInput = Vector2.zero;
            _context.MovementBlend = 0f;
            _context.HorizontalSpeed = 0f;
            _context.AirHorizontalVelocity = 0f;
            _context.VerticalVelocity = 0f;
            _context.RootMotionDeltaPosition = Vector3.zero;
        }

        /// <summary>
        /// Carries whatever horizontal speed the body already had into the air
        /// state, so stepping off a ledge or jumping out of a dash does not
        /// snap the character to a different speed on the takeoff frame.
        /// </summary>
        private void seedAirMomentum()
        {
            _context.AirHorizontalVelocity = _context.Rb.linearVelocity.z;
        }

        private void handleAirborneMovement(bool useAirControl)
        {
            float horizontal;

            if (useAirControl)
            {
                float target =
                    Mathf.Clamp(_context.MoveInput.x, -1f, 1f) *
                    _context.HorizontalSpeed;

                bool speedingUp =
                    Mathf.Abs(target) > Mathf.Abs(_context.AirHorizontalVelocity);

                float responseTime = speedingUp
                    ? design.AirAccelerationTime
                    : design.AirBrakeTime;

                float step = responseTime > Mathf.Epsilon
                    ? (_context.HorizontalSpeed / responseTime) * _physicsDeltaTime
                    : Mathf.Infinity;

                _context.AirHorizontalVelocity = Mathf.MoveTowards(
                    _context.AirHorizontalVelocity,
                    target,
                    step
                );

                horizontal = _context.AirHorizontalVelocity;
            }
            else
            {
                horizontal = _context.HorizontalVelocity;
                _context.AirHorizontalVelocity = horizontal;
            }

            _context.Rb.linearVelocity = new Vector3(
                0f,
                _context.VerticalVelocity,
                horizontal
            );
        }

        private void handleGravity(bool allowApexHang)
        {
            float verticalVelocity =
                _context.Rb.linearVelocity.y -
                currentGravity(allowApexHang) * _physicsDeltaTime;

            _context.VerticalVelocity = Mathf.Max(
                verticalVelocity,
                -design.TerminalFallSpeed
            );
        }

        /// <summary>
        /// Asymmetric gravity: the character rises on one curve and drops on a
        /// heavier one, with a softened band around the apex so the top of the
        /// arc is long enough to aim from.
        /// </summary>
        private float currentGravity(bool allowApexHang)
        {
            float verticalVelocity = _context.Rb.linearVelocity.y;

            if (allowApexHang &&
                design.ApexVelocityWindow > 0f &&
                Mathf.Abs(verticalVelocity) < design.ApexVelocityWindow)
            {
                return design.RiseGravity * design.ApexGravityMultiplier;
            }

            return verticalVelocity > 0f
                ? design.RiseGravity
                : design.FallGravity;
        }

        private float groundSpeed() => design.GroundSpeed * _manager.SpeedModifier;

        private float airSpeed() => design.AirSpeed * _manager.SpeedModifier;

        private float dashSpeed() => design.DashSpeed * _manager.SpeedModifier;

        private void setHorizontalSpeed(float speed)
        {
            _context.HorizontalSpeed = speed;
        }

        private void setVerticalVelocity(float velocity)
        {
            _context.VerticalVelocity = velocity;
            _context.Rb.linearVelocity = new Vector3(
                0f,
                velocity,
                _context.Rb.linearVelocity.z
            );
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

        private void setComboType(MovementComboType comboType)
        {
            _context.ComboType = comboType;
        }

        [System.Serializable]
        public class Context
        {
            [Header("Design")]
            [Tooltip("Every gravity, launch velocity and air speed used by " +
                "this mover is derived from this asset. Tweak it at play " +
                "time - changes land on the next physics step.")]
            public MovementDesignSO Design;

            [Header("Scene Bindings")]
            public Rigidbody Rb;
            public Transform MoverTransform;
            public Transform StabPoint;
            public LayerMask PlatformLayer;
            public LayerMask CharacterMouseSensor;

            [Header("Facing")]
            public float FaceTurnSpeedInDegree = 720;
            public float FaceDeadzone = 0.05f;
            public int LastFaceX = 1;

            [Header("Animation Blend")]
            public float BlendAcceleration;

            [Header("Stab")]
            public float StabDuration;
            public Ease StabEase;
            public float StabRange = 10f;
            public float StabMinAngle = 30;
            public float StabMaxAngle = 80;
            [Range(0f, 45f)] public float StabAimAssistAngle = 14f;
            [Range(0f, 1f)] public float StabAimAssistStrength = 0.85f;

            [Header("Live State")]
            public MovementType State;
            public MovementComboType ComboType;
            public LocomotionSource Locomotion;
            public Vector2 MoveInput;
            public Vector3 RootMotionDeltaPosition;
            public float MovementBlend;
            public int JumpRight;
            public bool IsStabStarted;
            public bool IsStabbing;
            public bool IsDashEnded;
            public bool IsStabEnded;

            [HideInInspector] public float VerticalVelocity;
            [HideInInspector] public float HorizontalSpeed;
            [HideInInspector] public float AirHorizontalVelocity;
            [HideInInspector] public float CoyoteTimer;
            [HideInInspector] public float JumpBufferTimer;
            [HideInInspector] public int JumpCount;

            /// <summary>
            /// Whether the jump now being spent left the air rather than the
            /// ground. Written by performJump, which is the one way into a
            /// jump, so it needs no reset; only read while State is Jump.
            /// </summary>
            [HideInInspector] public bool IsAirJump;
            [HideInInspector] public bool JumpCutApplied;
            [HideInInspector] public bool JumpPressLatched;
            [HideInInspector] public JumpHoldState JumpHold;

            public float HorizontalVelocity => MoveInput.x * HorizontalSpeed;
        }

    }
}
