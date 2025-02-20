using UnityEngine;

public class RBMover : IMover
{
    private MovementManager _movementManager;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Transform orientation;
    private Vector3 originalModelScale;

    private Vector2 inputDirection;

    private float gravity; // Custom gravity for jumping
    private float gravityModifierBase = 1.0f;
    private float gravityModifier = 1.0f;
    private float fallingGravityModifier = 2.0f;

    private float _jumpSpeed; // Initial jump velocity
    private float jumpBufferTime = 0.2f;
    private float jumpCutBase = 1.0f;
    private float jumpCutModifierBase = 1.0f;
    private float jumpCutAmount = 1.5f;
    private float jumpCutModifier = 1.0f;
    private float jumpBufferTimer;
    private float coyoteJumpTime = 0.2f;
    private float coyoteJumpTimer;
    private float jumpCut => jumpCutBase * jumpCutModifier;
    private float _gravity => gravity * gravityModifier * _movementManager.GravityEffector;

    private StateMachine stateMachine;
    private StateTransition idleToMove;
    private StateTransition moveToIdle;
    private StateTransition idleToJump;
    private StateTransition moveToJump;
    private StateTransition jumpToFall;
    private StateTransition fallToIdle;
    private StateTransition moveToFall;
    private StateTransition fallToCoyoteJump;

    private bool jumpInput;
    private bool isAscending;
    private bool isStopping;
    private bool isMoving;
    private bool hasJumped = false;

    public void Init(MovementManager movementManager)
    {
        rb = movementManager.GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        spriteRenderer = movementManager.GetComponentInChildren<SpriteRenderer>();
        _movementManager = movementManager;
        orientation = spriteRenderer.gameObject.transform;
        originalModelScale = orientation.localScale;

        stateMachine = new StateMachine(StateType.Idle);
        idleToMove = new StateTransition(StateType.Idle, StateType.Move, () => { return isMoving && _movementManager.IsGrounded(); }, 0, () => { _movementManager.IsIdling = false; _movementManager.IsMoving = true; });
        moveToIdle = new StateTransition(StateType.Move, StateType.Idle, () => { return !isMoving && _movementManager.IsGrounded(); }, 0, () => { _movementManager.IsMoving = false; _movementManager.IsIdling = true; });
        idleToJump = new StateTransition(StateType.Idle, StateType.Jump, () => { return hasJumped; }, 0, () => { _movementManager.IsIdling = false; _movementManager.IsJumping = true; });
        moveToJump = new StateTransition(StateType.Move, StateType.Jump, () => { return hasJumped; }, 0, () => { _movementManager.IsMoving = false; _movementManager.IsJumping = true; });
        jumpToFall = new StateTransition(StateType.Jump, StateType.Fall, () => { return !isAscending && !_movementManager.IsGrounded(); }, 0, () => { _movementManager.IsJumping = false; _movementManager.IsFalling = true; });
        fallToIdle = new StateTransition(StateType.Fall, StateType.Idle, () => { return _movementManager.IsGrounded(); }, 0, () => { _movementManager.IsFalling = false; _movementManager.IsIdling = true; });
        moveToFall = new StateTransition(StateType.Move, StateType.Fall, () => { return !_movementManager.IsGrounded(); }, 0, () => { _movementManager.IsMoving = false; _movementManager.IsFalling = true; });
        fallToCoyoteJump = new StateTransition(StateType.Fall, StateType.Jump, () => { return hasJumped; }, 0, () => { _movementManager.IsFalling = false; _movementManager.IsJumping = true; });

        stateMachine.AddTransition(idleToMove);
        stateMachine.AddTransition(moveToIdle);
        stateMachine.AddTransition(idleToJump);
        stateMachine.AddTransition(moveToJump);
        stateMachine.AddTransition(jumpToFall);
        stateMachine.AddTransition(fallToIdle);
        stateMachine.AddTransition(moveToFall);
        stateMachine.AddTransition(fallToCoyoteJump);

        gravity = (2 * _movementManager.JumpHeight) / Mathf.Pow(_movementManager.TimeToApex, 2);
    }

    public void Tick()
    {
        setDirection();
        handleJump();
        handleCoyoteTimer();
        handleJumpCut();
        setBooleans();

        stateMachine.Update();
    }

    public void FixedTick()
    {
        handleMove();
        applyCustomGravity();
    }

    public void TriggerJump()
    {
        float jumpHeight = _movementManager.JumpHeight;
        float timeToApex = _movementManager.TimeToApex;

        _jumpSpeed = (2 * jumpHeight) / timeToApex;
        gravity = (2 * jumpHeight) / Mathf.Pow(timeToApex, 2);

        jumpBufferTimer = jumpBufferTime;

        jumpCutModifier = jumpCutModifierBase;

        _movementManager.JumpInput = true;
    }

    public void CancelJump()
    {
        _movementManager.JumpInput = false;
    }

    public void End()
    {
    }

    private void handleJumpCut()
    {
        if (stateMachine.currentState.Equals(StateType.Jump) && !_movementManager.JumpInput)
        {
            jumpCutModifier = jumpCutAmount;
        }
    }

    private void handleMove()
    {
        float xVelocity = _movementManager.MoveSpeed * Mathf.Sign(inputDirection.x) * Mathf.Ceil(Mathf.Abs(inputDirection.x));
        float velocityDiff = xVelocity - rb.linearVelocity.x;
        float moveX = velocityDiff * _movementManager.AccelerationRate;
        rb.AddForce(moveX * Vector2.right);
    }

    private void setDirection()
    {
        inputDirection = _movementManager.MovementInputDirection;

        if (Mathf.Abs(inputDirection.x) > 0)
        {
            orientation.localScale = new Vector3(Mathf.Sign(inputDirection.x) * originalModelScale.x, originalModelScale.y, originalModelScale.z);
        }
    }

    private void handleJump()
    {
        jumpBufferTimer -= Time.deltaTime;

        if (!hasJumped && jumpBufferTimer > 0 && coyoteJumpTimer > 0)
        {
            jump();
            jumpBufferTimer = 0;
            coyoteJumpTimer = 0;
        }
    }

    private void jump()
    {
        rb.linearVelocityY = _jumpSpeed; // Apply custom jump speed
        hasJumped = true;
    }

    private void handleCoyoteTimer()
    {
        if (_movementManager.IsGrounded())
        {
            coyoteJumpTimer = coyoteJumpTime;
        }

        else
        {
            coyoteJumpTimer -= Time.deltaTime;
        }
    }

    private void applyCustomGravity() // MAKE BETTER //HERE BUGGY
    {
        if (_movementManager.IsGrounded())
        {
            gravityModifier = gravityModifierBase;
            return;
        }

        if (rb.linearVelocityY < 0 && hasJumped)
        {
            hasJumped = false; // Handle hasJumped in a more robust way.

            if (jumpCutModifier == jumpCutModifierBase)
            {
                gravityModifier = fallingGravityModifier;
            }

            else
            {
                gravityModifier = gravityModifierBase;
            }
        }

        if (rb.linearVelocityY < 0 && !hasJumped)
        {
            gravityModifier = fallingGravityModifier;
        }

        else
        {
            gravityModifier = gravityModifierBase;
        }

        rb.linearVelocityY -= _gravity * jumpCut * Time.fixedDeltaTime;
    }

    private void resetGravityModifier()
    {
        gravityModifier = gravityModifierBase;
    }

    private void resetJumpCutModifier()
    {
        jumpCutModifier = jumpCutModifierBase;
    }

    private void setBooleans()
    {
        isAscending = rb.linearVelocityY > 0;
        isMoving = Mathf.Abs(rb.linearVelocityX) > 0.1f;
    }
}