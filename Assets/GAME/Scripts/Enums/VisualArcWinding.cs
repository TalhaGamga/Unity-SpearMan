/// <summary>
/// Which way an authored arc effect runs from its leading edge toward its
/// fading tail, measured in the prefab's own local XY plane with angles going
/// from local +X toward local +Y.
///
/// Purely a description of the asset, never of the swing: the visualizer
/// compares it against the actual swing direction and mirrors the effect when
/// the two disagree, so the tail always ends up behind the blade.
/// </summary>
public enum VisualArcWinding : byte
{
    /// <summary>The tail lies at larger angles than the lead (+X toward +Y).</summary>
    TowardPositiveAngle = 0,

    /// <summary>The tail lies at smaller angles than the lead (+Y toward +X).</summary>
    TowardNegativeAngle = 1
}
