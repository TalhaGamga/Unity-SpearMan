/// <summary>
/// What a visual cue actually produces.
///
/// Most cues drop a pooled prefab and forget about it. A slash is different:
/// it is generated from the weapon's own reach and heading at the instant the
/// cue fires, so it cannot be a prefab sitting in a set.
/// </summary>
public enum VisualCueKind : byte
{
    /// <summary>Spawn the cue's Prefab through the VFX manager, placed per Alignment.</summary>
    Prefab = 0,

    /// <summary>
    /// Build and play a slash from the cue's SlashProfile, pivoting on the
    /// cue's Anchor and reaching to its ArcTip.
    ///
    /// A Prefab set on the same cue is spawned as well, placed per Alignment,
    /// for a one-off flash or impact burst on top of the stroke.
    /// </summary>
    Slash = 1
}
