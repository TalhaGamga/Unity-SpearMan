using UnityEngine;

public readonly struct HitContext
{
    private const float MinMotionSqrMagnitude = 0.0001f;
    private readonly Vector3 _fallbackDirection;

    public Vector3 Velocity { get; }
    public Vector3 Direction => GetDirection(PhysicsAxes.YZ);
    public Vector3 Point { get; }
    public Vector3 SlicePlaneNormal { get; }
    public Vector3 AttackerForward { get; }
    public float Speed => GetSpeed(PhysicsAxes.YZ);

    public HitContext(
        Vector3 velocity,
        Vector3 point,
        Vector3 fallbackDirection,
        Vector3 bladeDirection,
        Vector3 attackerForward)
    {
        Velocity = PhysicsAxesUtility.Project(
            velocity,
            PhysicsAxes.YZ
        );
        _fallbackDirection = PhysicsAxesUtility.Project(
            fallbackDirection,
            PhysicsAxes.YZ
        );
        // Flattened again even though the sensor already hands it over on the
        // plane. This is the boundary every hit crosses on its way into the
        // rules, and a point that arrived with a depth on it would be carried
        // straight into a knockback vector, a torque lever arm and a slice
        // plane before anything noticed - all of them silently wrong, and
        // wrong along the one axis the camera cannot show.
        Point = GameplayPlane.Flatten(point);
        AttackerForward = PhysicsAxesUtility.Direction(
            attackerForward,
            PhysicsAxes.YZ
        );

        Vector3 planarBladeDirection = PhysicsAxesUtility.Project(
            bladeDirection,
            PhysicsAxes.YZ
        );
        Vector3 planeNormal = Vector3.Cross(
            Velocity,
            planarBladeDirection
        );
        SlicePlaneNormal = planeNormal.sqrMagnitude > MinMotionSqrMagnitude
            ? planeNormal.normalized
            : Vector3.zero;
    }

    public Vector3 GetDirection(PhysicsAxes axes)
    {
        Vector3 velocity = PhysicsAxesUtility.Project(Velocity, axes);
        if (velocity.sqrMagnitude > MinMotionSqrMagnitude)
            return velocity.normalized;

        Vector3 fallback = PhysicsAxesUtility.Project(_fallbackDirection, axes);
        return fallback.sqrMagnitude > MinMotionSqrMagnitude
            ? fallback.normalized
            : Vector3.zero;
    }

    public float GetSpeed(PhysicsAxes axes)
    {
        return PhysicsAxesUtility.Project(Velocity, axes).magnitude;
    }
}
