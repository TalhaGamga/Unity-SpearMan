/// <summary>
/// Shared read of the jump button's hold state.
///
/// Every intent mapper that can short-circuit the movement intent stamps this
/// onto the action it produces. Without it a reaction or a forced motion would
/// swallow the release event, and the mover would keep believing the button is
/// still down long after the player let go.
/// </summary>
public static class JumpInputReader
{
    public static JumpHoldState ReadHold(InputSnapshot inputSnapshot)
    {
        if (inputSnapshot.CurrentInputs == null)
            return JumpHoldState.Unchanged;

        if (!inputSnapshot.CurrentInputs.TryGetValue(
                PlayerAction.Jump,
                out var jumpInput))
        {
            return JumpHoldState.Unchanged;
        }

        return jumpInput.IsHeld
            ? JumpHoldState.Held
            : JumpHoldState.Released;
    }
}
