/// <summary>
/// The character of the highlight streak that rides on the base slash.
///
/// The highlight is the part that says what swung. A rapier and a mace trace
/// the same arc through the air; what separates them is where the bright part
/// of the stroke swells and how sharply it ends. Each entry here is a set of
/// numbers for that, so the shape can be chosen by name rather than dialled in
/// from scratch every time.
/// </summary>
public enum SlashHighlightShape : byte
{
    /// <summary>Long, narrow, sharp at both ends. Rapiers and thin blades.</summary>
    ThinBlade = 0,

    /// <summary>Balanced and flowing, widest in the middle. A standard sword.</summary>
    Sword = 1,

    /// <summary>Swells late and ends in a point. Axes and cleavers.</summary>
    Axe = 2,

    /// <summary>Short, thick and blunt. Maces and hammers.</summary>
    Mace = 3,

    /// <summary>Use the profile's own highlight numbers instead of a preset.</summary>
    Custom = 4
}
