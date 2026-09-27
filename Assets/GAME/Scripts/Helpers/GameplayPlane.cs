using UnityEngine;

/// <summary>
/// The one plane the game is actually played on: X = 0, spanning Y and Z.
///
/// The models and the animation are three dimensional and the rules are not.
/// A swing throws the hand wherever the animator wanted it, several
/// centimetres either side of the plane over the course of one attack, and a
/// hit that was resolved where the hand happened to be would land differently
/// depending on which clip was playing. So the rig is presentation and this is
/// simulation, and the two are kept apart by projecting once, here, on the way
/// in.
///
/// That is the whole discipline, and the failure it prevents is subtle rather
/// than dramatic. Nothing crashes when a depth leaks through; the game just
/// becomes slightly unfair in ways that cannot be reproduced, because the
/// difference lives in an axis nobody can see. A contact point half a metre
/// off the plane still knocks the target back, just along a slightly wrong
/// vector - and the only symptom is that some hits feel off.
///
/// Every authoritative value therefore lives here, not just the overlap test:
/// the contact point, the knockback direction, the lever arm a torque is taken
/// about. Presentation may put an effect wherever it looks best, but it takes
/// that decision downstream and explicitly, from a value that was planar when
/// it was handed over.
///
/// The planar convention is (x = world Z, y = world Y): screen-right and
/// screen-up for a camera looking down +X.
/// </summary>
public static class GameplayPlane
{
    /// <summary>Where the plane sits along its own normal.</summary>
    public const float Depth = 0f;

    /// <summary>The axes anything on the plane is free to move in.</summary>
    public const PhysicsAxes Axes = PhysicsAxes.YZ;

    /// <summary>
    /// Plane normal, pointing at the camera. The single definition of which
    /// way is "into the screen" - systems that need it take it from here
    /// rather than writing Vector3.right and quietly disagreeing later.
    /// </summary>
    public static readonly Vector3 Normal = Vector3.right;

    private const float DirectionEpsilon = 1e-6f;

    /// <summary>Drops a world point onto the plane.</summary>
    public static Vector3 Flatten(Vector3 world)
    {
        return new Vector3(Depth, world.y, world.z);
    }

    /// <summary>
    /// Flattens a direction and renormalises it, or returns zero when it was
    /// pointing straight through the plane and has nothing left.
    /// </summary>
    public static Vector3 FlattenDirection(Vector3 world)
    {
        world.x = 0f;

        return world.sqrMagnitude > DirectionEpsilon
            ? world.normalized
            : Vector3.zero;
    }

    /// <summary>World point to plane coordinates.</summary>
    public static Vector2 ToPlanar(Vector3 world)
    {
        return new Vector2(world.z, world.y);
    }

    /// <summary>Plane coordinates back to a world point on the plane.</summary>
    public static Vector3 FromPlanar(Vector2 planar)
    {
        return new Vector3(Depth, planar.y, planar.x);
    }

    /// <summary>
    /// Plane coordinates back to a world point at a chosen depth.
    /// </summary>
    /// <remarks>
    /// For presentation only - an effect that should sit on the model rather
    /// than on the plane. Nothing that decides an outcome should call this.
    /// </remarks>
    public static Vector3 FromPlanarAtDepth(Vector2 planar, float depth)
    {
        return new Vector3(depth, planar.y, planar.x);
    }

    /// <summary>How far off the plane a world point is.</summary>
    public static float DepthOf(Vector3 world)
    {
        return world.x - Depth;
    }
}
