using UnityEngine;

/// <summary>
/// One confirmed hit, as the weapon that landed it saw it.
///
/// Built at the only point a hit is confirmed - the weapon's hit scan, the
/// moment a target joins the window's hit set - and handed to presentation
/// before the rules run. That order matters: a destructive or slicing attack
/// may replace the struck collider over the next few frames, and whatever
/// draws the hit needs it while it is still whole.
///
/// Carries attacker-side facts only. Nothing here says what the hit did to the
/// target - no damage taken, no reaction chosen - because the rules have not
/// run yet when this is built, and presentation that waited for them would be
/// a frame or more late on exactly the attacks that hit hardest.
/// </summary>
public readonly struct WeaponHit
{
    /// <summary>The attack whose window was open when the hit landed.</summary>
    public AttackDefinition Attack { get; }

    /// <summary>
    /// What was hit: the rigidbody's object when there is one, otherwise the
    /// collider's own. The same key the window's hit set de-duplicates on, so
    /// one target never produces two of these in one swing.
    /// </summary>
    public GameObject Target { get; }

    /// <summary>The collider the scan actually found.</summary>
    public Collider Collider { get; }

    /// <summary>
    /// Contact on the gameplay plane (x = GameplayPlane.Depth) - the same
    /// point knockback and slicing use. It lies inside the target's bounds,
    /// not on its surface, so anything drawn here must be brought out to the
    /// camera-facing side first or the target's own mesh will hide it.
    /// </summary>
    public Vector3 Point { get; }

    /// <summary>The target's centre, on the plane.</summary>
    public Vector3 TargetCenter { get; }

    /// <summary>
    /// Which way the edge travelled through the target, as a planar unit
    /// vector. Read off the blade's turn when it is turning, so a lunge that
    /// carries the whole character cannot flip it.
    /// </summary>
    public Vector3 SlashDirection { get; }

    /// <summary>Planar speed of the hitbox at the moment of contact, units/sec.</summary>
    public float SwingSpeed { get; }

    /// <summary>
    /// True for the first target this swing's hit window confirmed. Feedback
    /// that belongs to the swing rather than to each target - a camera shake,
    /// say - keys off this so a cut through a crowd lands once.
    /// </summary>
    public bool IsFirstOfSwing { get; }

    public WeaponHit(
        AttackDefinition attack,
        GameObject target,
        Collider collider,
        Vector3 point,
        Vector3 targetCenter,
        Vector3 slashDirection,
        float swingSpeed,
        bool isFirstOfSwing)
    {
        Attack = attack;
        Target = target;
        Collider = collider;
        Point = point;
        TargetCenter = targetCenter;
        SlashDirection = slashDirection;
        SwingSpeed = swingSpeed;
        IsFirstOfSwing = isFirstOfSwing;
    }
}
