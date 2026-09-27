using UnityEngine;

/// <summary>
/// One line of a weapon's visual dictionary: what a cue key looks like on this
/// weapon, and how it is placed.
///
/// Carries no timing of any kind. When a cue fires is authored on the animation
/// clip, which is the only place that can know it - a clip retimed by an artist
/// would silently desync any duration duplicated here.
/// </summary>
[System.Serializable]
public struct VisualCueEntry
{
    [Tooltip("Matches Cue=... in the clip's animation event. Case-insensitive.")]
    public string CueKey;

    [Tooltip("Prefab: drop the prefab below, placed per Alignment. Slash: " +
        "build a stroke from the profile below, and spawn the prefab too if " +
        "one is set.")]
    public VisualCueKind Kind;

    [Tooltip("What actually spawns. Pooled by the VFX manager.")]
    public GameObject Prefab;

    [Tooltip("Slash only. Shape, colour and timing of the stroke.")]
    public SlashProfile SlashProfile;
 
    [Header("Placement")]
    [Tooltip("Which point on the weapon rig the effect is born at. For a " +
        "slash this is the point the arc turns around, normally the grip.")]
    public VisualAnchor Anchor;

    [Tooltip("What the effect points at, sampled the moment it spawns. " +
        "Ignored by a slash, which always aims itself along the weapon.")]
    public VisualAlignment Alignment;

    [Tooltip("Offset from the anchor, in the anchor's own space.")]
    public Vector3 PositionOffset;

    [Tooltip("Extra rotation applied after alignment. Degrees.")]
    public Vector3 RotationOffset;

    [Tooltip("Base scale. Multiplied by the swing binding when one is set.")]
    public Vector3 Scale;

    [Tooltip("Keep the effect attached to the anchor for its whole life. " +
        "Right for trails, wrong for arcs - an arc should stay where it was struck.")]
    public bool FollowAnchor;

    [Tooltip("Flip the effect when the character faces the other way. Needed " +
        "for prefabs authored with a handedness of their own.")]
    public bool MirrorOnFacing;

    [Header("Reach")]
    [Tooltip("Far end of the weapon. Sets a slash's reach, and the ring " +
        "radius for a prefab using BladeTrailArc alignment.")]
    public VisualAnchor ArcTip;

    [Tooltip("Prefab only. Radius of the prefab's arc at scale one, in world " +
        "units, for BladeTrailArc alignment.")]
    [Min(0.01f)] public float ArcRadius;

    [Tooltip("Prefab only. Direction from the prefab's origin to its arc's " +
        "leading edge, in degrees within the prefab's local XY plane.")]
    public float ArcLeadAngle;

    [Tooltip("Prefab only. Which way the prefab's arc runs from lead to tail.")]
    public VisualArcWinding ArcWinding;

    [Header("Swing Bindings")]
    [Tooltip("Whether scale is authored or driven by how hard the swing was.")]
    public VisualScalar ScaleBy;

    [Tooltip("Whether particle playback rate is authored or driven by swing speed.")]
    public VisualScalar RateBy;

    [Tooltip("Swing speed in units/sec that maps to 0 and 1. Below x the " +
        "binding is at its weakest, above y at its strongest.")]
    public Vector2 SwingSpeedRange;

    [Tooltip("Strength at the bottom and top of the speed range. A floor above " +
        "zero keeps a slow swing from spawning an invisible effect.")]
    public Vector2 SwingStrengthRange;

    [Header("Lifetime")]
    [Tooltip("Prefab only. Seconds before the effect returns to the pool. 0 " +
        "derives it from the prefab's own particle systems.")]
    [Min(0f)] public float Lifetime;

    /// <summary>
    /// Sane starting point for a hand-added entry: visible, unflipped, born at
    /// the blade and aimed along the swing.
    /// </summary>
    public static VisualCueEntry Default => new VisualCueEntry
    {
        CueKey = string.Empty,
        Kind = VisualCueKind.Prefab,
        Prefab = null,
        SlashProfile = null,
        Anchor = VisualAnchor.Blade,
        Alignment = VisualAlignment.SwingVelocity,
        PositionOffset = Vector3.zero,
        RotationOffset = Vector3.zero,
        Scale = Vector3.one,
        FollowAnchor = false,
        MirrorOnFacing = false,
        ArcTip = VisualAnchor.BladeTip,
        ArcRadius = 1f,
        ArcLeadAngle = 90f,
        ArcWinding = VisualArcWinding.TowardPositiveAngle,
        ScaleBy = VisualScalar.Fixed,
        RateBy = VisualScalar.Fixed,
        SwingSpeedRange = new Vector2(2f, 14f),
        SwingStrengthRange = new Vector2(0.75f, 1.25f),
        Lifetime = 0f
    };
}
