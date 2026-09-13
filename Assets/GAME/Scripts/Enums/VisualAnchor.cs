/// <summary>
/// Where on the weapon rig an effect is placed. Resolved by the weapon itself,
/// so a spear can answer "Blade" with its tip and a rifle with its muzzle
/// without the visual layer knowing the difference.
/// </summary>
public enum VisualAnchor : byte
{
    /// <summary>Centre of the damage volume - the middle of the arc.</summary>
    Blade = 0,

    /// <summary>Far end of the weapon, where a trail would read best.</summary>
    BladeTip = 1,

    /// <summary>The grip. Effects that belong to the wielder, not the swing.</summary>
    Hand = 2,

    /// <summary>The character's own origin, at the feet.</summary>
    Root = 3
}
