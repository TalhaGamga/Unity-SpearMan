using System;

/// <summary>
/// Actions that may take control away from the attack currently playing.
///
/// This is deliberately a gameplay contract, not an Animator concern. Attack
/// logic publishes the options; intent mappers decide whether the player's
/// requested action is one of them; the Animator only reflects the resulting
/// movement and combat snapshots.
/// </summary>
[Flags]
public enum CombatCancelOptions : byte
{
    None = 0,
    Move = 1 << 0,
    Jump = 1 << 1,
    Dash = 1 << 2,
    Attack = 1 << 3,

    Mobility = Move | Jump | Dash,
    All = Mobility | Attack
}
