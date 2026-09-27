/// <summary>
/// How the cut itself is built - the slash's own shape, as opposed to the
/// ribbon trailing behind it.
///
/// Band and Circle are different promises rather than different looks. One is
/// built from where the weapon has been and cannot come away from it; the
/// other is a clean shape struck once, which reads better and tracks nothing.
/// Which is wanted depends on the attack, so it is a choice on the profile -
/// including the choice not to draw a cut at all and let the trail be the
/// whole stroke.
/// </summary>
public enum SlashShapeMode : byte
{
    /// <summary>
    /// A band swept along the blade's real path. Its leading edge is the blade
    /// by construction, so the cut follows a swing that wanders, changes plane
    /// or moves with the character.
    /// </summary>
    Band = 0,

    /// <summary>
    /// A ring struck about the axis the blade was on when the cue fired.
    ///
    /// The whole circle is geometry from the first frame. What rides the blade
    /// is where the ring is brightest, so the sweep runs at the speed of the
    /// swing while the shape itself stays a shape.
    /// </summary>
    Circle = 1,

    /// <summary>
    /// No cut at all. The ribbon is the whole stroke.
    /// </summary>
    /// <remarks>
    /// Worth stating rather than arriving at. Band mode with no particle
    /// prefab already draws nothing, because that is where its materials come
    /// from - but silently, and only until somebody fills the prefab field in
    /// for the sake of the spray and finds a cut they never asked for back on
    /// screen. This says the profile does not want one.
    /// </remarks>
    None = 2
}
