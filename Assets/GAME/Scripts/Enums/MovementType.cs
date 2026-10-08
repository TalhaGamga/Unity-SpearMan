/// <summary>
/// What the mover is doing.
/// </summary>
/// <remarks>
/// These numbers are a serialized wire format, not an implementation detail.
/// Unity serializes enum fields by VALUE, and this one is written into
/// Character.prefab (RBMoverMachine._context.State). Reordering or inserting a
/// member silently remaps saved prefab and scene data, with nothing failing to
/// compile and nothing failing at run time - the character simply comes back
/// from disk believing it is doing something else.
///
/// It used to be worse. Until the animator was given a named vocabulary these
/// ordinals were also compared against bare integers in
/// CharacterSwordCombat.controller - Jump happened to be 4, so thirty
/// conditions across the graph were spelled "MoveState == 4". Five members
/// below were then unreachable from code and load-bearing anyway, because
/// deleting any one of them would have shifted ForcedFall off the 16 that a
/// Reaction-layer transition was keyed on.
///
/// Add new members at 100 and up. Never renumber an existing one.
/// </remarks>
public enum MovementType
{
    None = 0,
    Idle = 1,
    Walk = 2,
    Move = 3,
    Jump = 4,
    Fall = 5,

    /// <summary>
    /// Unreachable from code: the second jump is a Jump whose air-ness the
    /// mover publishes separately, because the chain can be longer than two.
    /// </summary>
    DoubleJump = 6,

    Land = 7,
    Dash = 8,
    Stab = 9,

    /// <summary>Unreachable from code. Placeholder for traversal not yet built.</summary>
    Climb = 10,

    /// <summary>Unreachable from code. Placeholder for traversal not yet built.</summary>
    SwimUp = 11,

    /// <summary>Unreachable from code. Placeholder for traversal not yet built.</summary>
    Interact = 12,

    /// <summary>Unreachable from code. Placeholder for traversal not yet built.</summary>
    Parkour = 13,

    Neutral = 14,
    Launched = 15,
    ForcedFall = 16,

    /// <summary>
    /// Controlled follow-and-release motion while held by a penetrating
    /// weapon. New values live at 100+ to preserve the serialized wire format.
    /// </summary>
    Pierced = 100
}
