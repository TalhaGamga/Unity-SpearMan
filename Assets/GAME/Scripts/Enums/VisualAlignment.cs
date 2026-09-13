/// <summary>
/// What an effect points at. Sampled once, when the effect spawns - a slash arc
/// is struck in the direction the blade was travelling at that instant and then
/// lives its own life.
/// </summary>
public enum VisualAlignment : byte
{
    /// <summary>Inherit the anchor transform's own rotation.</summary>
    AnchorRotation = 0,

    /// <summary>Point along the blade. Reads the weapon's pose, not its motion.</summary>
    BladeDirection = 1,

    /// <summary>
    /// Point along the swing. The truest alignment for an arc, but it collapses
    /// to the blade direction on a frame where the weapon is momentarily still.
    /// </summary>
    SwingVelocity = 2,

    /// <summary>Point where the character is facing, ignoring the weapon.</summary>
    FacingForward = 3,

    /// <summary>
    /// Lay the effect flat in the gameplay plane and roll it to the swing.
    ///
    /// The right alignment for a slash arc in this game: traversal freezes X,
    /// so the action lives in the YZ plane and the camera watches it from the
    /// side. A plain LookRotation along the swing would stand the arc on edge
    /// and the player would see a sliver. This faces the arc at the camera and
    /// spins it to match the cut, which also means it reads correctly in both
    /// facing directions without a mirror.
    /// </summary>
    PlanarSwingArc = 4
}
