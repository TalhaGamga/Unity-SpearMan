using R3;
using UnityEngine;

public class RbMover : IMover
{
    public Observable<MovementSnapshot> Stream => _stream;

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

    private MovementType _lastState = MovementType.Idle;
    private float _lastBlendSpeed = 0f;
    private float _smoothedBlendSpeed = 0f;
    private const float _speedLerpRate = 10f;

    private readonly Subject<MovementSnapshot> _stream = new();

    public void Init(IMovementManager movementManager)
    {
        _manager = movementManager;
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

    public void SetMoveInput(Vector2 move)
    {
        _moveInput = move;
        //Debug.Log(_moveInput);
    }

    public void HandleRootMotion(Vector3 delta) => _rootMotionDelta = delta;

    public void HandleInput(MovementAction action)
    {
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

        // --- Root motion delta for ground movement (do not change axis) ---
        float zRootSpeed = _rootMotionDelta.z / deltaTime;
        float xRootSpeed = _rootMotionDelta.x / deltaTime;
        Vector3 rootMotionVelocity = new Vector3(xRootSpeed, 0f, zRootSpeed);

        // --- Physics for vertical (Y) ---
        float verticalVel = rbVel.y;
        bool jumpingThisFrame = false;

        // --- Jump logic ---
        if (_jumpQueued && _coyoteTimer > 0f && !_isJumping)
        {
            verticalVel = _jumpVelocity;
            jumpingThisFrame = true;
            _isJumping = true;
            _jumpQueued = false;
        }
        else if (isGrounded)
        {
            if (!_wasGroundedLastFrame)
            {
                verticalVel = 0f;
                _isJumping = false;
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
            // --- Ground: use root motion for X/Z ---
            finalVel = new Vector3(rootMotionVelocity.x, verticalVel, rootMotionVelocity.z * _moveInput.x);
        }
        else
        {
            // --- Air: use input for X/Z (air control), ignore root motion ---
            Vector3 airMove = new Vector3(-_moveInput.y, 0f, _moveInput.x).normalized * _airControlSpeed;
            finalVel = new Vector3(airMove.x, verticalVel, airMove.z);
        }

        // --- Apply velocity ---
        _rb.linearVelocity = finalVel;

        // --- Face intended direction based on input (for visuals only) ---
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

        // --- Animation MoveSpeed for blending (from input magnitude, not root motion) ---
        float inputBlendSpeed = _moveInput.magnitude;
        _smoothedBlendSpeed = Mathf.Lerp(_smoothedBlendSpeed, inputBlendSpeed, 1 - Mathf.Exp(-_speedLerpRate * deltaTime));

        // --- State (from input magnitude, not speed) ---
        MovementType state = MovementType.Idle;
        if (justLanded) state = MovementType.Land;
        else if (_isJumping && rbVel.y > .1f) state = MovementType.Jump;
        else if (_isFalling) state = MovementType.Fall;
        else if (_moveInput.sqrMagnitude < 0.01f) state = MovementType.Idle;
        else if (_moveInput.sqrMagnitude < 0.96f) state = MovementType.Walk;
        else state = MovementType.Run;

        // --- Emit MovementSnapshot only on change ---
        if (state != _lastState || Mathf.Abs(_smoothedBlendSpeed - _lastBlendSpeed) > 0.01f)
        {
            _lastState = state;
            _lastBlendSpeed = _smoothedBlendSpeed;
            _stream.OnNext(new MovementSnapshot(state, _smoothedBlendSpeed));
        }

        // --- Reset root motion ---
        _rootMotionDelta = Vector3.zero;
    }
}
