/// <summary>
/// A movement the combat side reads to pick a variant of an attack.
/// </summary>
/// <remarks>
/// Serialized into Character.prefab (RBMoverMachine._context.ComboType), so
/// the values are a wire format. Pin them; add at 100 and up.
/// </remarks>
public enum MovementComboType
{
    None = 0,

    /// <summary>
    /// A jump launched straight out of a dash. SwordIntentMapper reads this to
    /// turn a stab into the airborne variant.
    /// </summary>
    DashingJump = 1
}
