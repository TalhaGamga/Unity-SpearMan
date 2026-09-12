/// <summary>
/// Who owns the character's horizontal motion right now.
///
/// Declared per action rather than guessed by the mover. A mover that has to
/// infer this from velocities and input magnitudes will always get some case
/// wrong: an attack that authors a lunge looks exactly like a cross-fade that
/// authors nothing, until you ask the definition which one it is.
/// </summary>
public enum LocomotionSource : byte
{
    /// <summary>
    /// The mover drives, using the speeds in the movement design asset.
    /// Animation is a read-out. This is the default for traversal.
    /// </summary>
    Simulated = 0,

    /// <summary>
    /// The animation drives, through root motion. Move input still steers
    /// facing, but it no longer contributes any translation - so an authored
    /// lunge lands exactly where the animator placed it.
    /// </summary>
    RootMotion = 1,

    /// <summary>
    /// Nothing drives. The character is pinned for the duration, whatever the
    /// input or the clip say.
    /// </summary>
    Frozen = 2
}
