/// <summary>
/// Where a numeric property of an effect comes from. Keeps the authored value
/// and the live-driven value as one field instead of two that can disagree.
/// </summary>
public enum VisualScalar : byte
{
    /// <summary>Use the authored value as-is.</summary>
    Fixed = 0,

    /// <summary>
    /// Scale the authored value by how fast the weapon was moving when the cue
    /// fired, normalised through the entry's speed range. A committed swing
    /// reads bigger and faster than a flick, without a second cue entry.
    /// </summary>
    SwingSpeed = 1
}
