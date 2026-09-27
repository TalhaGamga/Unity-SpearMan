/// <summary>
/// What the combat machine is doing.
/// </summary>
/// <remarks>
/// Serialized into Character.prefab (CombatManager._currentType), so these
/// values are a wire format. Pin them; add at 100 and up.
///
/// Several members below are not reachable today. They are kept rather than
/// deleted because deleting one renumbers the rest, and the renumbering would
/// land in a prefab field that nothing would report as wrong.
/// </remarks>
public enum CombatType
{
    None = 0,
    GroundedPrimaryAttack = 1,

    /// <summary>Not reachable today.</summary>
    GroundedSecondaryAttack = 2,

    Stab = 3,

    /// <summary>Not reachable today.</summary>
    AirborneDashingAttack = 4,

    /// <summary>Not reachable today.</summary>
    AirbornePrimaryAttack = 5,

    /// <summary>Not reachable today.</summary>
    AirborneSecondaryAttack = 6,

    /// <summary>Not reachable today.</summary>
    Cancel = 7,

    /// <summary>Not reachable today.</summary>
    InPrimaryAttack = 8,

    Idle = 9
}
