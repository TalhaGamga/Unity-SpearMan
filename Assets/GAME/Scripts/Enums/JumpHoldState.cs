/// <summary>
/// Tri-state so an intent mapper that has no opinion about the jump button
/// cannot accidentally clear the hold flag for one that does.
/// </summary>
public enum JumpHoldState : byte
{
    Unchanged = 0,
    Released = 1,
    Held = 2
}
