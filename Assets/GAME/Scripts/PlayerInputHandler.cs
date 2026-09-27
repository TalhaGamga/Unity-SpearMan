using R3;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInputHandler : MonoBehaviour, IInputHandler
{
    public BehaviorSubject<InputSnapshot> InputSnapshotStream { get; }
        = new BehaviorSubject<InputSnapshot>(InputSnapshot.Empty);

    private readonly Dictionary<PlayerAction, InputType> _currentInputs = new();
    private readonly Dictionary<PlayerAction, InputType> _previousInputs = new();
    private InputSnapshot _lastSnapshot;

    [SerializeField] private InputReader _input;

    private void Start()
    {
        _input.Move += onMove;
        _input.Jump += onJump;
        _input.Attack += onAttack;
        _input.Dash += onDash;
        _input.MouseDrag += onMouseDrag;

        _input.Enable();
    }

    /// <summary>
    /// Takes this handler back off the reader.
    /// </summary>
    /// <remarks>
    /// The reader is a ScriptableObject, so it is an asset rather than a scene
    /// object: it outlives every reload, and so does anything still subscribed
    /// to it. Without this, reloading the sandbox leaves the destroyed
    /// handler's callbacks in the reader's invocation list, and the next key
    /// press reaches a MonoBehaviour that no longer exists.
    ///
    /// That is worse than a stray error in the console. These are multicast
    /// delegates, and the dead subscriber is the older one, so it runs first
    /// and throws before the live handler is ever reached - the fresh scene's
    /// input is dead on arrival, and it looks like the input system broke
    /// rather than like a leak from the run before.
    ///
    /// Named methods rather than the lambdas this used to use, because a
    /// lambda cannot be unsubscribed: -= needs the same delegate back, and
    /// every lambda expression is a new one.
    /// </remarks>
    private void OnDestroy()
    {
        if (_input == null)
            return;

        _input.Move -= onMove;
        _input.Jump -= onJump;
        _input.Attack -= onAttack;
        _input.Dash -= onDash;
        _input.MouseDrag -= onMouseDrag;
    }

    private void onMove(Vector2 direction) =>
        HandleInput(PlayerAction.Move, direction.magnitude > 0, direction);

    private void onJump(bool isPressed) =>
        HandleInput(PlayerAction.Jump, isPressed);

    private void onAttack(bool isPressed) =>
        HandleInput(PlayerAction.PrimaryAttack, isPressed);

    private void onDash(bool isPressed) =>
        HandleInput(PlayerAction.Dash, isPressed);

    private void onMouseDrag(Vector2 position) =>
        HandleInput(PlayerAction.MouseDelta, position.magnitude > 0, position);

    private void HandleInput(PlayerAction action, bool isHeld, object value = default)
    {
        var behavior = InputBehaviorMap.Behavior.TryGetValue(action, out var b) ? b : InputBehavior.Eventful;

        bool wasHeld = _currentInputs.TryGetValue(action, out var prevInput) && prevInput.IsHeld;

        var input = new InputType
        {
            Action = action,
            IsHeld = isHeld,
            //Direction = direction,
            Value = value, // Fill as needed
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
            // IsHeld stays truthful for as long as the button is down, while
            // the press and release edges live for exactly one frame. Consumers
            // that need an edge (jump) and consumers that need the hold (jump
            // cut, air control) both read the same snapshot.
            input.WasPresseedThisFrame = isHeld && !wasHeld;
            input.WasReleasedThisFrame = !isHeld && wasHeld;

            UpdateInput(action, input);

            if (input.WasPresseedThisFrame || input.WasReleasedThisFrame)
                StartCoroutine(ClearEdgeFlagsNextFrame(action));
        }
    }

    private System.Collections.IEnumerator ResetEventfulInputNextFrame(PlayerAction action, InputType resetInput)
    {
        yield return null;
        UpdateInput(action, resetInput);
    }

    /// <summary>
    /// Clears only the one-frame edge flags and re-reads the rest of the input
    /// from the live dictionary, so a value that changed in the meantime (a
    /// move direction, say) is never clobbered by a stale captured copy.
    /// </summary>
    private System.Collections.IEnumerator ClearEdgeFlagsNextFrame(PlayerAction action)
    {
        yield return null;

        if (!_currentInputs.TryGetValue(action, out var current))
            yield break;

        if (!current.WasPresseedThisFrame && !current.WasReleasedThisFrame)
            yield break;

        current.WasPresseedThisFrame = false;
        current.WasReleasedThisFrame = false;
        UpdateInput(action, current);
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

    private bool InputEquals(InputType a, InputType b)
    {
        return a.IsHeld == b.IsHeld &&
               a.Value == b.Value &&
               //a.Direction == b.Direction &&
               a.WasPresseedThisFrame == b.WasPresseedThisFrame &&
               a.WasReleasedThisFrame == b.WasReleasedThisFrame;
    }
}
