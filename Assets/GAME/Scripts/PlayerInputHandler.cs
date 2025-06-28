using R3;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
    public BehaviorSubject<InputSnapshot> InputSnapshotStream { get; }
        = new BehaviorSubject<InputSnapshot>(InputSnapshot.Empty);

    private readonly Dictionary<PlayerAction, InputType> _currentInputs = new();
    private InputSnapshot _lastSnapshot;

    [SerializeField] private InputReader _input;

    private void Start()
    {
        // Register event listeners to update input state
        _input.Move += direction =>
        {
            UpdateInput(PlayerAction.Run, new InputType
            {
                Action = PlayerAction.Run,
                IsHeld = direction.magnitude > 0,
                Direction = direction
            });
        };

        _input.Jump += isPressed =>
        {
            UpdateInput(PlayerAction.Jump, new InputType
            {
                Action = PlayerAction.Jump,
                IsHeld = isPressed
            });
        };

        _input.Attack += isPressed =>
        {
            UpdateInput(PlayerAction.PrimaryAttack, new InputType
            {
                Action = PlayerAction.PrimaryAttack,
                IsHeld = isPressed
            });
        };

        // Add more listeners for other actions as needed...

        _input.Enable();
    }

    /// <summary>
    /// Updates the internal state for a given action, and pushes a new InputSnapshot if the state changed.
    /// </summary>
    private void UpdateInput(PlayerAction action, InputType newInput)
    {
        bool changed = !_currentInputs.TryGetValue(action, out var prevInput) || !InputEquals(prevInput, newInput);

        if (changed)
        {
            _currentInputs[action] = newInput;

            // Construct a new snapshot
            var newSnapshot = new InputSnapshot
            {
                CurrentInputs = new Dictionary<PlayerAction, InputType>(_currentInputs),
                TimeStamp = Time.time
            };

            // Only emit if changed (optional, since we checked above)
            if (!InputSnapshotEquals(_lastSnapshot, newSnapshot))
            {
                _lastSnapshot = newSnapshot;
                InputSnapshotStream.OnNext(newSnapshot);
            }
        }
    }

    // Basic equality comparison for InputType (expand as needed)
    private bool InputEquals(InputType a, InputType b)
    {
        return a.IsHeld == b.IsHeld &&
               a.Value == b.Value &&
               a.Direction == b.Direction; // If Direction is a struct, this works.
    }

    private bool InputSnapshotEquals(InputSnapshot a, InputSnapshot b)
    {
        if (a.CurrentInputs == null && b.CurrentInputs == null) return true;
        if (a.CurrentInputs == null || b.CurrentInputs == null) return false;
        if (a.CurrentInputs.Count != b.CurrentInputs.Count) return false;

        foreach (var kvp in a.CurrentInputs)
        {
            if (!b.CurrentInputs.TryGetValue(kvp.Key, out var other))
                return false;
            if (!InputEquals(kvp.Value, other))
                return false;
        }
        return true;
    }

}
