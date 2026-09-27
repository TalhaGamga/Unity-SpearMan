/// <summary>
/// How the slash ring's radius is read off the swing it was struck on.
///
/// Either way it is measured over the stroke's own window - from the frame
/// the start cue fires to the frame the swing ends - rather than taken from
/// the weapon. The blade's length is only where the tip began: the arm
/// extends and the hand travels, so a ring struck at the blade's length sits
/// well inside the cut that made it.
/// </summary>
public enum SlashCircleRadius : byte
{
    /// <summary>
    /// The widest circle the blade swept - the furthest the tip ever got from
    /// the centre. The ring encloses the whole cut, which is the reading that
    /// matches what the swing looks like it covered.
    /// </summary>
    Widest = 0,

    /// <summary>
    /// The mean reach across the stroke. Smaller and steadier than the widest,
    /// because one frame at full extension cannot set it; worth having when a
    /// swing snaps out at the end and the widest reading makes the ring jump.
    /// </summary>
    Average = 1
}
