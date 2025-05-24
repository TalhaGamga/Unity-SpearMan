using UnityEngine;

public class RbMover : IMover
{
    private Rigidbody _rb;
    private Transform _characterTransform;
    private IMovementManager _manager;

    private float _moveSpeed = 5f;
    private float _jumpForce = 6f;
    private bool _canJump = true;

    private Vector2 _cachedMoveInput;
    private bool _jumpRequested;
    private Vector2 _currentMoveInput;

    public void Init(IMovementManager movementManager)
    {
        _manager = movementManager;

        _characterTransform = _manager?.CharacterModelTransform;
        _rb = (_manager as MonoBehaviour)?.GetComponent<Rigidbody>();
        if (_rb == null)
            Debug.LogError("RbMover: Rigidbody not found!");
    }

    public void End()
    {
        _cachedMoveInput = Vector2.zero;
        _jumpRequested = false;
    }

    public void HandleInput(MovementAction action)
    {
        switch (action.ActionType)
        {
            case MovementActionType.Jump:
                if (_manager.IsGrounded())
                    _rb.AddForce(Vector3.up * 6f, ForceMode.Impulse);
                break;
            case MovementActionType.Dash:
                break;
        }
    }

    public void UpdateMover(float deltaTime)
    {
        if (_rb == null || _characterTransform == null) return;

        Vector3 move = new Vector3(_cachedMoveInput.x, 0f, _cachedMoveInput.y) * _moveSpeed;

        move.y = _rb.linearVelocity.y;
        _rb.linearVelocity = move;

        Vector3 horizontalMove = new Vector3(_cachedMoveInput.x, 0f, _cachedMoveInput.y);
        if (horizontalMove.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(horizontalMove, Vector3.up);
            _characterTransform.rotation = Quaternion.Slerp(_characterTransform.rotation, targetRot, 0.1f); // Smoothing factor
        }

        // Jump logic
        if (_jumpRequested && _canJump && _manager.IsGrounded())
        {
            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            _jumpRequested = false;
            _canJump = false;
        }
    }

    public void SetMoveInput(Vector2 move)
    {
        _currentMoveInput = move;
    }
}
