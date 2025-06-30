using R3;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
    public BehaviorSubject<InputSnapshot> InputSnapshotStream { get; }
        = new BehaviorSubject<InputSnapshot>(InputSnapshot.Empty);

    private readonly Dictionary<PlayerAction, InputType> _currentInputs = new();
    private readonly Dictionary<PlayerAction, InputType> _previousInputs = new();
    private InputSnapshot _lastSnapshot;

    [SerializeField] private InputReader _input;

    private void Start()
    {
        _input.Move += direction =>
        {
            HandleInput(PlayerAction.Run, direction.magnitude > 0, direction);
        };

        _input.Jump += isPressed =>
        {
            HandleInput(PlayerAction.Jump, isPressed);
        };

        _input.Attack += isPressed =>
        {
            HandleInput(PlayerAction.PrimaryAttack, isPressed);
        };

        _input.Enable();
    }

    private void HandleInput(PlayerAction action, bool isHeld, Vector2 direction = default)
    {
        var behavior = InputBehaviorMap.Behavior.TryGetValue(action, out var b) ? b : InputBehavior.Eventful;

        bool wasHeld = _currentInputs.TryGetValue(action, out var prevInput) && prevInput.IsHeld;

        var input = new InputType
        {
            Action = action,
            IsHeld = isHeld,
            Direction = direction,
            Value = 0f, // Fill as needed
            WasPresseedThisFrame = false
        };

        if (behavior == InputBehavior.Eventful)
        {
            // If this is a new press, mark as JustPressed for this snapshot only
            input.WasPresseedThisFrame = isHeld && !wasHeld;
            input.IsHeld = input.WasPresseedThisFrame; // Only 'true' for the frame it is pressed

            UpdateInput(action, input);

            // Immediately "unset" the eventful input for next snapshots so it's only one-shot
            if (input.WasPresseedThisFrame)
            {
                var resetInput = input;
                resetInput.IsHeld = false;
                resetInput.WasPresseedThisFrame = false;
                // Delay this until next frame to avoid race conditions if needed
                StartCoroutine(ResetEventfulInputNextFrame(action, resetInput));
            }
        }
        else // Stateful
        {
            input.WasPresseedThisFrame = isHeld && !wasHeld; // Optionally track, but mainly use IsHeld
            UpdateInput(action, input);
        }
    }

    private System.Collections.IEnumerator ResetEventfulInputNextFrame(PlayerAction action, InputType resetInput)
    {
        yield return null;
        UpdateInput(action, resetInput);
    }

    /// <summary>
    /// Updates the internal state for a given action, and pushes a new InputSnapshot if the state changed.
    /// </summary>
    private void UpdateInput(PlayerAction action, InputType newInput)
    {
        _previousInputs[action] = _currentInputs.TryGetValue(action, out var prev) ? prev : default;

        bool changed = !_currentInputs.TryGetValue(action, out var prevInput) || !InputEquals(prevInput, newInput);

        if (changed)
        {
            _currentInputs[action] = newInput;

            var newSnapshot = new InputSnapshot
            {
                CurrentInputs = new Dictionary<PlayerAction, InputType>(_currentInputs),
                TimeStamp = Time.time
            };

            if (!InputSnapshotEquals(_lastSnapshot, newSnapshot))
            {
                _lastSnapshot = newSnapshot;
                InputSnapshotStream.OnNext(newSnapshot);
            }
        }
    }

    private bool InputEquals(InputType a, InputType b)
    {
        return a.IsHeld == b.IsHeld &&
               a.Value == b.Value &&
               a.Direction == b.Direction &&
               a.WasPresseedThisFrame == b.WasPresseedThisFrame;
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
