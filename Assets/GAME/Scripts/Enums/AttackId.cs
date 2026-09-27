/// <summary>
/// Names the attack the animator should be playing.
///
/// This is the identity that crosses into CharacterSwordCombat.controller, so
/// over there it is a name rather than a number. The difference is what
/// happens when it is wrong: a wrong name is a parameter the validator refuses
/// to find, while a wrong number is a perfectly valid number that nothing can
/// check.
///
/// It replaces a parameter called Version, which versioned nothing. Version
/// carried two magic integers whose meaning existed only as a threshold in the
/// controller - and they were numbered against their own clips, 2 selecting
/// Dash_Attack_ver_A and 1 selecting ver_B. SwordIntentMapper already knew
/// which attack it wanted by name and threw that name away; this carries it
/// the rest of the way.
///
/// Only meaningful while <see cref="CombatSnapshot.IsAttacking"/> is true.
/// </summary>
public enum AttackId : byte
{
    None = 0,

    /// <summary>First swing of the ground combo. Animator: Attack_3Combo_1.</summary>
    ComboOpener = 1,

    /// <summary>Second swing. Animator: Attack_3Combo_2_Inplace.</summary>
    ComboFollow = 2,

    /// <summary>Third swing, which wraps back to the opener. Animator: Attack_3Combo_3.</summary>
    ComboFinisher = 3,

    /// <summary>Stab thrown from standing. Animator: Dash_Attack_ver_B.</summary>
    GroundDashStab = 4,

    /// <summary>Stab thrown out of a dashing jump. Animator: Dash_Attack_ver_A.</summary>
    AirDashStab = 5
}
