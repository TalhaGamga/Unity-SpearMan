using UnityEngine;

public class MovementManager : MonoBehaviour, IMovementManager
{
    [Header("Public Encapsulating Variables")]
    public float JumpHeight => _baseJumpHeight * _jumpHeightModifier;
    public float MoveSpeed => _baseMoveSpeed * _speedModifier;

    [Header("Public Airborne Variables")]
    public float TimeToApex;
    public float GravityEffector;

    [Header("States")]
    public bool IsMoving;
    public bool IsJumping;
    public bool IsFalling;
    public bool IsIdling;

    [Header("Inputs")]
    public bool JumpInput;

    [Header("Move")]
    public float AccelerationRate = 1.0f;

    [HideInInspector] public Vector2 MovementInputDirection;
    [SerializeField] private float _baseMoveSpeed;
    [SerializeField] private float _speedModifier;

    [Header("Jump")]
    [SerializeField] private float _baseJumpHeight;
    [SerializeField] private float _jumpHeightModifier;

    [Header("Ground Check")]
    [SerializeField] private bool isGrounded;
    [SerializeField] private Transform[] groundCheckPoints;
    [SerializeField] private float checkDistance;
    [SerializeField] private LayerMask groundLayer;

    [Header("Private Variables")]
    private IMover currentMover;
    private IHumanoidMovementPromptReceiver promptReceiver;

    private void OnDrawGizmos()
    {
        foreach (var checkPoint in groundCheckPoints)
        {
            Gizmos.DrawLine(checkPoint.position, checkPoint.position - Vector3.up * checkDistance);
        }
    }
    private void Awake()
    {
        promptReceiver = GetComponent<IHumanoidMovementPromptReceiver>();
    }

    private void Start()
    {
        SetMover(new RBMover());
    }

    private void OnEnable()
    {
        promptReceiver.OnMoveInput += takeMoveInputs;
        promptReceiver.OnJumpInput += triggerJump;
        promptReceiver.OnJumpCancel += cancelJump;
    }

    private void OnDisable()
    {
        promptReceiver.OnMoveInput -= takeMoveInputs;
        promptReceiver.OnJumpInput -= triggerJump;
        promptReceiver.OnJumpCancel -= cancelJump;
    }

    private void Update()
    {
        checkGround();

        currentMover?.Tick();
    }

    private void FixedUpdate()
    {
        currentMover?.FixedTick();
    }

    public void SetMover(IMover newMover)
    {
        currentMover?.End();
        newMover?.Init(this);
        currentMover = newMover;
    }

    private void takeMoveInputs(Vector2 movementInputs)
    {
        MovementInputDirection = movementInputs.normalized;
    }
    public bool IsGrounded()
    {
        return isGrounded;
    }

    private void checkGround()
    {
        isGrounded = false;

        foreach (var checkPoint in groundCheckPoints)
        {
            isGrounded |= Physics2D.Raycast(checkPoint.position, -checkPoint.up, checkDistance, groundLayer);
        }
    }

    private void triggerJump()
    {
        currentMover?.TriggerJump();
    }

    private void cancelJump()
    {
        currentMover?.CancelJump();
    }

    public void SetJumpModifier(float newModifier)
    {
        _jumpHeightModifier = newModifier;
    }

    public void SetSpeedModifier(float newModifier)
    {
        _speedModifier = newModifier;
    }
}