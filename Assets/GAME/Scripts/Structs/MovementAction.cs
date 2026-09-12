using UnityEngine;

public struct MovementAction
{
    public MovementType ActionType;
    public float Duration;
    public Vector2 Direction;

    /// <summary>
    /// Jump button hold state, used by the mover for variable jump height.
    /// Mappers that do not own the jump button leave this at
    /// <see cref="JumpHoldState.Unchanged"/> so they cannot cut a jump short.
    /// </summary>
    public JumpHoldState JumpHold;

    /// <summary>
    /// Who owns horizontal motion for this action. Traversal leaves it at
    /// <see cref="LocomotionSource.Simulated"/>; an attack hands over its
    /// definition's choice so the mover stops steering and lets the clip do it.
    /// </summary>
    public LocomotionSource Locomotion;
}
