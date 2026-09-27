/// <summary>
/// How an impact's fade-out runs from full to gone over the last part of each
/// particle's life. Named so the character of the fade can be picked rather
/// than drawn; Custom hands it to a curve.
/// </summary>
public enum ImpactFadeShape : byte
{
    /// <summary>An even fall. What the impact was authored with.</summary>
    Linear = 0,

    /// <summary>Drops quickly and trails off faint, like a glow cooling.</summary>
    Soft = 1,

    /// <summary>Leaves full gently and lands on nothing gently: no edge at either end.</summary>
    Smooth = 2,

    /// <summary>Holds nearly full, then goes at the very end.</summary>
    Hard = 3,

    /// <summary>The tuning's own Fade Curve, as drawn.</summary>
    Custom = 4
}
