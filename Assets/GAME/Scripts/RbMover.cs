using R3;
using Unity.VisualScripting;
using UnityEngine;

public class RbMover : IMover
{
    public Observable<MovementSnapshot> Stream => _stream;

    private Rigidbody _rb;
    private Transform _characterOrientator;
    private Transform _characterTranslater;
    private IMovementManager _manager;

    private float _moveSpeed = 5f;
    private bool _canJump = true;

    private Vector2 _moveInput;
    private bool _jumpQueued; // Set when player tries to jump
    private float _jumpBufferTime = 0.15f; // (seconds, tweak to taste)
    private float _jumpBufferTimer;

    private float _coyoteTime = 0.5f; // seconds
    private float _coyoteTimer;
    private bool _wasGroundedLastFrame;
    private bool _isFalling;
    private bool _wasFallingLastFrame;

    private float _jumpHeight = 4f;           // meters
    private float _jumpTimeToPeak = 0.45f;     // seconds
    private float _gravity;
    private float _jumpVelocity;


    private bool _isJumping;
    private bool _jumpHeld;

    private MoveState _lastState = MoveState.Idle;
    private float _lastRawSpeed = 0f;
    private float _smoothedSpeed = 0f;
    private const float _speedLerpRate = 10f;
    private const float _moveLerpRate = 8f;   // For Rigidbody movement

    private Subject<MovementSnapshot> _stream = new();

    public void Init(IMovementManager movementManager)
    {
        _manager = movementManager;

        _characterTranslater = _manager?.CharacterTranslater;
        _characterOrientator = _manager?.CharacterOrientator;

        _rb = (_manager as MonoBehaviour)?.GetComponent<Rigidbody>();
        if (_rb == null)
            Debug.LogError("RbMover: Rigidbody not found!");

        _rb.useGravity = false;

        _gravity = (2f * _jumpHeight) / (_jumpTimeToPeak * _jumpTimeToPeak);
        _jumpVelocity = _gravity * _jumpTimeToPeak;
        _characterOrientator.Rotate(0, 90, 0);
    }

    public void End()
    {
        _moveInput = Vector2.zero;
    }

    public void HandleInput(MovementAction action)
    {
        switch (action.ActionType)
        {
            case MovementActionType.Jump:
                _jumpQueued = true; // Queue jump, handled in UpdateMover
                _jumpHeld = true;
                _jumpBufferTimer = _jumpBufferTime;
                break;

            case MovementActionType.Land:
                _jumpHeld = false;
                break;
            // Handle Land

            case MovementActionType.Dash:
                // Dash logic here
                break;
        }
    }

    public void UpdateMover(float deltaTime)
    {
        if (_rb == null) return;

        // --- Handle jump buffer timing ---
        if (_jumpQueued)
        {
            _jumpBufferTimer -= deltaTime;
            if (_jumpBufferTimer <= 0f)
                _jumpQueued = false;
        }

        // --- Read current Rigidbody velocity ---
        Vector3 rbVel = _rb.linearVelocity;

        // --- Grounded check ---
        bool isGrounded = _manager.GetIsGrounded();

        // --- COYOTE TIME LOGIC ---
        if (isGrounded)
        {
            _coyoteTimer = _coyoteTime;
            if (!_isJumping)
                _coyoteTimer = _coyoteTime;
        }
        else
        {
            _coyoteTimer -= deltaTime;
        }

        // --- Horizontal movement (XZ), with smoothing ---
        Vector3 targetVel = new Vector3(_moveInput.x, 0, _moveInput.y) * _moveSpeed;
        Vector3 currentXZ = new Vector3(rbVel.x, 0, rbVel.z);
        Vector3 smoothedXZ = Vector3.Lerp(currentXZ, targetVel, 1 - Mathf.Exp(-_moveLerpRate * deltaTime));

        // --- Vertical velocity ---
        float verticalVel = rbVel.y;

        // --- JUMP logic (uses coyote time and jump buffer) ---
        if (_jumpQueued && _coyoteTimer > 0f && !_isJumping)
        {
            verticalVel = _jumpVelocity;
            _isJumping = true;
            _jumpQueued = false;
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
        }
        else if (isGrounded)
        {
            // Snap to ground ONLY ON LANDING, not every frame
            if (!_wasGroundedLastFrame)
            {
                verticalVel = 0f;
                _isJumping = false;
            }
            else
            {
                // Preserve Y as is (let Unity handle micro-bounces)
                // verticalVel = rbVel.y;
            }
        }
        else
        {
            // --- Early jump cancel (jump cut) ---
            bool isRising = rbVel.y > 0.1f;
            if (_isJumping && !_jumpHeld && isRising)
            {
                verticalVel *= .98f; // Cut jump
                Debug.Log("Jump Cut");
            }

            // --- Apply custom gravity ---
            verticalVel -= _gravity * deltaTime;
        }

        // --- Assemble final velocity and apply ---
        Vector3 finalVel = new Vector3(smoothedXZ.x, verticalVel, smoothedXZ.z);
        _rb.linearVelocity = finalVel;

        // --- Rotate as before ---
        Vector3 horizontalMove = new Vector3(_moveInput.x, 0f, _moveInput.y);
        if (horizontalMove.sqrMagnitude > 0.01f && _characterOrientator != null)
        {
            Quaternion targetRot = Quaternion.LookRotation(horizontalMove, Vector3.up);
            _characterOrientator.rotation = Quaternion.Slerp(_characterOrientator.rotation, targetRot, 0.1f);
        }

        // --- FALLING STATE DETECTION ---
        _isFalling = !isGrounded && _rb.linearVelocity.y < 0.1f;

        // --- EVENT FLAGS FOR ANIMATION ---
        bool justLanded = _wasFallingLastFrame && isGrounded;
        bool justStartedFalling = !isGrounded && _wasGroundedLastFrame;

        // --- Update memory for next frame ---
        _wasGroundedLastFrame = isGrounded;
        _wasFallingLastFrame = _isFalling;

        // --- Animation speed blending (Animator) ---
        float rawSpeed = targetVel.magnitude;
        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, rawSpeed, 1 - Mathf.Exp(-_speedLerpRate * deltaTime));

        //MoveState state = _smoothedSpeed < 0.1f ? MoveState.Idle
        //                  : _smoothedSpeed < 5f ? MoveState.Walk
        //                                        : MoveState.Run;

        MoveState state = MoveState.Idle;

        if (justLanded)
        {
            state = MoveState.Landed;
        }

        else if (_isJumping && rbVel.y > .1f)
        {
            state = MoveState.Jump;
        }

        else if (_isFalling)
        {
            state = MoveState.Fall;
        }

        else if (_smoothedSpeed < .1f)
        {
            state = MoveState.Idle;
        }

        else if (_smoothedSpeed < 4f)
        {
            state = MoveState.Walk;
        }

        else
        {
            state = MoveState.Run;
        }


        // Emit only if state or displayed speed has changed meaningfully
        if (state != _lastState || Mathf.Abs(_smoothedSpeed - _lastRawSpeed) > 0.01f)
        {
            _lastState = state;
            _lastRawSpeed = _smoothedSpeed;
            _stream.OnNext(new MovementSnapshot(state, _smoothedSpeed));
        }
    }


    public void SetMoveInput(Vector2 move)
    {
        _moveInput = move;
    }
}