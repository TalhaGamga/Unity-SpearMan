using R3;
using UnityEngine;

public class RbMover : IMover
{
    public MovementType LastState => _lastState;

    private Rigidbody _rb;
    private Transform _characterOrientator;
    private Transform _characterTranslater;
    private IMovementManager _manager;

    private Vector2 _moveInput;
    private Vector3 _rootMotionDelta = Vector3.zero;

    // Jump, physics, state vars
    private bool _jumpQueued;
    private bool _isJumping;
    private bool _jumpHeld;
    private float _jumpBufferTimer;
    private float _coyoteTimer;

    private readonly float _jumpBufferTime = 0.15f;
    private readonly float _coyoteTime = 0.2f;
    private readonly float _jumpHeight = 4f;
    private readonly float _jumpTimeToPeak = 0.45f;

    [SerializeField] private float _airControlSpeed = 7f; // Air horizontal speed

    private float _gravity;
    private float _jumpVelocity;

    private bool _wasGroundedLastFrame;
    private bool _wasFallingLastFrame;
    private bool _isFalling;

    private int _jumpStage = 0;          // Current jump count (0=ground, 1=first jump, etc.)
    private int _maxJumpStage = 2;       // 1=single, 2=double, 3=triple jump...

    public MovementType _lastState = MovementType.Idle;
    private float _lastBlendSpeed = 0f;
    private float _smoothedBlendSpeed = 0f;
    private const float _speedLerpRate = 10f;

    private BehaviorSubject<MovementSnapshot> _stream;
    private Subject<MovementTransition> _transitionStream;
    private bool _forceIdle;

    public void Init(IMovementManager movementManager, BehaviorSubject<MovementSnapshot> SnapshotStream, Subject<MovementTransition> TransitionStream)
    {
        _manager = movementManager;
        _stream = SnapshotStream;
        _transitionStream = TransitionStream;

        _characterTranslater = _manager.CharacterTranslater;
        _characterOrientator = _manager.CharacterOrientator;

        _rb = (_manager as MonoBehaviour)?.GetComponent<Rigidbody>();
        if (_rb == null)
            Debug.LogError("RbMover: Rigidbody not found!");

        _rb.useGravity = false;

        _gravity = (2f * _jumpHeight) / (_jumpTimeToPeak * _jumpTimeToPeak);
        _jumpVelocity = _gravity * _jumpTimeToPeak;
    }

    public void End() => _moveInput = Vector2.zero;

    public void HandleRootMotion(Vector3 delta) => _rootMotionDelta = delta;

    public void HandleAction(MovementAction action)
    {
        // Always update move input on run/fall for air control
        if (action.ActionType == MovementType.Run || action.ActionType == MovementType.Fall)
            _moveInput = action.Direction;

        if (action.ActionType == MovementType.Idle)
        {
            _moveInput = action.Direction;
            _forceIdle = true;
        }
        else
        {
            _forceIdle = false;
        }

        // Jump queue (let air jumps happen too!)
        if (action.ActionType == MovementType.Jump)
        {
            _jumpQueued = true;
            _jumpHeld = true;
            _jumpBufferTimer = _jumpBufferTime;
        }
        else if (action.ActionType == MovementType.Land)
        {
            _jumpHeld = false;
        }
    }

    public void UpdateMover(float deltaTime)
    {
        if (_rb == null) return;

        // --- Jump buffer ---
        if (_jumpQueued)
        {
            _jumpBufferTimer -= deltaTime;
            if (_jumpBufferTimer <= 0f)
                _jumpQueued = false;
        }

        Vector3 rbVel = _rb.linearVelocity;
        bool isGrounded = _manager.GetIsGrounded();

        // --- Coyote time logic ---
        if (isGrounded) _coyoteTimer = _coyoteTime;
        else _coyoteTimer -= deltaTime;

        // --- Root motion delta for ground movement ---
        float zRootSpeed = _rootMotionDelta.z / deltaTime;
        float xRootSpeed = _rootMotionDelta.x / deltaTime;
        Vector3 rootMotionVelocity = new Vector3(xRootSpeed, 0f, zRootSpeed);

        // --- Physics for vertical (Y) ---
        float verticalVel = rbVel.y;
        bool jumpingThisFrame = false;

        // --- Air jump logic: allow jump if not at max stage
        if (_jumpQueued && _jumpStage < _maxJumpStage)
        {
            verticalVel = _jumpVelocity;
            jumpingThisFrame = true;
            _isJumping = true;
            _jumpQueued = false;
            _jumpStage++;
        }
        else if (isGrounded)
        {
            if (!_wasGroundedLastFrame)
            {
                verticalVel = 0f;
                _isJumping = false;
                _jumpStage = 0;
            }
        }
        else
        {
            bool rising = rbVel.y > 0.1f;
            if (_isJumping && !_jumpHeld && rising)
                verticalVel *= 0.98f;
            verticalVel -= _gravity * deltaTime;
        }

        Vector3 finalVel = rbVel;

        if (isGrounded)
        {
            if (_moveInput.sqrMagnitude < 0.01f)
            {
                // --- IDLE: apply root motion in character's facing direction ---
                if (_characterOrientator != null)
                {
                    Vector3 rootMotionLocal = new Vector3(rootMotionVelocity.x, 0f, rootMotionVelocity.z);
                    Vector3 rootMotionWorld = _characterOrientator.TransformDirection(rootMotionLocal);
                    finalVel = new Vector3(rootMotionWorld.x, verticalVel, rootMotionWorld.z);
                }
                else
                {
                    finalVel = new Vector3(rootMotionVelocity.x, verticalVel, rootMotionVelocity.z);
                }
            }
            else
            {
                // --- WALK/RUN: use root motion modulated by input (customize if needed) ---
                finalVel = new Vector3(rootMotionVelocity.x, verticalVel, rootMotionVelocity.z * _moveInput.x);
            }
        }
        else
        {
            // --- Air: use input for X/Z (air control), ignore root motion ---
            Vector3 airMove = new Vector3(-_moveInput.y, 0f, _moveInput.x).normalized * _airControlSpeed;
            finalVel = new Vector3(airMove.x, verticalVel, airMove.z);
        }

        // --- Apply velocity ---
        _rb.linearVelocity = finalVel;

        // --- Face intended direction ---
        if (_moveInput.sqrMagnitude > 0.01f && _characterOrientator != null)
        {
            Vector3 inputDir = new Vector3(-_moveInput.y, 0f, _moveInput.x);
            Quaternion targetRot = Quaternion.LookRotation(inputDir.normalized, Vector3.up);
            _characterOrientator.rotation = Quaternion.Slerp(_characterOrientator.rotation, targetRot, 0.2f);
        }

        _isFalling = !isGrounded && _rb.linearVelocity.y < -0.1f;
        bool justLanded = _wasFallingLastFrame && isGrounded;

        _wasGroundedLastFrame = isGrounded;
        _wasFallingLastFrame = _isFalling;

        // --- Animation MoveSpeed for blending ---
        float inputBlendSpeed = _moveInput.magnitude;
        _smoothedBlendSpeed = Mathf.Lerp(_smoothedBlendSpeed, inputBlendSpeed, 1 - Mathf.Exp(-_speedLerpRate * deltaTime));

        // --- State ---
        MovementType state = MovementType.Idle;
        if (justLanded) state = MovementType.Land;
        else if (_isJumping && rbVel.y > .1f) state = MovementType.Jump;
        else if (_isFalling) state = MovementType.Fall;
        else if (_moveInput.sqrMagnitude < 0.01f || _forceIdle) state = MovementType.Idle;
        else if (_moveInput.sqrMagnitude < 0.96f) state = MovementType.Walk;
        else state = MovementType.Run;

        bool stateChanged = (state != _lastState);
        bool blendChanged = Mathf.Abs(_smoothedBlendSpeed - _lastBlendSpeed) > 0.01f;
        bool jumpStageChanged = jumpingThisFrame;

        // --- 1. Emit MovementSnapshot FIRST (whenever data changes) ---
        if (stateChanged || blendChanged || jumpStageChanged)
        {
            _lastBlendSpeed = _smoothedBlendSpeed;
            _stream.OnNext(new MovementSnapshot(state, _smoothedBlendSpeed, _jumpStage));
        }

        // --- 2. Then emit transition signal ONLY if state changed ---
        if (stateChanged)
        {
            if (state == MovementType.Fall)
            {
                _transitionStream?.OnNext(new MovementTransition { From = _lastState, To = state });
            }

            _lastState = state;
        }

        // --- Reset root motion ---
        _rootMotionDelta = Vector3.zero;
    }


}
